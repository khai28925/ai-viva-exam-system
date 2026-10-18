using System.ComponentModel.DataAnnotations;
using AiViva.Application.ExamSessions;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiViva.Infrastructure.Persistence;

public sealed class PostgresExamSessionService(QuestionBankDbContext db) : IExamSessionService
{
    public async Task<IReadOnlyCollection<ExamSessionDto>> GetAllAsync(ExamActor actor, CancellationToken ct = default)
    {
        var sessions = VisibleSessions(actor).OrderByDescending(session => session.CreatedAt)
            .ThenBy(session => session.Id);
        return await Project(sessions).ToArrayAsync(ct);
    }

    public async Task<ExamSessionResult<ExamSessionDto>> GetByIdAsync(Guid id, ExamActor actor, CancellationToken ct = default)
    {
        var session = await Project(VisibleSessions(actor).Where(session => session.Id == id)).SingleOrDefaultAsync(ct);
        return session is null ? Missing<ExamSessionDto>() : new(session);
    }

    public async Task<ExamSessionResult<ExamSessionDto>> CreateAsync(
        SaveExamSessionRequest request, ExamActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManage) return Forbidden<ExamSessionDto>();
        var validation = Validate(request);
        if (validation is not null) return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Invalid, validation);
        if (request.Status != ExamSession.Statuses.Draft)
            return ExamSessionResult<ExamSessionDto>.Invalid("status", "A new session must be Draft.");
        var bankError = await CheckBankAsync(request, ct);
        if (bankError is not null) return bankError;

        var session = new ExamSession(request.Title, request.QuestionBankId, request.Status, actor.Id);
        db.ExamSessions.Add(session);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Conflict, "The question bank or creator no longer exists. Refresh and try again.");
        }
        return new(await Project(db.ExamSessions.Where(item => item.Id == session.Id)).SingleAsync(ct));
    }

    public async Task<ExamSessionResult<ExamSessionDto>> UpdateAsync(
        Guid id, SaveExamSessionRequest request, ExamActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManage) return Forbidden<ExamSessionDto>();
        var validation = Validate(request);
        if (validation is not null) return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Invalid, validation);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await LockSessionAsync(id, ct);
        if (session is null || !actor.CanManageSession(session.CreatedBy)) return Missing<ExamSessionDto>();
        if (session.Title == request.Title && session.QuestionBankId == request.QuestionBankId &&
            session.Status == request.Status)
            return new(await Project(db.ExamSessions.Where(item => item.Id == id)).SingleAsync(ct));
        if (session.Status == ExamSession.Statuses.Draft && request.Status == ExamSession.Statuses.Draft)
        {
            if (session.QuestionBankId != request.QuestionBankId &&
                await db.ExamSessionQuestions.AnyAsync(item => item.ExamSessionId == id, ct))
                return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Conflict,
                    "Remove selected questions before changing the question bank.", "selection_not_empty");
            var bankError = await CheckBankAsync(request, ct);
            if (bankError is not null) return bankError;
        }
        else if (session.Status == ExamSession.Statuses.Draft && request.Status == ExamSession.Statuses.Open &&
                 session.QuestionBankId == request.QuestionBankId)
        {
            var readiness = await CheckReadinessAsync(id, session.QuestionBankId, ct);
            if (readiness is not null) return readiness;
        }
        else if (session.Status == ExamSession.Statuses.Open && request.Status == ExamSession.Statuses.Closed &&
                 session.QuestionBankId == request.QuestionBankId && session.Title == request.Title)
        {
            // Closing does not change the published exam definition.
        }
        else if (session.Status != request.Status || session.QuestionBankId != request.QuestionBankId || session.Title != request.Title)
            return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Conflict,
                "This status transition or edit is not allowed.", "invalid_status_transition");

        session.UpdateDetails(request.Title, request.QuestionBankId, request.Status);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Conflict, "The question bank no longer exists. Refresh and try again.");
        }
        var dto = await Project(db.ExamSessions.Where(item => item.Id == id)).SingleAsync(ct);
        await transaction.CommitAsync(ct);
        return new(dto);
    }

    public async Task<ExamSessionResult<IReadOnlyCollection<SelectedExamQuestionDto>>> GetQuestionsAsync(
        Guid id, ExamActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManage) return Forbidden<IReadOnlyCollection<SelectedExamQuestionDto>>();
        var session = await db.ExamSessions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (session is null || !actor.CanManageSession(session.CreatedBy))
            return Missing<IReadOnlyCollection<SelectedExamQuestionDto>>();
        return new(await SelectedQuestions(id).ToArrayAsync(ct));
    }

    public async Task<ExamSessionResult<IReadOnlyCollection<SelectedExamQuestionDto>>> ReplaceQuestionsAsync(
        Guid id, SaveExamQuestionSelectionRequest request, ExamActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManage) return Forbidden<IReadOnlyCollection<SelectedExamQuestionDto>>();
        if (request.QuestionIds is null || request.QuestionIds.Count > 50 ||
            request.QuestionIds.Any(questionId => questionId == Guid.Empty) ||
            request.QuestionIds.Distinct().Count() != request.QuestionIds.Count)
            return ExamSessionResult<IReadOnlyCollection<SelectedExamQuestionDto>>.Invalid("questionIds",
                "Provide 0 to 50 distinct, non-empty question IDs.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await LockSessionAsync(id, ct);
        if (session is null || !actor.CanManageSession(session.CreatedBy))
            return Missing<IReadOnlyCollection<SelectedExamQuestionDto>>();
        if (session.Status != ExamSession.Statuses.Draft)
            return ExamSessionResult<IReadOnlyCollection<SelectedExamQuestionDto>>.Fail(
                ExamSessionError.Conflict, "Only a Draft session can change its questions.", "selection_locked");

        var requested = request.QuestionIds.ToArray();
        var questions = await db.Questions.AsNoTracking()
            .Where(question => requested.Contains(question.Id) &&
                question.QuestionBankId == session.QuestionBankId && question.QuestionType == "Bank")
            .Select(question => new { question.Id, question.Content }).ToArrayAsync(ct);
        if (questions.Length != requested.Length)
            return ExamSessionResult<IReadOnlyCollection<SelectedExamQuestionDto>>.Invalid("questionIds",
                "Every selected question must be a Bank question in this session's question bank.");

        await db.ExamSessionQuestions.Where(item => item.ExamSessionId == id).ExecuteDeleteAsync(ct);
        DetachSelectedQuestions(id);
        var contentById = questions.ToDictionary(question => question.Id, question => question.Content);
        for (var index = 0; index < requested.Length; index++)
            db.ExamSessionQuestions.Add(new ExamSessionQuestion
            {
                Id = Guid.NewGuid(),
                ExamSessionId = id,
                QuestionBankId = session.QuestionBankId,
                QuestionId = requested[index],
                ContentSnapshot = contentById[requested[index]],
                DisplayOrder = index,
                IsRequired = true
            });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            return ExamSessionResult<IReadOnlyCollection<SelectedExamQuestionDto>>.Invalid("questionIds",
                "A selected question no longer exists. Refresh and try again.");
        }
        await transaction.CommitAsync(ct);
        return new(await SelectedQuestions(id).ToArrayAsync(ct));
    }

    public async Task<ExamSessionResult<bool>> DeleteAsync(Guid id, ExamActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManage) return Forbidden<bool>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await LockSessionAsync(id, ct);
        if (session is null || !actor.CanManageSession(session.CreatedBy)) return Missing<bool>();
        if (session.Status != ExamSession.Statuses.Draft ||
            await db.ExamAttempts.AnyAsync(attempt => attempt.ExamSessionId == id, ct))
            return ExamSessionResult<bool>.Fail(ExamSessionError.Conflict,
                "Only an unused Draft session can be deleted.", "session_not_deletable");
        await db.ExamSessionQuestions.Where(item => item.ExamSessionId == id).ExecuteDeleteAsync(ct);
        DetachSelectedQuestions(id);
        db.ExamSessions.Remove(session);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(true);
    }

    public async Task<ExamSessionResult<ExamAttemptDto>> JoinAsync(Guid id, ExamActor actor, CancellationToken ct = default)
    {
        if (!actor.CanJoin) return Forbidden<ExamAttemptDto>();
        // Lock the session for joins, updates and deletion: two clicks cannot create two attempts,
        // and a join cannot race a close/delete or a change to the question bank.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await LockSessionAsync(id, ct);
        if (session is null) return Missing<ExamAttemptDto>();
        if (session.Status != ExamSession.Statuses.Open)
            return ExamSessionResult<ExamAttemptDto>.Fail(ExamSessionError.Conflict, "This exam session is not open.");
        var questions = await db.ExamSessionQuestions.AsNoTracking()
            .Where(question => question.ExamSessionId == id)
            .OrderBy(question => question.DisplayOrder)
            .Select(question => new ExamQuestionDto(question.QuestionId, question.ContentSnapshot)).ToArrayAsync(ct);
        if (questions.Length == 0)
            return ExamSessionResult<ExamAttemptDto>.Fail(ExamSessionError.Conflict,
                "This published session has no selected questions.", "legacy_session_unprepared");
        var attempt = await db.ExamAttempts.SingleOrDefaultAsync(
            item => item.ExamSessionId == id && item.StudentId == actor.Id, ct);
        if (attempt is null)
        {
            attempt = new ExamAttempt(id, actor.Id);
            db.ExamAttempts.Add(attempt);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return new(new ExamAttemptDto(attempt.Id, id, actor.Id, attempt.StartedAt, questions));
    }

    private IQueryable<ExamSession> VisibleSessions(ExamActor actor)
    {
        var query = db.ExamSessions.AsNoTracking();
        if (actor.Id == Guid.Empty) return query.Where(_ => false);
        return actor.Role switch
        {
            Role.Codes.Admin => query,
            Role.Codes.Lecturer => query.Where(session => session.CreatedBy == actor.Id),
            Role.Codes.Student => query.Where(session => session.Status == ExamSession.Statuses.Open),
            _ => query.Where(_ => false)
        };
    }

    private IQueryable<ExamSessionDto> Project(IQueryable<ExamSession> sessions) =>
        from session in sessions
        join bank in db.QuestionBanks on session.QuestionBankId equals bank.Id
        select new ExamSessionDto(session.Id, session.Title, session.QuestionBankId, bank.Name,
            session.Status, session.CreatedBy, session.CreatedAt,
            db.ExamAttempts.Count(attempt => attempt.ExamSessionId == session.Id));

    private Task<ExamSession?> LockSessionAsync(Guid id, CancellationToken ct) =>
        db.Database.ProviderName?.Contains("Sqlite", StringComparison.Ordinal) == true
            ? db.ExamSessions.SingleOrDefaultAsync(item => item.Id == id, ct)
            : db.ExamSessions.FromSqlInterpolated($"SELECT * FROM exam_sessions WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);

    private async Task<ExamSessionResult<ExamSessionDto>?> CheckBankAsync(SaveExamSessionRequest request, CancellationToken ct)
    {
        if (!await db.QuestionBanks.AnyAsync(bank => bank.Id == request.QuestionBankId, ct))
            return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.NotFound, "Question bank was not found.");
        return null;
    }

    private IQueryable<SelectedExamQuestionDto> SelectedQuestions(Guid id) =>
        db.ExamSessionQuestions.AsNoTracking().Where(item => item.ExamSessionId == id)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => new SelectedExamQuestionDto(item.QuestionId, item.ContentSnapshot,
                item.DisplayOrder, item.IsRequired));

    private void DetachSelectedQuestions(Guid sessionId)
    {
        foreach (var entry in db.ChangeTracker.Entries<ExamSessionQuestion>()
                     .Where(entry => entry.Entity.ExamSessionId == sessionId))
            entry.State = EntityState.Detached;
    }

    private async Task<ExamSessionResult<ExamSessionDto>?> CheckReadinessAsync(
        Guid sessionId, Guid bankId, CancellationToken ct)
    {
        var missing = new List<string>();
        var selectedIds = await db.ExamSessionQuestions.AsNoTracking()
            .Where(item => item.ExamSessionId == sessionId)
            .OrderBy(item => item.DisplayOrder).Select(item => item.QuestionId).ToArrayAsync(ct);
        if (selectedIds.Length == 0) missing.Add("selection");

        var validIds = await db.Questions.AsNoTracking()
            .Where(item => selectedIds.Contains(item.Id) && item.QuestionBankId == bankId && item.QuestionType == "Bank")
            .Select(item => item.Id).ToArrayAsync(ct);
        var withKeyPoints = await db.ExpectedKeyPoints.AsNoTracking()
            .Where(item => selectedIds.Contains(item.QuestionId))
            .Select(item => item.QuestionId).Distinct().ToArrayAsync(ct);
        var withoutKeyPoints = selectedIds.Except(withKeyPoints).ToArray();
        if (withoutKeyPoints.Length > 0) missing.Add("keyPoints");
        if (validIds.Length != selectedIds.Length && !missing.Contains("selection")) missing.Add("selection");

        var rubric = await db.Rubrics.AsNoTracking().SingleOrDefaultAsync(item => item.QuestionBankId == bankId, ct);
        if (rubric is null) missing.Add("rubric");
        else
        {
            var criteria = await db.RubricCriteria.AsNoTracking()
                .Where(item => item.RubricId == rubric.Id).Select(item => item.Weight).ToArrayAsync(ct);
            if (criteria.Length == 0) missing.Add("rubricCriteria");
            else if (criteria.Sum() != 1.0000m) missing.Add("rubricWeight");
        }
        return missing.Count == 0 ? null : ExamSessionResult<ExamSessionDto>.Fail(
            ExamSessionError.Conflict, "The session is not ready to publish.", "session_not_ready",
            missing, withoutKeyPoints);
    }

    private static string? Validate(SaveExamSessionRequest request)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(request, new ValidationContext(request), results, true)
            ? null : string.Join(" ", results.Select(result => result.ErrorMessage));
    }

    private static bool IsForeignKeyViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };

    private static ExamSessionResult<T> Missing<T>() => ExamSessionResult<T>.Fail(ExamSessionError.NotFound, "Exam session was not found.");
    private static ExamSessionResult<T> Forbidden<T>() => ExamSessionResult<T>.Fail(ExamSessionError.Forbidden, "This role cannot perform that action.");
}
