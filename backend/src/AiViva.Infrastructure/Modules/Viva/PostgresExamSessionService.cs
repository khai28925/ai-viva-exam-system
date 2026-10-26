using System.ComponentModel.DataAnnotations;
using AiViva.Application.ExamSessions;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence.Models;
using AiViva.ModuleContracts.QuestionPreparation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiViva.Infrastructure.Persistence;

public sealed class PostgresExamSessionService(
    QuestionBankDbContext db, IQuestionPreparationReader questionPreparation) : IExamSessionService
{
    public async Task<IReadOnlyCollection<ExamSessionDto>> GetAllAsync(ExamActor actor, CancellationToken ct = default)
    {
        var sessions = VisibleSessions(actor).OrderByDescending(session => session.CreatedAt)
            .ThenBy(session => session.Id);
        return await ProjectAsync(sessions, ct);
    }

    public async Task<ExamSessionResult<ExamSessionDto>> GetByIdAsync(Guid id, ExamActor actor, CancellationToken ct = default)
    {
        var session = (await ProjectAsync(VisibleSessions(actor).Where(session => session.Id == id), ct))
            .SingleOrDefault();
        return session is null ? Missing<ExamSessionDto>() : new(session);
    }

    public async Task<ExamSessionResult<ExamSessionDto>> CreateAsync(
        SaveExamSessionRequest request, ExamActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManage) return Forbidden<ExamSessionDto>();
        var validation = Validate(request);
        if (validation is not null) return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Invalid, validation);
        if (!ExamSessionPolicy.CanCreate(request.Status))
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
        return new((await ProjectAsync(db.ExamSessions.Where(item => item.Id == session.Id), ct)).Single());
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
        var updateKind = ExamSessionPolicy.ClassifyUpdate(session, request);
        if (updateKind == ExamSessionUpdateKind.Unchanged)
            return new((await ProjectAsync(db.ExamSessions.Where(item => item.Id == id), ct)).Single());
        if (updateKind == ExamSessionUpdateKind.EditDraft)
        {
            if (session.QuestionBankId != request.QuestionBankId &&
                await db.ExamSessionQuestions.AnyAsync(item => item.ExamSessionId == id, ct))
                return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Conflict,
                    "Remove selected questions before changing the question bank.", "selection_not_empty");
            var bankError = await CheckBankAsync(request, ct);
            if (bankError is not null) return bankError;
        }
        else if (updateKind == ExamSessionUpdateKind.Publish)
        {
            var readiness = await CheckReadinessAsync(id, session.QuestionBankId, ct);
            if (readiness is not null) return readiness;
        }
        else if (updateKind == ExamSessionUpdateKind.Close)
        {
            // Closing does not change the published exam definition.
        }
        else if (updateKind == ExamSessionUpdateKind.Invalid)
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
        var dto = (await ProjectAsync(db.ExamSessions.Where(item => item.Id == id), ct)).Single();
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
        if (!ExamSessionPolicy.IsValidSelection(request.QuestionIds))
            return ExamSessionResult<IReadOnlyCollection<SelectedExamQuestionDto>>.Invalid("questionIds",
                "Provide 0 to 50 distinct, non-empty question IDs.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await LockSessionAsync(id, ct);
        if (session is null || !actor.CanManageSession(session.CreatedBy))
            return Missing<IReadOnlyCollection<SelectedExamQuestionDto>>();
        if (!ExamSessionPolicy.CanEditSelection(session.Status))
            return ExamSessionResult<IReadOnlyCollection<SelectedExamQuestionDto>>.Fail(
                ExamSessionError.Conflict, "Only a Draft session can change its questions.", "selection_locked");

        var requested = request.QuestionIds!.ToArray();
        var contentById = await questionPreparation.GetSelectableQuestionsAsync(session.QuestionBankId, requested, ct);
        if (contentById.Count != requested.Length)
            return ExamSessionResult<IReadOnlyCollection<SelectedExamQuestionDto>>.Invalid("questionIds",
                "Every selected question must be a Bank question in this session's question bank.");

        await db.ExamSessionQuestions.Where(item => item.ExamSessionId == id).ExecuteDeleteAsync(ct);
        DetachSelectedQuestions(id);
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
        if (!ExamSessionPolicy.CanDelete(session.Status,
                await db.ExamAttempts.AnyAsync(attempt => attempt.ExamSessionId == id, ct)))
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
        if (!ExamSessionPolicy.CanJoin(session.Status))
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

    private async Task<ExamSessionDto[]> ProjectAsync(IQueryable<ExamSession> sessions, CancellationToken ct)
    {
        var rows = await sessions.Select(session => new
        {
            session.Id,
            session.Title,
            session.QuestionBankId,
            session.Status,
            session.CreatedBy,
            session.CreatedAt,
            ParticipantCount = db.ExamAttempts.Count(attempt => attempt.ExamSessionId == session.Id)
        }).ToArrayAsync(ct);
        var names = await questionPreparation.GetBankNamesAsync(
            rows.Select(row => row.QuestionBankId).ToArray(), ct);
        return rows.Where(row => names.ContainsKey(row.QuestionBankId))
            .Select(row => new ExamSessionDto(row.Id, row.Title, row.QuestionBankId,
                names[row.QuestionBankId], row.Status, row.CreatedBy, row.CreatedAt, row.ParticipantCount))
            .ToArray();
    }

    private Task<ExamSession?> LockSessionAsync(Guid id, CancellationToken ct) =>
        db.Database.ProviderName?.Contains("Sqlite", StringComparison.Ordinal) == true
            ? db.ExamSessions.SingleOrDefaultAsync(item => item.Id == id, ct)
            : db.ExamSessions.FromSqlInterpolated($"SELECT * FROM exam_sessions WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);

    private async Task<ExamSessionResult<ExamSessionDto>?> CheckBankAsync(SaveExamSessionRequest request, CancellationToken ct)
    {
        if (!await questionPreparation.BankExistsAsync(request.QuestionBankId, ct))
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
        var selectedIds = await db.ExamSessionQuestions.AsNoTracking()
            .Where(item => item.ExamSessionId == sessionId)
            .OrderBy(item => item.DisplayOrder).Select(item => item.QuestionId).ToArrayAsync(ct);
        var preparation = await questionPreparation.GetReadinessAsync(bankId, selectedIds, ct);
        var readiness = ExamSessionPolicy.CheckReadiness(selectedIds, preparation);
        return readiness.IsReady ? null : ExamSessionResult<ExamSessionDto>.Fail(
            ExamSessionError.Conflict, "The session is not ready to publish.", "session_not_ready",
            readiness.Missing, readiness.QuestionIdsWithoutKeyPoints);
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
