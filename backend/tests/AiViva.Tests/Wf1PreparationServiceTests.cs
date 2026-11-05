using System.Text;
using AiViva.Application.Modules.QuestionPreparation.Wf1;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;
using AiViva.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiViva.Tests;

public sealed class Wf1PreparationServiceTests : IAsyncDisposable
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4 test");
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly QuestionBankDbContext db;
    private readonly FakeAiGenerator ai = new();
    private readonly FakeExtractor extractor = new();
    private readonly FakeEmbeddings embeddings = new();
    private readonly Wf1PreparationService service;
    private readonly Wf1Actor lecturer;

    public Wf1PreparationServiceTests()
    {
        connection.Open();
        connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        db = new QuestionBankDbContext(new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var role = db.Roles.Single(item => item.Code == Role.Codes.Lecturer);
        var user = new UserAccount("wf1-lecturer@example.local", role.Id);
        db.Users.Add(user);
        db.SaveChanges();
        lecturer = new Wf1Actor(user.Id, Role.Codes.Lecturer);
        service = new Wf1PreparationService(db, ai, extractor, embeddings);
    }

    [Fact]
    public async Task Generate_review_and_approve_creates_official_question_and_subject_rubrics()
    {
        var subject = await service.CreateSubjectAsync(
            new CreateSubjectRequest("Software Design", "Architecture", "Main bank"), lecturer);
        await service.UploadDocumentAsync(subject.Id, "lesson.pdf", "application/pdf", Pdf, lecturer);

        var generated = await service.GenerateAsync(subject.Id,
            new GenerateWf1ContentRequest(subject.QuestionBanks.Single().Id, 2), lecturer);

        generated.QuestionCandidates.Should().HaveCount(2);
        generated.QuestionCandidates.Should().OnlyContain(item => item.Status == "Pending");
        generated.DraftRubricSet.Should().NotBeNull();
        generated.DraftRubricSet!.Rubrics.Sum(item => item.WeightPercent).Should().Be(100);

        var candidate = generated.QuestionCandidates.First();
        var approved = await service.ReviewCandidateAsync(subject.Id, candidate.Id,
            new ReviewAiQuestionCandidateRequest(true), lecturer);
        approved.Status.Should().Be("Approved");
        approved.ApprovedQuestionId.Should().NotBeNull();

        var question = await db.Questions.SingleAsync(item => item.Id == approved.ApprovedQuestionId);
        question.BloomLevel.Should().Be("Understand");
        question.DifficultyLevel.Should().Be(2);
        (await db.ExpectedKeyPoints.CountAsync(item => item.QuestionId == question.Id)).Should().Be(2);

        var approvedSet = await service.ApproveRubricSetAsync(
            subject.Id, generated.DraftRubricSet.Id, lecturer);
        approvedSet.Status.Should().Be("Approved");
        (await service.GetWorkspaceAsync(subject.Id, lecturer)).ApprovedRubricSet!.Id
            .Should().Be(approvedSet.Id);
    }

    [Fact]
    public async Task Approving_a_new_rubric_version_archives_the_previous_one()
    {
        // Only one approved set per subject is allowed by a unique index. The order of the two
        // UPDATEs used to depend on the random ids, so re-approval failed about half the time.
        var subject = await service.CreateSubjectAsync(
            new CreateSubjectRequest("Software Design", "Architecture", "Main bank"), lecturer);
        SaveRubricSetRequest Rubric(string name) => new([
            new(name, null, 60, [new("Accuracy", null, 5)]), new("Delivery", null, 40, [new("Clarity", null, 5)])]);

        var versions = new List<Guid>();
        for (var version = 1; version <= 6; version++)
        {
            var draft = await service.SaveDraftRubricSetAsync(subject.Id, Rubric($"Knowledge v{version}"), lecturer);
            versions.Add((await service.ApproveRubricSetAsync(subject.Id, draft.Id, lecturer)).Id);
        }

        (await service.GetWorkspaceAsync(subject.Id, lecturer)).ApprovedRubricSet!.Id.Should().Be(versions[^1]);
        (await db.RubricSets.CountAsync(set => set.SubjectId == subject.Id && set.Status == "Approved")).Should().Be(1);
        (await db.RubricSets.CountAsync(set => set.SubjectId == subject.Id && set.Status == "Archived")).Should().Be(5);
    }

    [Fact]
    public async Task Generate_requires_a_study_document()
    {
        var subject = await service.CreateSubjectAsync(
            new CreateSubjectRequest("Databases", null, "Database bank"), lecturer);

        var action = () => service.GenerateAsync(subject.Id,
            new GenerateWf1ContentRequest(subject.QuestionBanks.Single().Id, 1), lecturer);

        var exception = await action.Should().ThrowAsync<Wf1Exception>();
        exception.Which.Code.Should().Be("study_document_required");
        exception.Which.Failure.Should().Be(Wf1Failure.Conflict);
    }

    [Fact]
    public async Task Upload_indexes_pdf_text_into_embedded_passages()
    {
        var subject = await service.CreateSubjectAsync(new CreateSubjectRequest("Software Design", null, "Bank"), lecturer);

        var document = await service.UploadDocumentAsync(subject.Id, "lesson.pdf", "application/pdf", Pdf, lecturer);

        document.IndexStatus.Should().Be("Indexed");
        document.PageCount.Should().Be(2);
        document.ChunkCount.Should().Be(2);
        document.SemanticSearch.Should().BeTrue();
        (await db.StudyDocumentChunks.CountAsync(chunk => chunk.Embedding != null)).Should().Be(2);
        (await service.GetWorkspaceAsync(subject.Id, lecturer)).Documents.Single().ChunkCount.Should().Be(2);
    }

    [Fact]
    public async Task Topic_retrieves_the_relevant_passage_and_the_question_cites_its_page()
    {
        extractor.Pages = Enumerable.Range(1, 12).Select(page => page == 7
            ? Page("Database normalization removes redundancy by splitting tables into normal forms.")
            : Page($"Chapter {page} explains layered software architecture and component responsibilities.")).ToArray();
        var subject = await service.CreateSubjectAsync(new CreateSubjectRequest("Software Design", null, "Bank"), lecturer);
        await service.UploadDocumentAsync(subject.Id, "syllabus.pdf", "application/pdf", Pdf, lecturer);

        var generated = await service.GenerateAsync(subject.Id,
            new GenerateWf1ContentRequest(subject.QuestionBanks.Single().Id, 2, "normalization"), lecturer);

        ai.LastTopic.Should().Be("normalization");
        ai.LastQuestionGrounding!.Chunks.Should().HaveCount(8);
        ai.LastQuestionGrounding.Chunks.Should().Contain(chunk => chunk.PageStart == 7);
        var sources = generated.QuestionCandidates.First().Sources!;
        sources.Should().ContainSingle();
        sources[0].FileName.Should().Be("syllabus.pdf");
        sources[0].PageStart.Should().Be(7);
        sources[0].Excerpt.Should().Contain("normalization");
        generated.QuestionCandidates.First().Topic.Should().Be("normalization");
        embeddings.QueryCalls.Should().Be(1);
    }

    [Fact]
    public async Task Scanned_pdf_without_text_is_attached_whole_and_cited_as_a_document()
    {
        extractor.Pages = ["", "   "];
        var subject = await service.CreateSubjectAsync(new CreateSubjectRequest("Networks", null, "Bank"), lecturer);
        var document = await service.UploadDocumentAsync(subject.Id, "scan.pdf", "application/pdf", Pdf, lecturer);
        document.IndexStatus.Should().Be("NoText");

        var generated = await service.GenerateAsync(subject.Id,
            new GenerateWf1ContentRequest(subject.QuestionBanks.Single().Id, 1), lecturer);

        ai.LastQuestionGrounding!.Chunks.Should().BeEmpty();
        ai.LastQuestionGrounding.Documents.Should().ContainSingle(item => item.FileName == "scan.pdf");
        generated.QuestionCandidates.Single().Sources.Should().ContainSingle(item => item.FileName == "scan.pdf");
    }

    [Fact]
    public async Task Missing_embedding_provider_falls_back_to_keyword_retrieval_and_can_be_reindexed()
    {
        embeddings.Fail = true;
        var subject = await service.CreateSubjectAsync(new CreateSubjectRequest("Software Design", null, "Bank"), lecturer);
        var document = await service.UploadDocumentAsync(subject.Id, "lesson.pdf", "application/pdf", Pdf, lecturer);
        document.IndexStatus.Should().Be("Indexed");
        document.SemanticSearch.Should().BeFalse();
        document.IndexError.Should().Contain("từ khóa");

        await service.GenerateAsync(subject.Id,
            new GenerateWf1ContentRequest(subject.QuestionBanks.Single().Id, 1, "injection"), lecturer);
        ai.LastQuestionGrounding!.Chunks.Should().NotBeEmpty();

        embeddings.Fail = false;
        var reindexed = await service.ReindexDocumentAsync(subject.Id, document.Id, lecturer);
        reindexed.SemanticSearch.Should().BeTrue();
        reindexed.IndexError.Should().BeNull();
    }

    [Fact]
    public async Task Documents_uploaded_before_indexing_are_indexed_on_first_generation()
    {
        var subject = await service.CreateSubjectAsync(new CreateSubjectRequest("Software Design", null, "Bank"), lecturer);
        db.StudyDocuments.Add(new StudyDocument
        {
            Id = Guid.NewGuid(),
            SubjectId = subject.Id,
            FileName = "legacy.pdf",
            MimeType = "application/pdf",
            SizeBytes = Pdf.Length,
            Content = Pdf,
            UploadedBy = lecturer.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var generated = await service.GenerateAsync(subject.Id,
            new GenerateWf1ContentRequest(subject.QuestionBanks.Single().Id, 1), lecturer);

        generated.Documents.Single().IndexStatus.Should().Be("Indexed");
        ai.LastQuestionGrounding!.Chunks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Generating_questions_only_keeps_the_lecturers_rubric_draft()
    {
        var subject = await service.CreateSubjectAsync(new CreateSubjectRequest("Software Design", null, "Bank"), lecturer);
        await service.UploadDocumentAsync(subject.Id, "lesson.pdf", "application/pdf", Pdf, lecturer);
        var manual = await service.SaveDraftRubricSetAsync(subject.Id, new SaveRubricSetRequest(
            [new SaveSubjectRubricRequest("Kiến thức", null, 100, [new SaveSubjectRubricCriterionRequest("Đúng", null, 10)])]),
            lecturer);

        var generated = await service.GenerateAsync(subject.Id,
            new GenerateWf1ContentRequest(subject.QuestionBanks.Single().Id, 1, null, IncludeRubrics: false), lecturer);

        ai.RubricCalls.Should().Be(0);
        generated.DraftRubricSet!.Id.Should().Be(manual.Id);
        generated.DraftRubricSet.Source.Should().Be("Manual");
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    // Long enough that two pages never merge into one passage.
    private static string Page(string sentence) => string.Join(" ", Enumerable.Repeat(sentence, 9));

    private sealed class FakeExtractor : IDocumentTextExtractor
    {
        public string[] Pages { get; set; } =
        [
            Page("Dependency injection supplies collaborators from outside the class."),
            Page("Layered architecture separates presentation, application and persistence.")
        ];

        public IReadOnlyList<string> ExtractPages(byte[] content) => Pages;
    }

    private sealed class FakeEmbeddings : IEmbeddingGenerator
    {
        private static readonly string[] Vocabulary = ["dependency", "injection", "normalization", "database", "architecture"];
        public bool Fail { get; set; }
        public int QueryCalls { get; private set; }
        public string Model => "fake-embedding";

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, bool query,
            CancellationToken cancellationToken = default)
        {
            if (Fail) throw new AiGenerationException(AiGenerationFailure.NotConfigured, "No key");
            if (query) QueryCalls++;
            IReadOnlyList<float[]> vectors = texts.Select(text => Vocabulary
                .Select(word => (float)text.Split(' ').Count(token => token.StartsWith(word, StringComparison.OrdinalIgnoreCase)) + 0.01f)
                .ToArray()).ToArray();
            return Task.FromResult(vectors);
        }
    }

    private sealed class FakeAiGenerator : IAiContentGenerator
    {
        public string Model => "fake-gemini";
        public string? LastTopic { get; private set; }
        public AiGroundingContext? LastQuestionGrounding { get; private set; }
        public int RubricCalls { get; private set; }

        public Task<IReadOnlyList<AiQuestionSuggestion>> GenerateQuestionsAsync(
            string subjectName, string? topic, AiGroundingContext grounding, int count,
            CancellationToken cancellationToken = default)
        {
            LastTopic = topic;
            LastQuestionGrounding = grounding;
            // Cite the passage about the topic (or the first source), plus an unknown label that must be ignored.
            var cited = grounding.Chunks.FirstOrDefault(chunk => topic is not null &&
                chunk.Text.Contains(topic, StringComparison.OrdinalIgnoreCase))?.Label
                ?? grounding.Chunks.FirstOrDefault()?.Label ?? "D1";
            IReadOnlyList<AiQuestionSuggestion> result = Enumerable.Range(1, count)
                .Select(index => new AiQuestionSuggestion(
                    $"Question {index} for {subjectName}?", "Understand", 2,
                    "Requires explanation.", ["Core definition", "Practical meaning"], [cited, "S999"]))
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<AiRubricSuggestion>> GenerateRubricsAsync(
            string subjectName, AiGroundingContext grounding,
            CancellationToken cancellationToken = default)
        {
            RubricCalls++;
            IReadOnlyList<AiRubricSuggestion> result =
            [
                new("Accuracy", "Correct subject knowledge", 60,
                    [new AiCriterionSuggestion("Correctness", null, 10)]),
                new("Communication", "Clear oral delivery", 40,
                    [new AiCriterionSuggestion("Clarity", null, 10)])
            ];
            return Task.FromResult(result);
        }
    }
}
