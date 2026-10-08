using System.ComponentModel.DataAnnotations;
using AiViva.Application.ExamSessions;
using AiViva.Domain.Entities;
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
        if (session.QuestionBankId != request.QuestionBankId &&
            await db.ExamAttempts.AnyAsync(attempt => attempt.ExamSessionId == id, ct))
            return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Conflict, "Cannot change the question bank after a student has joined.");
        var bankError = await CheckBankAsync(request, ct);
        if (bankError is not null) return bankError;

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

    public async Task<ExamSessionResult<bool>> DeleteAsync(Guid id, ExamActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManage) return Forbidden<bool>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await LockSessionAsync(id, ct);
        if (session is null || !actor.CanManageSession(session.CreatedBy)) return Missing<bool>();
        if (await db.ExamAttempts.AnyAsync(attempt => attempt.ExamSessionId == id, ct))
            return ExamSessionResult<bool>.Fail(ExamSessionError.Conflict, "Cannot delete a session after a student has joined. Close it instead.");
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
        var questions = await db.Questions.AsNoTracking()
            .Where(question => question.QuestionBankId == session.QuestionBankId)
            .OrderBy(question => question.CreatedAt).ThenBy(question => question.Id)
            .Select(question => new ExamQuestionDto(question.Id, question.Content)).ToArrayAsync(ct);
        if (questions.Length == 0)
            return ExamSessionResult<ExamAttemptDto>.Fail(ExamSessionError.Conflict, "This exam session has no questions.");
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

    private Task<ExamSession?> LockSessionAsync(Guid id, CancellationToken ct) => db.ExamSessions
        .FromSqlInterpolated($"SELECT * FROM exam_sessions WHERE id = {id} FOR UPDATE")
        .SingleOrDefaultAsync(ct);

    private async Task<ExamSessionResult<ExamSessionDto>?> CheckBankAsync(SaveExamSessionRequest request, CancellationToken ct)
    {
        if (!await db.QuestionBanks.AnyAsync(bank => bank.Id == request.QuestionBankId, ct))
            return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.NotFound, "Question bank was not found.");
        if (request.Status == ExamSession.Statuses.Open &&
            !await db.Questions.AnyAsync(question => question.QuestionBankId == request.QuestionBankId, ct))
            return ExamSessionResult<ExamSessionDto>.Fail(ExamSessionError.Conflict, "Add at least one question before opening a session.");
        return null;
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
