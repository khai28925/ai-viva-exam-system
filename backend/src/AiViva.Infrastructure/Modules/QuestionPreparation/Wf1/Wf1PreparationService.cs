using System.Text.Json;
using AiViva.Application.Modules.QuestionPreparation.Wf1;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;

public sealed class Wf1PreparationService(
    QuestionBankDbContext db, IAiContentGenerator ai, IDocumentTextExtractor extractor,
    IEmbeddingGenerator embeddings) : IWf1PreparationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int RubricContextChunks = 16;

    public async Task<IReadOnlyCollection<SubjectSummaryDto>> GetSubjectsAsync(
        Wf1Actor actor, CancellationToken ct = default)
    {
        var query = db.Subjects.AsNoTracking();
        if (!actor.IsAdmin) query = query.Where(subject => subject.CreatedBy == actor.Id || subject.CreatedBy == null);
        var subjects = await query.OrderBy(subject => subject.Name).ToArrayAsync(ct);
        var subjectIds = subjects.Select(subject => subject.Id).ToArray();
        var banks = await db.QuestionBanks.AsNoTracking().Where(bank => subjectIds.Contains(bank.SubjectId))
            .OrderBy(bank => bank.Name).ToArrayAsync(ct);
        return subjects.Select(subject => ToSummary(subject, banks.Where(bank => bank.SubjectId == subject.Id))).ToArray();
    }

    public async Task<SubjectWorkspaceDto> GetWorkspaceAsync(
        Guid subjectId, Wf1Actor actor, CancellationToken ct = default)
    {
        var subject = await OwnedSubjectAsync(subjectId, actor, ct);
        var banks = await db.QuestionBanks.AsNoTracking().Where(bank => bank.SubjectId == subjectId)
            .OrderBy(bank => bank.Name).ToArrayAsync(ct);
        // Listing metadata only: the PDF bytes (up to 10 MB each) are not needed here.
        var documents = (await db.StudyDocuments.AsNoTracking()
            .Where(document => document.SubjectId == subjectId)
            .Select(document => new StudyDocument
            {
                Id = document.Id,
                SubjectId = document.SubjectId,
                FileName = document.FileName,
                MimeType = document.MimeType,
                SizeBytes = document.SizeBytes,
                CreatedAt = document.CreatedAt,
                IndexStatus = document.IndexStatus,
                PageCount = document.PageCount,
                IndexError = document.IndexError
            }).ToArrayAsync(ct))
            .OrderByDescending(document => document.CreatedAt).ToArray();
        var candidates = (await db.AiQuestionCandidates.AsNoTracking()
            .Where(candidate => candidate.SubjectId == subjectId).ToArrayAsync(ct))
            .OrderByDescending(candidate => candidate.CreatedAt).ToArray();
        var runIds = candidates.Select(candidate => candidate.GenerationRunId).Distinct().ToArray();
        var topics = await db.AiGenerationRuns.AsNoTracking().Where(run => runIds.Contains(run.Id))
            .ToDictionaryAsync(run => run.Id, run => run.Topic, ct);
        var sets = (await db.RubricSets.AsNoTracking()
            .Where(set => set.SubjectId == subjectId && set.Status != "Archived").ToArrayAsync(ct))
            .OrderByDescending(set => set.Version).ToArray();
        var documentIds = documents.Select(document => document.Id).ToArray();
        var chunkStats = (await db.StudyDocumentChunks.AsNoTracking()
                .Where(chunk => documentIds.Contains(chunk.DocumentId))
                .Select(chunk => new { chunk.DocumentId, HasEmbedding = chunk.Embedding != null }).ToArrayAsync(ct))
            .GroupBy(chunk => chunk.DocumentId)
            .ToDictionary(group => group.Key, group => (Count: group.Count(), Semantic: group.All(c => c.HasEmbedding)));
        return new SubjectWorkspaceDto(
            ToSummary(subject, banks),
            documents.Select(document => ToDocumentDto(document,
                chunkStats.GetValueOrDefault(document.Id))).ToArray(),
            candidates.Select(candidate => ToCandidateDto(candidate, topics.GetValueOrDefault(candidate.GenerationRunId))).ToArray(),
            await ToRubricSetDtoAsync(sets.FirstOrDefault(set => set.Status == "Draft"), ct),
            await ToRubricSetDtoAsync(sets.FirstOrDefault(set => set.Status == "Approved"), ct));
    }

    public async Task<SubjectSummaryDto> CreateSubjectAsync(
        CreateSubjectRequest request, Wf1Actor actor, CancellationToken ct = default)
    {
        EnsureActor(actor);
        var subject = new Subject(request.Name, request.Description, actor.Id);
        var bank = new QuestionBank(request.QuestionBankName, $"Question bank for {subject.Name}", actor.Id);
        bank.AssignSubject(subject.Id);
        db.Subjects.Add(subject);
        db.QuestionBanks.Add(bank);
        await db.SaveChangesAsync(ct);
        return ToSummary(subject, [bank]);
    }

    public async Task<SubjectBankDto> CreateQuestionBankAsync(
        Guid subjectId, CreateSubjectQuestionBankRequest request, Wf1Actor actor,
        CancellationToken ct = default)
    {
        await OwnedSubjectAsync(subjectId, actor, ct);
        var bank = new QuestionBank(request.Name, request.Description, actor.Id);
        bank.AssignSubject(subjectId);
        db.QuestionBanks.Add(bank);
        await db.SaveChangesAsync(ct);
        return new SubjectBankDto(bank.Id, bank.Name, bank.Description);
    }

    public async Task<StudyDocumentDto> UploadDocumentAsync(
        Guid subjectId, string fileName, string mimeType, byte[] content, Wf1Actor actor,
        CancellationToken ct = default)
    {
        await OwnedSubjectAsync(subjectId, actor, ct);
        if (!string.Equals(mimeType, "application/pdf", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase) ||
            !content.AsSpan().StartsWith("%PDF-"u8))
            throw Invalid("Only PDF study documents are supported in this workflow.", "unsupported_document_type");
        if (content.Length == 0 || content.Length > 10 * 1024 * 1024)
            throw Invalid("Document size must be between 1 byte and 10 MB.", "invalid_document_size");
        var normalizedName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 255)
            throw Invalid("File name is invalid.", "invalid_file_name");
        var document = new StudyDocument
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            FileName = normalizedName,
            MimeType = mimeType,
            SizeBytes = content.LongLength,
            Content = content,
            UploadedBy = actor.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.StudyDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return await IndexDocumentAsync(document, ct);
    }

    public async Task<StudyDocumentDto> ReindexDocumentAsync(
        Guid subjectId, Guid documentId, Wf1Actor actor, CancellationToken ct = default)
    {
        await OwnedSubjectAsync(subjectId, actor, ct);
        var document = await db.StudyDocuments.SingleOrDefaultAsync(
            item => item.Id == documentId && item.SubjectId == subjectId, ct)
            ?? throw Missing("Study document was not found.");
        return await IndexDocumentAsync(document, ct);
    }

    // RAG indexing: extract page text, split it into passages and embed them. Indexing problems
    // never reject the upload; a scanned PDF is sent whole to the model and a missing embedding
    // provider degrades retrieval to keyword ranking.
    private async Task<StudyDocumentDto> IndexDocumentAsync(StudyDocument document, CancellationToken ct)
    {
        await db.StudyDocumentChunks.Where(chunk => chunk.DocumentId == document.Id).ExecuteDeleteAsync(ct);
        IReadOnlyList<string> pages;
        try { pages = extractor.ExtractPages(document.Content); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            document.IndexStatus = "Failed";
            document.IndexError = "Không đọc được nội dung PDF; tài liệu sẽ được gửi nguyên văn cho AI.";
            document.IndexedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return ToDocumentDto(document, (0, false));
        }

        var passages = StudyMaterialChunker.Chunk(pages);
        document.PageCount = pages.Count;
        document.IndexedAt = DateTimeOffset.UtcNow;
        document.IndexError = null;
        document.EmbeddingModel = null;
        if (passages.Count == 0)
        {
            document.IndexStatus = "NoText";
            await db.SaveChangesAsync(ct);
            return ToDocumentDto(document, (0, false));
        }

        IReadOnlyList<float[]>? vectors = null;
        try
        {
            vectors = await embeddings.EmbedAsync(passages.Select(p => p.Text).ToArray(), query: false, ct);
            document.EmbeddingModel = embeddings.Model;
        }
        catch (AiGenerationException exception)
        {
            document.IndexError = $"Chưa tạo được embedding ({exception.Failure}); đang dùng tìm kiếm theo từ khóa.";
        }
        db.StudyDocumentChunks.AddRange(passages.Select((passage, index) => new StudyDocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SubjectId = document.SubjectId,
            ChunkIndex = index,
            PageStart = passage.PageStart,
            PageEnd = passage.PageEnd,
            Content = passage.Text,
            Embedding = vectors is null ? null : ToBytes(vectors[index])
        }));
        document.IndexStatus = "Indexed";
        await db.SaveChangesAsync(ct);
        return ToDocumentDto(document, (passages.Count, vectors is not null));
    }

    public async Task DeleteDocumentAsync(
        Guid subjectId, Guid documentId, Wf1Actor actor, CancellationToken ct = default)
    {
        await OwnedSubjectAsync(subjectId, actor, ct);
        var document = await db.StudyDocuments.SingleOrDefaultAsync(
            item => item.Id == documentId && item.SubjectId == subjectId, ct)
            ?? throw Missing("Study document was not found.");
        db.StudyDocuments.Remove(document);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SubjectWorkspaceDto> GenerateAsync(
        Guid subjectId, GenerateWf1ContentRequest request, Wf1Actor actor,
        CancellationToken ct = default)
    {
        var subject = await OwnedSubjectAsync(subjectId, actor, ct);
        if (request.QuestionCount is < 1 or > 50)
            throw Invalid("Question count must be between 1 and 50.", "invalid_question_count");
        if (!await db.QuestionBanks.AnyAsync(bank =>
                bank.Id == request.QuestionBankId && bank.SubjectId == subjectId, ct))
            throw Missing("Question bank was not found in this subject.");
        var topic = string.IsNullOrWhiteSpace(request.Topic) ? null : request.Topic.Trim();
        if (topic?.Length > 200) throw Invalid("Topic must not exceed 200 characters.", "invalid_topic");
        var documents = await db.StudyDocuments.Where(item => item.SubjectId == subjectId).ToArrayAsync(ct);
        if (documents.Length == 0)
            throw new Wf1Exception(Wf1Failure.Conflict,
                "Upload at least one PDF before asking AI to generate content.", "study_document_required");
        // Documents uploaded before RAG indexing existed are indexed on first use.
        foreach (var pending in documents.Where(document => document.IndexStatus == "Pending"))
            await IndexDocumentAsync(pending, ct);

        var (questionGrounding, questionSources) = await GroundingAsync(
            subjectId, documents, topic, Math.Clamp(request.QuestionCount * 2, 8, 30), ct);

        var run = new AiGenerationRun
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            QuestionBankId = request.QuestionBankId,
            RequestedQuestionCount = request.QuestionCount,
            Topic = topic,
            RetrievedChunkCount = questionGrounding.Chunks.Count,
            Model = ai.Model,
            Status = "Running",
            CreatedBy = actor.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AiGenerationRuns.Add(run);
        await db.SaveChangesAsync(ct);

        IReadOnlyList<AiQuestionSuggestion> questions;
        IReadOnlyList<AiRubricSuggestion> rubrics = [];
        try
        {
            questions = await ai.GenerateQuestionsAsync(subject.Name, topic, questionGrounding, request.QuestionCount, ct);
            // Record the model that actually answered: a fallback may serve when the primary is unavailable.
            var servedBy = new List<string> { ai.Model };
            if (request.IncludeRubrics)
            {
                var (rubricGrounding, _) = await GroundingAsync(subjectId, documents, null, RubricContextChunks, ct);
                rubrics = await ai.GenerateRubricsAsync(subject.Name, rubricGrounding, ct);
                if (!servedBy.Contains(ai.Model)) servedBy.Add(ai.Model);
            }
            run.Model = Truncate(string.Join(", ", servedBy), 80);
        }
        catch (AiGenerationException exception)
        {
            run.Status = "Failed";
            run.ErrorMessage = exception.Message[..Math.Min(exception.Message.Length, 1000)];
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            throw exception.Failure switch
            {
                AiGenerationFailure.RateLimited => new Wf1Exception(Wf1Failure.AiRateLimited, exception.Message, "ai_rate_limited"),
                _ => new Wf1Exception(Wf1Failure.AiUnavailable, exception.Message, "ai_unavailable")
            };
        }

        if (request.IncludeRubrics)
            await ReplaceDraftRubricSetAsync(subjectId, actor.Id, "AI", rubrics.Select(rubric =>
                new SaveSubjectRubricRequest(rubric.Name, rubric.Description, rubric.WeightPercent,
                    rubric.Criteria.Select(criterion => new SaveSubjectRubricCriterionRequest(
                        criterion.Name, criterion.Description, criterion.MaxScore)).ToArray())).ToArray(), true, ct);
        db.AiQuestionCandidates.AddRange(questions.Select(question => new AiQuestionCandidate
        {
            Id = Guid.NewGuid(),
            GenerationRunId = run.Id,
            SubjectId = subjectId,
            QuestionBankId = request.QuestionBankId,
            Content = question.Content.Trim(),
            BloomLevel = question.BloomLevel,
            DifficultyLevel = question.DifficultyLevel,
            Rationale = Truncate(question.Rationale.Trim(), 1000),
            ExpectedKeyPointsJson = JsonSerializer.Serialize(question.ExpectedKeyPoints, JsonOptions),
            SourceRefsJson = JsonSerializer.Serialize((question.SourceLabels ?? [])
                .Where(questionSources.ContainsKey).Select(label => questionSources[label]).ToArray(), JsonOptions),
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow
        }));
        run.Status = "Completed";
        run.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetWorkspaceAsync(subjectId, actor, ct);
    }

    public async Task<AiQuestionCandidateDto> UpdateCandidateAsync(
        Guid subjectId, Guid candidateId, UpdateAiQuestionCandidateRequest request, Wf1Actor actor,
        CancellationToken ct = default)
    {
        await OwnedSubjectAsync(subjectId, actor, ct);
        ValidateCandidate(request);
        var candidate = await CandidateAsync(subjectId, candidateId, ct);
        if (candidate.Status != "Pending")
            throw new Wf1Exception(Wf1Failure.Conflict, "Only a pending question can be edited.", "candidate_reviewed");
        candidate.Content = request.Content.Trim();
        candidate.BloomLevel = request.BloomLevel;
        candidate.DifficultyLevel = request.DifficultyLevel;
        candidate.Rationale = request.Rationale.Trim();
        candidate.ExpectedKeyPointsJson = JsonSerializer.Serialize(
            request.ExpectedKeyPoints.Select(point => point.Trim()).Where(point => point.Length > 0), JsonOptions);
        await db.SaveChangesAsync(ct);
        return ToCandidateDto(candidate);
    }

    public async Task<AiQuestionCandidateDto> ReviewCandidateAsync(
        Guid subjectId, Guid candidateId, ReviewAiQuestionCandidateRequest request, Wf1Actor actor,
        CancellationToken ct = default)
    {
        await OwnedSubjectAsync(subjectId, actor, ct);
        var candidate = await CandidateAsync(subjectId, candidateId, ct);
        if (candidate.Status != "Pending")
            throw new Wf1Exception(Wf1Failure.Conflict, "This question was already reviewed.", "candidate_reviewed");
        if (request.Approve)
        {
            var question = new Question(candidate.QuestionBankId, candidate.Content);
            question.UpdateClassification(candidate.BloomLevel, candidate.DifficultyLevel);
            db.Questions.Add(question);
            var points = JsonSerializer.Deserialize<string[]>(candidate.ExpectedKeyPointsJson, JsonOptions) ?? [];
            db.ExpectedKeyPoints.AddRange(points.Select((point, index) => new ExpectedKeyPoint
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Content = point,
                DisplayOrder = index,
                CreatedAt = DateTimeOffset.UtcNow
            }));
            candidate.Status = "Approved";
            candidate.ApprovedQuestionId = question.Id;
        }
        else candidate.Status = "Rejected";
        candidate.ReviewedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToCandidateDto(candidate);
    }

    public async Task<RubricSetDto> SaveDraftRubricSetAsync(
        Guid subjectId, SaveRubricSetRequest request, Wf1Actor actor, CancellationToken ct = default)
    {
        await OwnedSubjectAsync(subjectId, actor, ct);
        ValidateRubrics(request.Rubrics, requireTotal: false);
        var set = await ReplaceDraftRubricSetAsync(subjectId, actor.Id, "Manual", request.Rubrics, false, ct);
        await db.SaveChangesAsync(ct);
        return (await ToRubricSetDtoAsync(set, ct))!;
    }

    public async Task<RubricSetDto> ApproveRubricSetAsync(
        Guid subjectId, Guid rubricSetId, Wf1Actor actor, CancellationToken ct = default)
    {
        await OwnedSubjectAsync(subjectId, actor, ct);
        var set = await db.RubricSets.SingleOrDefaultAsync(item =>
            item.Id == rubricSetId && item.SubjectId == subjectId, ct) ?? throw Missing("Rubric set was not found.");
        if (set.Status != "Draft")
            throw new Wf1Exception(Wf1Failure.Conflict, "Only a draft rubric set can be approved.", "rubric_set_not_draft");
        var dto = (await ToRubricSetDtoAsync(set, ct))!;
        ValidateRubrics(dto.Rubrics.Select(rubric => new SaveSubjectRubricRequest(
            rubric.Name, rubric.Description, rubric.WeightPercent,
            rubric.Criteria.Select(criterion => new SaveSubjectRubricCriterionRequest(
                criterion.Name, criterion.Description, criterion.MaxScore)).ToArray())).ToArray(), requireTotal: true);
        var previous = await db.RubricSets.Where(item =>
            item.SubjectId == subjectId && item.Status == "Approved").ToArrayAsync(ct);
        // A unique index allows one approved set per subject and is checked per statement, so the
        // previous set must be archived before the new one is approved; one transaction keeps it atomic.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var item in previous) item.Status = "Archived";
        if (previous.Length > 0) await db.SaveChangesAsync(ct);
        set.Status = "Approved";
        set.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (await ToRubricSetDtoAsync(set, ct))!;
    }

    private async Task<RubricSet> ReplaceDraftRubricSetAsync(
        Guid subjectId, Guid createdBy, string source, IReadOnlyList<SaveSubjectRubricRequest> rubrics,
        bool requireTotal, CancellationToken ct)
    {
        ValidateRubrics(rubrics, requireTotal);
        var oldDrafts = await db.RubricSets.Where(set => set.SubjectId == subjectId && set.Status == "Draft").ToArrayAsync(ct);
        if (oldDrafts.Length > 0)
        {
            var oldIds = oldDrafts.Select(set => set.Id).ToArray();
            var oldRubrics = await db.SubjectRubrics.Where(item => oldIds.Contains(item.RubricSetId)).ToArrayAsync(ct);
            var rubricIds = oldRubrics.Select(item => item.Id).ToArray();
            db.SubjectRubricCriteria.RemoveRange(db.SubjectRubricCriteria.Where(item => rubricIds.Contains(item.SubjectRubricId)));
            db.SubjectRubrics.RemoveRange(oldRubrics);
            db.RubricSets.RemoveRange(oldDrafts);
        }
        var version = (await db.RubricSets.Where(set => set.SubjectId == subjectId)
            .MaxAsync(set => (int?)set.Version, ct) ?? 0) + 1;
        var draft = new RubricSet
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            Version = version,
            Status = "Draft",
            Source = source,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.RubricSets.Add(draft);
        foreach (var (rubricRequest, rubricIndex) in rubrics.Select((value, index) => (value, index)))
        {
            var rubric = new SubjectRubric
            {
                Id = Guid.NewGuid(),
                RubricSetId = draft.Id,
                Name = rubricRequest.Name.Trim(),
                Description = Normalize(rubricRequest.Description),
                WeightPercent = rubricRequest.WeightPercent,
                DisplayOrder = rubricIndex
            };
            db.SubjectRubrics.Add(rubric);
            db.SubjectRubricCriteria.AddRange(rubricRequest.Criteria.Select((criterion, index) =>
                new SubjectRubricCriterion
                {
                    Id = Guid.NewGuid(),
                    SubjectRubricId = rubric.Id,
                    Name = criterion.Name.Trim(),
                    Description = Normalize(criterion.Description),
                    MaxScore = criterion.MaxScore,
                    DisplayOrder = index
                }));
        }
        await db.SaveChangesAsync(ct);
        return draft;
    }

    // Retrieval step of RAG: the most relevant passages for the topic (or an even sample of the
    // whole syllabus), labelled S1..Sn, plus scanned PDFs attached whole and labelled D1..Dn.
    private async Task<(AiGroundingContext Grounding, Dictionary<string, CandidateSourceDto> Sources)> GroundingAsync(
        Guid subjectId, IReadOnlyCollection<StudyDocument> documents, string? topic, int limit, CancellationToken ct)
    {
        var indexedIds = documents.Where(document => document.IndexStatus == "Indexed")
            .Select(document => document.Id).ToArray();
        var rows = await db.StudyDocumentChunks.AsNoTracking()
            .Where(chunk => chunk.SubjectId == subjectId && indexedIds.Contains(chunk.DocumentId)).ToArrayAsync(ct);
        var chunks = rows.Select(row => new RetrievableChunk(row.Id, row.DocumentId, row.ChunkIndex,
            row.PageStart, row.PageEnd, row.Content, row.Embedding is null ? null : ToFloats(row.Embedding))).ToArray();
        float[]? queryVector = null;
        if (topic is not null && chunks.Any(chunk => chunk.Embedding is not null))
        {
            try { queryVector = (await embeddings.EmbedAsync([topic], query: true, ct))[0]; }
            catch (AiGenerationException) { /* keyword ranking still applies */ }
        }

        var names = documents.ToDictionary(document => document.Id, document => document.FileName);
        var selected = StudyMaterialRetriever.Retrieve(chunks, topic, queryVector, limit)
            .Select((chunk, index) => new AiSourceChunk($"S{index + 1}", names[chunk.DocumentId],
                chunk.PageStart, chunk.PageEnd, chunk.Text)).ToArray();
        var attachments = documents.Where(document => document.IndexStatus is "NoText" or "Failed")
            .Select(document => new AiSourceDocument(document.FileName, document.MimeType, document.Content)).ToArray();

        var sources = selected.ToDictionary(chunk => chunk.Label, chunk =>
            new CandidateSourceDto(chunk.FileName, chunk.PageStart, chunk.PageEnd, Truncate(chunk.Text, 280)));
        for (var i = 0; i < attachments.Length; i++)
            sources[$"D{i + 1}"] = new CandidateSourceDto(attachments[i].FileName, 0, 0,
                "Toàn bộ tài liệu (PDF không có lớp văn bản).");
        return (new AiGroundingContext(selected, attachments), sources);
    }

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] ToFloats(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, vector.Length * sizeof(float));
        return vector;
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)].TrimEnd() + "…";

    private async Task<Subject> OwnedSubjectAsync(Guid subjectId, Wf1Actor actor, CancellationToken ct)
    {
        EnsureActor(actor);
        var subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == subjectId, ct)
            ?? throw Missing("Subject was not found.");
        if (!actor.IsAdmin && subject.CreatedBy is not null && subject.CreatedBy != actor.Id)
            throw new Wf1Exception(Wf1Failure.Forbidden, "You cannot manage this subject.");
        return subject;
    }

    private async Task<AiQuestionCandidate> CandidateAsync(
        Guid subjectId, Guid candidateId, CancellationToken ct)
    {
        return await db.AiQuestionCandidates.SingleOrDefaultAsync(
            item => item.Id == candidateId && item.SubjectId == subjectId, ct)
            ?? throw Missing("AI question candidate was not found.");
    }

    private async Task<RubricSetDto?> ToRubricSetDtoAsync(RubricSet? set, CancellationToken ct)
    {
        if (set is null) return null;
        var rubrics = await db.SubjectRubrics.AsNoTracking().Where(item => item.RubricSetId == set.Id)
            .OrderBy(item => item.DisplayOrder).ToArrayAsync(ct);
        var ids = rubrics.Select(item => item.Id).ToArray();
        var criteria = await db.SubjectRubricCriteria.AsNoTracking().Where(item => ids.Contains(item.SubjectRubricId))
            .OrderBy(item => item.DisplayOrder).ToArrayAsync(ct);
        return new RubricSetDto(set.Id, set.Version, set.Status, set.Source, set.CreatedAt,
            rubrics.Select(rubric => new SubjectRubricDto(rubric.Id, rubric.Name, rubric.Description,
                rubric.WeightPercent, rubric.DisplayOrder,
                criteria.Where(criterion => criterion.SubjectRubricId == rubric.Id)
                    .Select(criterion => new SubjectRubricCriterionDto(criterion.Id, criterion.Name,
                        criterion.Description, criterion.MaxScore, criterion.DisplayOrder)).ToArray())).ToArray());
    }

    private static SubjectSummaryDto ToSummary(Subject subject, IEnumerable<QuestionBank> banks) =>
        new(subject.Id, subject.Name, subject.Description, subject.CreatedBy, subject.CreatedAt,
            banks.Select(bank => new SubjectBankDto(bank.Id, bank.Name, bank.Description)).ToArray());
    private static StudyDocumentDto ToDocumentDto(StudyDocument item, (int Count, bool Semantic) chunks) =>
        new(item.Id, item.FileName, item.MimeType, item.SizeBytes, item.CreatedAt, item.IndexStatus,
            item.PageCount, chunks.Count, chunks.Count > 0 && chunks.Semantic, item.IndexError);
    private static AiQuestionCandidateDto ToCandidateDto(AiQuestionCandidate item) => ToCandidateDto(item, null);
    private static AiQuestionCandidateDto ToCandidateDto(AiQuestionCandidate item, string? topic) =>
        new(item.Id, item.QuestionBankId, item.Content, item.BloomLevel, item.DifficultyLevel,
            item.Rationale, JsonSerializer.Deserialize<string[]>(item.ExpectedKeyPointsJson, JsonOptions) ?? [],
            item.Status, item.ApprovedQuestionId,
            JsonSerializer.Deserialize<CandidateSourceDto[]>(item.SourceRefsJson, JsonOptions) ?? [], topic);

    private static void ValidateCandidate(UpdateAiQuestionCandidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Trim().Length > 2000 ||
            request.BloomLevel is not ("Remember" or "Understand" or "Apply" or "Analyze") ||
            request.DifficultyLevel is < 1 or > 5 || request.ExpectedKeyPoints.Count is < 1 or > 5 ||
            request.ExpectedKeyPoints.Any(point => string.IsNullOrWhiteSpace(point) || point.Trim().Length > 2000))
            throw Invalid("Question candidate is invalid.", "invalid_candidate");
    }

    private static void ValidateRubrics(IReadOnlyCollection<SaveSubjectRubricRequest> rubrics, bool requireTotal)
    {
        if (rubrics.Count is < 1 or > 10 || rubrics.Any(rubric =>
                string.IsNullOrWhiteSpace(rubric.Name) || rubric.Name.Trim().Length > 120 ||
                rubric.Description?.Trim().Length > 500 || rubric.WeightPercent is <= 0 or > 100 ||
                rubric.Criteria.Count is < 1 or > 10 || rubric.Criteria.Any(criterion =>
                    string.IsNullOrWhiteSpace(criterion.Name) || criterion.Name.Trim().Length > 120 ||
                    criterion.Description?.Trim().Length > 500 || criterion.MaxScore <= 0)))
            throw Invalid("Rubric set is invalid.", "invalid_rubric_set");
        if (rubrics.Select(rubric => rubric.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rubrics.Count)
            throw Invalid("Rubric names must be unique.", "duplicate_rubric_name");
        if (requireTotal && rubrics.Sum(rubric => rubric.WeightPercent) != 100m)
            throw Invalid("Rubric percentages must total exactly 100.", "rubric_weight");
    }

    private static void EnsureActor(Wf1Actor actor)
    {
        if (actor.Id == Guid.Empty || actor.Role is not ("ADMIN" or "LECTURER"))
            throw new Wf1Exception(Wf1Failure.Forbidden, "Only lecturers and administrators can manage WF1.");
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Wf1Exception Missing(string message) => new(Wf1Failure.NotFound, message);
    private static Wf1Exception Invalid(string message, string code) => new(Wf1Failure.Invalid, message, code);
}
