using System.Data;
using AiViva.Application.Abstractions;
using AiViva.Application.ExpectedKeyPoints;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiViva.Infrastructure.Persistence;

internal sealed class ExpectedKeyPointsRepository(QuestionBankDbContext db) : IExpectedKeyPoints
{
    private const int MaxWriteAttempts = 3;

    public async Task<ExpectedKeyPointsResult<IReadOnlyCollection<ExpectedKeyPointsDto>>> GetByQuestionIdAsync(
        Guid bankId, Guid questionId, CancellationToken ct = default)
    {
        if (!await QuestionExistsAsync(bankId, questionId, ct))
        {
            return ExpectedKeyPointsResult<IReadOnlyCollection<ExpectedKeyPointsDto>>.Fail(
                ExpectedKeyPointsError.QuestionNotFound, QuestionNotFoundDetail(bankId, questionId));
        }

        var entities = await db.ExpectedKeyPoints.AsNoTracking()
            .Where(point => point.QuestionId == questionId)
            .OrderBy(point => point.DisplayOrder)
            .ThenBy(point => point.Id)
            .ToArrayAsync(ct);
        IReadOnlyCollection<ExpectedKeyPointsDto> result = entities.Select(ToDto).ToArray();
        return ExpectedKeyPointsResult<IReadOnlyCollection<ExpectedKeyPointsDto>>.Success(result);
    }

    public async Task<ExpectedKeyPointsResult<ExpectedKeyPointsDto>> GetByIdAsync(
        Guid bankId, Guid questionId, Guid keyPointId, CancellationToken ct = default)
    {
        if (!await QuestionExistsAsync(bankId, questionId, ct))
        {
            return ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Fail(
                ExpectedKeyPointsError.QuestionNotFound, QuestionNotFoundDetail(bankId, questionId));
        }

        var entity = await db.ExpectedKeyPoints.AsNoTracking()
            .SingleOrDefaultAsync(point => point.QuestionId == questionId && point.Id == keyPointId, ct);
        return entity is null
            ? ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Fail(
                ExpectedKeyPointsError.KeyPointNotFound, KeyPointNotFoundDetail(keyPointId, questionId))
            : ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Success(ToDto(entity));
    }

    public async Task<ExpectedKeyPointsResult<ExpectedKeyPointsDto>> CreateAsync(
        Guid bankId, Guid questionId, CreateExpectedKeyPointsRequest request, CancellationToken ct = default)
    {
        var validationErrors = ExpectedKeyPointsValidation.Validate(request);
        if (validationErrors is not null)
        {
            return ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Fail(
                ExpectedKeyPointsError.Invalid, "Invalid expected key point request.", validationErrors);
        }

        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await QuestionExistsAsync(bankId, questionId, ct))
                {
                    return ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Fail(
                        ExpectedKeyPointsError.QuestionNotFound, QuestionNotFoundDetail(bankId, questionId));
                }

                if (await IsQuestionLockedAsync(questionId, ct))
                {
                    return PreparationLocked<ExpectedKeyPointsDto>();
                }

                var maxOrder = await db.ExpectedKeyPoints.AsNoTracking()
                    .Where(point => point.QuestionId == questionId)
                    .MaxAsync(point => (int?)point.DisplayOrder, ct);
                var entity = new ExpectedKeyPoint
                {
                    Id = Guid.NewGuid(),
                    QuestionId = questionId,
                    Content = request.Content,
                    DisplayOrder = (maxOrder ?? -1) + 1,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.ExpectedKeyPoints.Add(entity);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Success(ToDto(entity));
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                db.ChangeTracker.Clear();
                if (attempt == MaxWriteAttempts - 1)
                {
                    return ConcurrentWrite<ExpectedKeyPointsDto>();
                }
            }
            catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
            {
                db.ChangeTracker.Clear();
                return ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Fail(
                    ExpectedKeyPointsError.QuestionNotFound, QuestionNotFoundDetail(bankId, questionId));
            }
        }

        return ConcurrentWrite<ExpectedKeyPointsDto>();
    }

    public async Task<ExpectedKeyPointsResult<ExpectedKeyPointsDto>> UpdateAsync(
        Guid bankId, Guid questionId, Guid keyPointId, UpdateExpectedKeyPointsRequest request,
        CancellationToken ct = default)
    {
        var validationErrors = ExpectedKeyPointsValidation.Validate(request);
        if (validationErrors is not null)
        {
            return ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Fail(
                ExpectedKeyPointsError.Invalid, "Invalid expected key point request.", validationErrors);
        }

        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await QuestionExistsAsync(bankId, questionId, ct))
                {
                    return ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Fail(
                        ExpectedKeyPointsError.QuestionNotFound, QuestionNotFoundDetail(bankId, questionId));
                }

                var entity = await db.ExpectedKeyPoints.SingleOrDefaultAsync(
                    point => point.QuestionId == questionId && point.Id == keyPointId, ct);
                if (entity is null)
                {
                    return ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Fail(
                        ExpectedKeyPointsError.KeyPointNotFound, KeyPointNotFoundDetail(keyPointId, questionId));
                }

                if (await IsQuestionLockedAsync(questionId, ct))
                {
                    return PreparationLocked<ExpectedKeyPointsDto>();
                }

                entity.Content = request.Content;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return ExpectedKeyPointsResult<ExpectedKeyPointsDto>.Success(ToDto(entity));
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                db.ChangeTracker.Clear();
                if (attempt == MaxWriteAttempts - 1)
                {
                    return ConcurrentWrite<ExpectedKeyPointsDto>();
                }
            }
        }

        return ConcurrentWrite<ExpectedKeyPointsDto>();
    }

    public async Task<ExpectedKeyPointsResult<bool>> DeleteAsync(
        Guid bankId, Guid questionId, Guid keyPointId, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!await QuestionExistsAsync(bankId, questionId, ct))
                {
                    return ExpectedKeyPointsResult<bool>.Fail(
                        ExpectedKeyPointsError.QuestionNotFound, QuestionNotFoundDetail(bankId, questionId));
                }

                var entity = await db.ExpectedKeyPoints.SingleOrDefaultAsync(
                    point => point.QuestionId == questionId && point.Id == keyPointId, ct);
                if (entity is null)
                {
                    return ExpectedKeyPointsResult<bool>.Fail(
                        ExpectedKeyPointsError.KeyPointNotFound, KeyPointNotFoundDetail(keyPointId, questionId));
                }

                if (await IsQuestionLockedAsync(questionId, ct))
                {
                    return PreparationLocked<bool>();
                }

                db.ExpectedKeyPoints.Remove(entity);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return ExpectedKeyPointsResult<bool>.Success(true);
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                db.ChangeTracker.Clear();
                if (attempt == MaxWriteAttempts - 1)
                {
                    return ConcurrentWrite<bool>();
                }
            }
        }

        return ConcurrentWrite<bool>();
    }

    private Task<bool> QuestionExistsAsync(Guid bankId, Guid questionId, CancellationToken ct) =>
        db.Questions.AnyAsync(question =>
            question.Id == questionId && question.QuestionBankId == bankId && question.QuestionType == "Bank", ct);

    private Task<bool> IsQuestionLockedAsync(Guid questionId, CancellationToken ct) =>
        db.ExamSessionQuestions.AnyAsync(selection =>
            selection.QuestionId == questionId && db.ExamSessions.Any(session =>
                session.Id == selection.ExamSessionId &&
                (session.Status == ExamSession.Statuses.Open || session.Status == ExamSession.Statuses.Closed)), ct);

    private static bool IsRetryable(Exception exception) =>
        exception is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure
        } || exception is DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure
            }
        };

    private static bool IsForeignKeyViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };

    private static ExpectedKeyPointsResult<T> PreparationLocked<T>() =>
        ExpectedKeyPointsResult<T>.Fail(
            ExpectedKeyPointsError.PreparationLocked,
            "Cannot modify expected key points for a question selected in an open or closed exam session.");

    private static ExpectedKeyPointsResult<T> ConcurrentWrite<T>() =>
        ExpectedKeyPointsResult<T>.Fail(
            ExpectedKeyPointsError.ConcurrentWrite,
            "The expected key point was modified concurrently. Please retry.");

    private static string QuestionNotFoundDetail(Guid bankId, Guid questionId) =>
        $"Bank question '{questionId}' was not found in question bank '{bankId}'.";

    private static string KeyPointNotFoundDetail(Guid keyPointId, Guid questionId) =>
        $"Expected key point '{keyPointId}' was not found for question '{questionId}'.";

    private static ExpectedKeyPointsDto ToDto(ExpectedKeyPoint point) =>
        new(point.Id, point.QuestionId, point.Content, point.DisplayOrder, point.CreatedAt);
}
