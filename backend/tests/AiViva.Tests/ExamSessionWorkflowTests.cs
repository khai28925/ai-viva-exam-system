using AiViva.Application.ExamSessions;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Persistence.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiViva.Tests;

public sealed class ExamSessionWorkflowTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly QuestionBankDbContext _db;
    private readonly PostgresExamSessionService _service;
    private readonly ExamActor _lecturer;
    private readonly ExamActor _student;
    private readonly Guid _bankId;
    private readonly Question _question;

    public ExamSessionWorkflowTests()
    {
        _connection.Open();
        _connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        _db = new QuestionBankDbContext(new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        var lecturerRole = _db.Roles.Single(role => role.Code == Role.Codes.Lecturer);
        var studentRole = _db.Roles.Single(role => role.Code == Role.Codes.Student);
        var lecturer = new UserAccount("session-lecturer@example.com", lecturerRole.Id);
        var student = new UserAccount("session-student@example.com", studentRole.Id);
        var bank = new QuestionBank("Session bank", null);
        _question = new Question(bank.Id, "Original question");
        _db.Users.AddRange(lecturer, student);
        _db.QuestionBanks.Add(bank);
        _db.Questions.Add(_question);
        _db.SaveChanges();
        _bankId = bank.Id;
        _lecturer = new ExamActor(lecturer.Id, Role.Codes.Lecturer);
        _student = new ExamActor(student.Id, Role.Codes.Student);
        _service = new PostgresExamSessionService(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Create_requires_draft()
    {
        var result = await _service.CreateAsync(Request("Open"), _lecturer);
        result.Error.Should().Be(ExamSessionError.Invalid);
        result.ValidationErrors.Should().ContainKey("status");
    }

    [Fact]
    public async Task Selection_validates_duplicates_and_bank_membership()
    {
        var session = await CreateDraftAsync();
        var duplicate = await _service.ReplaceQuestionsAsync(session.Id,
            new([_question.Id, _question.Id]), _lecturer);
        duplicate.Error.Should().Be(ExamSessionError.Invalid);
        duplicate.ValidationErrors.Should().ContainKey("questionIds");

        var foreign = await _service.ReplaceQuestionsAsync(session.Id, new([Guid.NewGuid()]), _lecturer);
        foreign.Error.Should().Be(ExamSessionError.Invalid);
        (await _service.GetQuestionsAsync(session.Id, _lecturer)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_requires_selection_key_points_and_weighted_rubric()
    {
        var session = await CreateDraftAsync();
        var notReady = await _service.UpdateAsync(session.Id, Request("Open"), _lecturer);
        notReady.Code.Should().Be("session_not_ready");
        notReady.Missing.Should().Contain(["selection", "rubric"]);

        await _service.ReplaceQuestionsAsync(session.Id, new([_question.Id]), _lecturer);
        var withoutKeyPoint = await _service.UpdateAsync(session.Id, Request("Open"), _lecturer);
        withoutKeyPoint.Missing.Should().Contain("keyPoints");
        withoutKeyPoint.QuestionIdsWithoutKeyPoints.Should().Contain(_question.Id);

        _db.ExpectedKeyPoints.Add(new ExpectedKeyPoint
        {
            Id = Guid.NewGuid(),
            QuestionId = _question.Id,
            Content = "Expected answer",
            DisplayOrder = 0
        });
        var rubric = new Rubric { Id = Guid.NewGuid(), QuestionBankId = _bankId, Name = "Evaluation" };
        _db.Rubrics.Add(rubric);
        _db.RubricCriteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            Name = "Accuracy",
            MaxScore = 10,
            Weight = 0.5m
        });
        await _db.SaveChangesAsync();

        var wrongWeight = await _service.UpdateAsync(session.Id, Request("Open"), _lecturer);
        wrongWeight.Missing.Should().Contain("rubricWeight");

        _db.RubricCriteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            Name = "Clarity",
            MaxScore = 10,
            Weight = 0.5m
        });
        await _db.SaveChangesAsync();
        var published = await _service.UpdateAsync(session.Id, Request("Open"), _lecturer);
        published.Error.Should().Be(ExamSessionError.None);
        published.Value!.Status.Should().Be("Open");
    }

    [Fact]
    public async Task Join_returns_only_selected_snapshot_and_is_idempotent()
    {
        var session = await CreateDraftAsync();
        await _service.ReplaceQuestionsAsync(session.Id, new([_question.Id]), _lecturer);
        var selected = (await _service.GetQuestionsAsync(session.Id, _lecturer)).Value!;
        selected.Single().DisplayOrder.Should().Be(0);
        _db.ExpectedKeyPoints.Add(new ExpectedKeyPoint
        {
            Id = Guid.NewGuid(),
            QuestionId = _question.Id,
            Content = "Expected answer",
            DisplayOrder = 0
        });
        var rubric = new Rubric { Id = Guid.NewGuid(), QuestionBankId = _bankId, Name = "Evaluation" };
        _db.Rubrics.Add(rubric);
        _db.RubricCriteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            Name = "Accuracy",
            MaxScore = 10,
            Weight = 0.5m
        });
        _db.RubricCriteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            Name = "Clarity",
            MaxScore = 10,
            Weight = 0.5m
        });
        await _db.SaveChangesAsync();
        await _service.UpdateAsync(session.Id, Request("Open"), _lecturer);

        _question.UpdateContent("Changed after publish");
        _db.Questions.Add(new Question(_bankId, "Not selected"));
        await _db.SaveChangesAsync();
        var first = await _service.JoinAsync(session.Id, _student);
        var second = await _service.JoinAsync(session.Id, _student);
        first.Value!.Questions.Single().Content.Should().Be("Original question");
        first.Value.Questions.Should().HaveCount(1);
        second.Value!.Id.Should().Be(first.Value.Id);
        (await _db.ExamAttempts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Bank_change_with_selection_and_draft_delete_follow_contract()
    {
        var session = await CreateDraftAsync();
        await _service.ReplaceQuestionsAsync(session.Id, new([_question.Id]), _lecturer);
        var otherBank = new QuestionBank("Other", null);
        _db.QuestionBanks.Add(otherBank);
        await _db.SaveChangesAsync();

        var change = await _service.UpdateAsync(session.Id,
            new SaveExamSessionRequest("Session", otherBank.Id, "Draft"), _lecturer);
        change.Code.Should().Be("selection_not_empty");
        var deleted = await _service.DeleteAsync(session.Id, _lecturer);
        deleted.Value.Should().BeTrue();
        (await _db.ExamSessionQuestions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Legacy_open_session_does_not_expose_entire_question_bank()
    {
        var legacy = new ExamSession("Legacy", _bankId, ExamSession.Statuses.Open, _lecturer.Id);
        _db.ExamSessions.Add(legacy);
        await _db.SaveChangesAsync();

        var joined = await _service.JoinAsync(legacy.Id, _student);
        joined.Error.Should().Be(ExamSessionError.Conflict);
        joined.Code.Should().Be("legacy_session_unprepared");
        (await _db.ExamAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Published_session_cannot_change_selection_or_be_deleted()
    {
        var legacy = new ExamSession("Legacy", _bankId, ExamSession.Statuses.Open, _lecturer.Id);
        _db.ExamSessions.Add(legacy);
        await _db.SaveChangesAsync();

        var selection = await _service.ReplaceQuestionsAsync(legacy.Id, new([_question.Id]), _lecturer);
        selection.Code.Should().Be("selection_locked");
        var deletion = await _service.DeleteAsync(legacy.Id, _lecturer);
        deletion.Code.Should().Be("session_not_deletable");
        var backwards = await _service.UpdateAsync(legacy.Id, Request("Draft"), _lecturer);
        backwards.Code.Should().Be("invalid_status_transition");
    }

    [Fact]
    public async Task Closed_session_rejects_join_and_is_hidden_from_student_list()
    {
        var closed = new ExamSession("Closed", _bankId, ExamSession.Statuses.Closed, _lecturer.Id);
        _db.ExamSessions.Add(closed);
        await _db.SaveChangesAsync();

        (await _service.GetByIdAsync(closed.Id, _student)).Error.Should().Be(ExamSessionError.NotFound);
        var joined = await _service.JoinAsync(closed.Id, _student);
        joined.Error.Should().Be(ExamSessionError.Conflict);
        (await _db.ExamAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Other_lecturer_cannot_read_or_modify_selection()
    {
        var session = await CreateDraftAsync();
        var outsider = new ExamActor(Guid.NewGuid(), Role.Codes.Lecturer);

        (await _service.GetQuestionsAsync(session.Id, outsider)).Error.Should().Be(ExamSessionError.NotFound);
        (await _service.ReplaceQuestionsAsync(session.Id, new([_question.Id]), outsider))
            .Error.Should().Be(ExamSessionError.NotFound);
        (await _service.GetQuestionsAsync(session.Id, _student)).Error.Should().Be(ExamSessionError.Forbidden);
    }

    private SaveExamSessionRequest Request(string status) => new("Session", _bankId, status);

    private async Task<ExamSessionDto> CreateDraftAsync() =>
        (await _service.CreateAsync(Request("Draft"), _lecturer)).Value!;
}
