using System.IO.Compression;
using System.Xml.Linq;
using AiViva.Application.ExamSessions;
using AiViva.Application.Modules.Viva;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Accounts;
using AiViva.Infrastructure.Modules.QuestionPreparation;
using AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;
using AiViva.Infrastructure.Modules.Viva;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Persistence.Models;
using AiViva.ModuleContracts.QuestionPreparation;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiViva.Tests;

public sealed class InterviewWorkflowTests : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly QuestionBankDbContext db;
    private readonly FakeAi ai = new();
    private readonly TestClock clock = new();
    private readonly InterviewService service;
    private readonly ExamActor student;
    private readonly ExamActor lecturer;
    private readonly ExamSession session;
    private readonly ExamAttempt attempt;

    public InterviewWorkflowTests()
    {
        connection.Open();
        connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        db = new(new DbContextOptionsBuilder<QuestionBankDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var teacher = new UserAccount("teacher@aives.local", db.Roles.Single(r => r.Code == "LECTURER").Id);
        var pupil = new UserAccount("student@aives.local", db.Roles.Single(r => r.Code == "STUDENT").Id);
        pupil.UpdateProfile("Nguyễn Văn An", "se180001");
        db.Users.AddRange(teacher, pupil);
        student = new(pupil.Id, "STUDENT");
        lecturer = new(teacher.Id, "LECTURER");
        var subject = new Subject("Software architecture", null, teacher.Id);
        var bank = new QuestionBank("Question bank", null, teacher.Id);
        bank.AssignSubject(subject.Id);
        var question = new Question(bank.Id, "Explain dependency injection.");
        question.UpdateClassification("Understand", 2);
        db.Subjects.Add(subject);
        db.QuestionBanks.Add(bank);
        db.Questions.Add(question);
        var rubricSet = new RubricSet
        {
            Id = Guid.NewGuid(),
            SubjectId = subject.Id,
            Version = 1,
            Status = "Approved",
            Source = "Manual",
            CreatedBy = teacher.Id,
            CreatedAt = clock.GetUtcNow()
        };
        var rubric = new SubjectRubric { Id = Guid.NewGuid(), RubricSetId = rubricSet.Id, Name = "Accuracy", WeightPercent = 100 };
        db.RubricSets.Add(rubricSet);
        db.SubjectRubrics.Add(rubric);
        db.SubjectRubricCriteria.Add(new SubjectRubricCriterion
        {
            Id = Guid.NewGuid(),
            SubjectRubricId = rubric.Id,
            Name = "Core concepts",
            MaxScore = 10
        });
        db.ExpectedKeyPoints.Add(new ExpectedKeyPoint
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Content = "Dependency supplied from outside",
            DisplayOrder = 0,
            CreatedAt = clock.GetUtcNow()
        });
        session = new("=Demo viva", bank.Id, "Draft", teacher.Id);
        session.ConfigureInterview(30, 1, "vi-VN", ExamSession.AnswerModes.VoiceOrText);
        session.PinRubricSet(rubricSet.Id);
        session.UpdateDetails(session.Title, bank.Id, "Open");
        db.ExamSessions.Add(session);
        db.ExamSessionQuestions.Add(new ExamSessionQuestion
        {
            Id = Guid.NewGuid(),
            ExamSessionId = session.Id,
            QuestionBankId = bank.Id,
            QuestionId = question.Id,
            ContentSnapshot = question.Content,
            DisplayOrder = 0
        });
        attempt = new ExamAttempt(session.Id, pupil.Id);
        db.ExamAttempts.Add(attempt);
        db.SaveChanges();
        service = new(db, ai, new PostgresQuestionPreparationReader(db),
            new AccountService(db, new PasswordHasher<UserAccount>()), clock);
    }

    [Fact]
    public async Task Interview_followup_report_finalization_statistics_and_export_work_together()
    {
        var start = await service.StartAsync(attempt.Id, student);
        start.CurrentQuestion!.Source.Should().Be("Bank");
        var mainId = start.CurrentQuestion.Id;
        var follow = await service.SubmitAsync(attempt.Id, new(mainId, "It supplies dependencies."), student);
        follow.CurrentQuestion!.Source.Should().Be("AiFollowUp");
        follow.CurrentQuestion.FollowUpNumber.Should().Be(1);
        var duplicate = await service.SubmitAsync(attempt.Id, new(mainId, "Duplicate request"), student);
        duplicate.CurrentQuestion!.Id.Should().Be(follow.CurrentQuestion.Id);
        ai.Calls.Should().Be(1);
        var completed = await service.SubmitAsync(attempt.Id, new(follow.CurrentQuestion.Id, "The container resolves interfaces."), student);
        completed.Status.Should().Be("Completed");
        completed.CurrentQuestion.Should().BeNull();
        (await db.Answers.CountAsync()).Should().Be(2);
        (await db.AttemptQuestions.CountAsync(q => q.Source == "AiFollowUp")).Should().Be(1);
        ai.LastAllowFollowUp.Should().BeFalse();
        ai.LastTurns.Should().HaveCount(2);

        var provisional = await service.GetReportAsync(attempt.Id, student);
        provisional.AiScore.Should().Be(5);
        provisional.FinalScore.Should().BeNull();
        provisional.Questions.Should().ContainSingle();
        var probe = provisional.Questions[0].FollowUps.Should().ContainSingle().Subject;
        probe.ProbedQuote.Should().Be("supplies dependencies", "the report shows which student words the follow-up questioned");
        probe.ProbeType.Should().Be("justify");
        var final = await service.FinalizeAsync(attempt.Id, new([new(mainId, 8.25m)], "Good explanation"), lecturer);
        final.FinalScore.Should().Be(8.25m);
        final.IsFinalized.Should().BeTrue();
        (await service.GetReportAsync(attempt.Id, student)).FinalScore.Should().Be(8.25m);
        var statistics = await service.GetStatisticsAsync(session.Id, lecturer);
        statistics.AverageScore.Should().Be(8.25m);
        statistics.Questions.Single().GoodAnswerRate.Should().Be(100);
        statistics.Distribution.Sum(b => b.Count).Should().Be(1);
        var csv = GradeSheetExport.ToCsv(await service.GetGradeSheetAsync(session.Id, lecturer));
        csv.Should().Contain("student@aives.local").And.Contain("8.25").And.Contain("'=Demo viva");
    }

    [Fact]
    public async Task Report_shows_lecturer_marks_durations_bloom_and_student_identity()
    {
        var start = await service.StartAsync(attempt.Id, student);
        clock.Advance(12);
        var follow = await service.SubmitAsync(attempt.Id, new(start.CurrentQuestion!.Id, "It supplies dependencies."), student);
        clock.Advance(7);
        await service.SubmitAsync(attempt.Id, new(follow.CurrentQuestion!.Id, "Through constructors."), student);

        var report = await service.GetReportAsync(attempt.Id, student);
        report.StudentName.Should().Be("Nguyễn Văn An");
        report.StudentCode.Should().Be("SE180001");
        report.TotalDurationSeconds.Should().Be(19);
        var answer = report.Questions.Single();
        answer.DurationSeconds.Should().Be(12);
        answer.FollowUps.Single().DurationSeconds.Should().Be(7);
        answer.BloomLevel.Should().Be("Understand");
        answer.FinalScore.Should().BeNull();

        var final = await service.FinalizeAsync(attempt.Id, new([new(start.CurrentQuestion.Id, 9)], "Rõ ràng"), lecturer);
        final.Questions.Single().FinalScore.Should().Be(9);
        (await service.GetReportAsync(attempt.Id, student)).Questions.Single().FinalScore.Should().Be(9);
        (await service.ListReportsAsync(lecturer)).Single().StudentCode.Should().Be("SE180001");
    }

    [Fact]
    public async Task Statistics_cover_bloom_levels_missing_points_and_finalization()
    {
        var start = await service.StartAsync(attempt.Id, student);
        clock.Advance(10);
        var follow = await service.SubmitAsync(attempt.Id, new(start.CurrentQuestion!.Id, "It supplies dependencies."), student);
        await service.SubmitAsync(attempt.Id, new(follow.CurrentQuestion!.Id, "Through constructors."), student);

        var provisional = await service.GetStatisticsAsync(session.Id, lecturer);
        provisional.Finalized.Should().Be(0);
        provisional.GoodAnswerRate.Should().Be(0);
        provisional.Questions.Single().CommonMissingKeyPoints.Should().Equal("Practical example");
        provisional.Questions.Single().AverageDurationSeconds.Should().Be(10);
        provisional.BloomLevels.Should().ContainSingle(level => level.BloomLevel == "Understand" && level.AverageScore == 5);

        await service.FinalizeAsync(attempt.Id, new([new(start.CurrentQuestion.Id, 8)], null), lecturer);
        var final = await service.GetStatisticsAsync(session.Id, lecturer);
        final.Finalized.Should().Be(1);
        final.GoodAnswerRate.Should().Be(100);
        final.HighestScore.Should().Be(8);
        final.LowestScore.Should().Be(8);
    }

    [Fact]
    public async Task Grade_sheet_lists_student_code_per_question_marks_and_opens_as_xlsx()
    {
        var start = await service.StartAsync(attempt.Id, student);
        var follow = await service.SubmitAsync(attempt.Id, new(start.CurrentQuestion!.Id, "It supplies dependencies."), student);
        await service.SubmitAsync(attempt.Id, new(follow.CurrentQuestion!.Id, "Through constructors."), student);

        var pending = await service.GetGradeSheetAsync(session.Id, lecturer);
        pending.Rows.Single().Status.Should().Be("Chờ giảng viên chốt");
        pending.Rows.Single().QuestionScores.Should().ContainSingle().Which.Should().BeNull();

        await service.FinalizeAsync(attempt.Id, new([new(start.CurrentQuestion.Id, 8.5m)], "Tốt"), lecturer);
        var sheet = await service.GetGradeSheetAsync(session.Id, lecturer);
        sheet.SubjectName.Should().Be("Software architecture");
        sheet.QuestionCount.Should().Be(1);
        var row = sheet.Rows.Single();
        row.No.Should().Be(1);
        row.StudentCode.Should().Be("SE180001");
        row.FullName.Should().Be("Nguyễn Văn An");
        row.QuestionScores.Should().Equal(8.5m);
        row.FinalScore.Should().Be(8.5m);
        row.Status.Should().Be("Đã chốt");

        using var zip = new ZipArchive(new MemoryStream(GradeSheetExport.ToXlsx(sheet)), ZipArchiveMode.Read);
        zip.GetEntry("[Content_Types].xml").Should().NotBeNull();
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var worksheet = XDocument.Parse(await reader.ReadToEndAsync());
        var cells = worksheet.Descendants().Where(e => e.Name.LocalName is "t" or "v").Select(e => e.Value).ToArray();
        cells.Should().Contain(["BẢNG ĐIỂM THI VẤN ĐÁP", "SE180001", "Nguyễn Văn An", "Câu 1", "8.5", "Đã chốt"]);
        cells.Should().Contain(text => text.Contains("=Demo viva"));
    }

    [Fact]
    public async Task Voice_only_session_grades_the_server_transcript_never_typed_text()
    {
        session.UpdateDetails(session.Title, session.QuestionBankId, "Draft");
        session.ConfigureInterview(30, 1, "vi-VN", ExamSession.AnswerModes.Voice);
        session.UpdateDetails(session.Title, session.QuestionBankId, "Open");
        await db.SaveChangesAsync();

        var start = await service.StartAsync(attempt.Id, student);
        start.AnswerMode.Should().Be("Voice");
        var transcript = await service.TranscribeAsync(attempt.Id, start.CurrentQuestion!.Id, [1, 2, 3], "audio/webm", student);
        ai.LastTranscriptionContext.Should().Be("Explain dependency injection.");
        var follow = await service.SubmitAsync(attempt.Id, new(start.CurrentQuestion.Id, "Pasted textbook answer"), student);
        (await db.Answers.SingleAsync()).TextContent.Should().Be(transcript.Text);

        // Typed text without a recording is treated as no answer at all.
        await service.SubmitAsync(attempt.Id, new(follow.CurrentQuestion!.Id, "Typed instead of spoken"), student);
        (await db.Answers.SingleAsync(a => a.AttemptQuestionId == follow.CurrentQuestion.Id)).TextContent.Should().BeEmpty();
        (await db.ServiceInteractionLogs.CountAsync(log => log.ServiceType == "STT")).Should().Be(1);
    }

    [Fact]
    public async Task Resume_keeps_deadline_and_server_rejects_late_answer_content()
    {
        var start = await service.StartAsync(attempt.Id, student);
        clock.Advance(20);
        var resumed = await service.StartAsync(attempt.Id, student);
        resumed.CurrentQuestion!.Deadline.Should().Be(start.CurrentQuestion!.Deadline);
        clock.Advance(20);
        var completed = await service.SubmitAsync(attempt.Id, new(start.CurrentQuestion.Id, "Late forged answer"), student);
        completed.Status.Should().Be("Completed");
        ai.Calls.Should().Be(0);
        (await db.Answers.SingleAsync()).TextContent.Should().BeEmpty();
        (await service.GetReportAsync(attempt.Id, student)).AiScore.Should().Be(0);
    }

    [Fact]
    public async Task Provider_failure_preserves_original_on_time_answer_for_retry()
    {
        var start = await service.StartAsync(attempt.Id, student);
        ai.FailNext = true;
        var send = () => service.SubmitAsync(attempt.Id, new(start.CurrentQuestion!.Id, "Original answer"), student);
        await send.Should().ThrowAsync<InterviewException>();
        (await db.Answers.CountAsync()).Should().Be(0);
        clock.Advance(300);
        await service.SubmitAsync(attempt.Id, new(start.CurrentQuestion!.Id, "Changed after deadline"), student);
        (await db.Answers.SingleAsync()).TextContent.Should().Be("Original answer");
    }

    [Fact]
    public async Task Student_cannot_read_other_attempt_finalize_or_export()
    {
        var stranger = new ExamActor(Guid.NewGuid(), "STUDENT");
        var read = () => service.StartAsync(attempt.Id, stranger);
        (await read.Should().ThrowAsync<InterviewException>()).Which.Status.Should().Be(404);
        var export = () => service.GetGradeSheetAsync(session.Id, student);
        (await export.Should().ThrowAsync<InterviewException>()).Which.Status.Should().Be(404);
        var finalize = () => service.FinalizeAsync(attempt.Id, new([], null), student);
        (await finalize.Should().ThrowAsync<InterviewException>()).Which.Status.Should().Be(404);
        var otherTeacher = () => service.GetStatisticsAsync(session.Id, new(Guid.NewGuid(), "LECTURER"));
        (await otherTeacher.Should().ThrowAsync<InterviewException>()).Which.Status.Should().Be(404);
    }

    [Fact]
    public async Task Settings_are_locked_after_publish()
    {
        var save = () => service.SaveSettingsAsync(session.Id, new(300, 5, "en-US"), lecturer);
        (await save.Should().ThrowAsync<InterviewException>()).Which.Status.Should().Be(409);
    }

    [Fact]
    public async Task Zero_followups_is_persisted_instead_of_being_replaced_with_database_default()
    {
        var configured = new ExamSession("No followups", session.QuestionBankId, "Draft", lecturer.Id);
        configured.ConfigureInterview(30, 0, "vi-VN", ExamSession.AnswerModes.Voice);
        db.ExamSessions.Add(configured);
        await db.SaveChangesAsync();
        await db.Entry(configured).ReloadAsync();
        configured.MaxFollowUps.Should().Be(0);
    }

    [Fact]
    public async Task Closing_enrollment_does_not_prevent_an_existing_student_from_finishing()
    {
        var start = await service.StartAsync(attempt.Id, student);
        session.UpdateDetails(session.Title, session.QuestionBankId, "Closed");
        await db.SaveChangesAsync();
        var resumed = await service.StartAsync(attempt.Id, student);
        resumed.CurrentQuestion!.Id.Should().Be(start.CurrentQuestion!.Id);
        var completed = await service.SubmitAsync(attempt.Id, new(start.CurrentQuestion.Id, ""), student);
        completed.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Answer_receipt_survives_a_cancelled_ai_request()
    {
        var start = await service.StartAsync(attempt.Id, student);
        ai.CancelNext = true;
        var submit = () => service.SubmitAsync(attempt.Id, new(start.CurrentQuestion!.Id, "On time answer"), student);
        await submit.Should().ThrowAsync<OperationCanceledException>();
        clock.Advance(300);
        await service.SubmitAsync(attempt.Id, new(start.CurrentQuestion!.Id, "Retry text"), student);
        (await db.Answers.SingleAsync()).TextContent.Should().Be("On time answer");
    }

    [Fact]
    public async Task Uploaded_audio_keeps_its_receipt_time_while_transcription_is_processing()
    {
        var start = await service.StartAsync(attempt.Id, student);
        clock.Advance(25);
        ai.TranscriptionCompleted = () => clock.Advance(60);
        var transcript = await service.TranscribeAsync(attempt.Id, start.CurrentQuestion!.Id, [1, 2, 3], "audio/webm", student);
        await service.SubmitAsync(attempt.Id, new(start.CurrentQuestion.Id, transcript.Text), student);
        (await db.Answers.SingleAsync()).TextContent.Should().Be("Spoken answer");
    }

    [Fact]
    public void Rubric_weighting_is_computed_by_backend_and_invalid_marks_are_rejected()
    {
        var c1 = new AssessmentCriterion(Guid.NewGuid(), "Accuracy", null, 10);
        var c2 = new AssessmentCriterion(Guid.NewGuid(), "Clarity", null, 5);
        var context = new AssessmentContext([], [new(Guid.NewGuid(), "Knowledge", 60, [c1]), new(Guid.NewGuid(), "Delivery", 40, [c2])]);
        var assessment = new InterviewAssessment([new(c1.Id, 6, ""), new(c2.Id, 5, "")], "", [], [], null);
        InterviewScoring.Calculate(context, assessment).Should().Be(7.6m);
        var invalid = () => InterviewScoring.Calculate(context, assessment with { Criteria = [new(c1.Id, 100, ""), new(c2.Id, 5, "")] });
        invalid.Should().Throw<InterviewException>();
    }

    [Fact]
    public void Criteria_about_other_topics_are_left_out_of_the_score()
    {
        // A subject rubric shared by all questions: a perfect answer about microservices must not
        // lose marks for the SOLID and MVC criteria that the question never asked about.
        var microservices = new AssessmentCriterion(Guid.NewGuid(), "Microservices", null, 5);
        var mvc = new AssessmentCriterion(Guid.NewGuid(), "MVC", null, 5);
        var solid = new AssessmentCriterion(Guid.NewGuid(), "SOLID", null, 5);
        var context = new AssessmentContext([], [
            new(Guid.NewGuid(), "Architecture styles", 40, [mvc, microservices]),
            new(Guid.NewGuid(), "Design principles", 60, [solid])]);
        var assessment = new InterviewAssessment([
            new(microservices.Id, 5, "Correct"), new(mvc.Id, 0, "Off topic", false), new(solid.Id, 0, "Off topic", false)],
            "", [], [], null);

        CriterionMark[] partlyApplicable = [new(microservices.Id, 5, ""), new(mvc.Id, 0, "", false), new(solid.Id, 3, "")];
        CriterionMark[] noneApplicable =
            [new(microservices.Id, 5, "", false), new(mvc.Id, 5, "", false), new(solid.Id, 0, "", false)];

        InterviewScoring.Calculate(context, assessment).Should().Be(10m);
        InterviewScoring.Calculate(context, assessment with { Criteria = partlyApplicable })
            .Should().Be(7.6m, "each rubric is averaged over its applicable criteria only");
        InterviewScoring.Calculate(context, assessment with { Criteria = noneApplicable })
            .Should().Be(4m, "when nothing is marked applicable every criterion counts");
    }

    [Fact]
    public void Assessments_stored_before_the_applicable_flag_count_every_criterion()
    {
        var mark = System.Text.Json.JsonSerializer.Deserialize<CriterionMark>(
            $$"""{"criterionId":"{{Guid.NewGuid()}}","score":3,"feedback":"ok"}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        mark!.Applicable.Should().BeTrue();
    }

    public async ValueTask DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(int seconds) => now = now.AddSeconds(seconds);
    }

    private sealed class FakeAi : IInterviewAi
    {
        public int Calls { get; private set; }
        public bool FailNext { get; set; }
        public bool CancelNext { get; set; }
        public Action? TranscriptionCompleted { get; set; }
        public bool LastAllowFollowUp { get; private set; }
        public string? LastTranscriptionContext { get; private set; }
        public IReadOnlyList<InterviewTurn> LastTurns { get; private set; } = [];
        public Task<string> TranscribeAsync(byte[] audio, string mimeType, string language, string? questionContext = null,
            CancellationToken ct = default)
        {
            LastTranscriptionContext = questionContext;
            TranscriptionCompleted?.Invoke();
            return Task.FromResult("Spoken answer");
        }
        public Task<InterviewAssessment> AssessAsync(AssessmentContext context, IReadOnlyList<InterviewTurn> turns,
            bool allowFollowUp, string language, CancellationToken ct = default)
        {
            Calls++;
            if (CancelNext) { CancelNext = false; throw new OperationCanceledException(); }
            if (FailNext) { FailNext = false; throw new InterviewException(503, "Provider unavailable"); }
            LastAllowFollowUp = allowFollowUp;
            LastTurns = turns;
            return Task.FromResult(new InterviewAssessment(context.Rubrics.SelectMany(r => r.Criteria)
                .Select(c => new CriterionMark(c.Id, c.MaxScore / 2, "Partially correct")).ToArray(), "Explain more precisely.",
                ["Recognizes dependencies"], ["Practical example"],
                allowFollowUp ? "You said 'supplies dependencies'. How does this reduce coupling?" : null,
                allowFollowUp ? "supplies dependencies" : null, allowFollowUp ? "justify" : null));
        }
    }
}
