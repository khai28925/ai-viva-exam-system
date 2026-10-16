using AiViva.Application.Rubrics;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiViva.Infrastructure.Rubrics;

public sealed class PostgresRubricService(QuestionBankDbContext db) : IRubricService
{
    public async Task<RubricServiceResult<RubricDto>> GetRubricAsync(Guid bankId, CancellationToken ct = default)
    {
        if (!await BankExistsAsync(bankId, ct))
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        var rubric = await db.Rubrics.AsNoTracking().SingleOrDefaultAsync(r => r.QuestionBankId == bankId, ct);
        if (rubric is null)
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.RubricNotFound, $"Rubric for question bank '{bankId}' was not found.");
        }

        return RubricServiceResult<RubricDto>.Success(ToDto(rubric));
    }

    public async Task<RubricServiceResult<RubricDto>> CreateRubricAsync(Guid bankId, CreateRubricRequest request, CancellationToken ct = default)
    {
        var validationErrors = RubricValidation.Validate(request);
        if (validationErrors is not null)
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.Invalid, "Invalid rubric request.", validationErrors);
        }

        if (!await BankExistsAsync(bankId, ct))
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        if (await IsBankLockedAsync(bankId, ct))
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.PreparationLocked, "Cannot modify rubric or criteria for a question bank used in an open or closed exam session.");
        }

        if (await db.Rubrics.AnyAsync(r => r.QuestionBankId == bankId, ct))
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.RubricAlreadyExists, "A rubric already exists for this question bank.");
        }

        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            QuestionBankId = bankId,
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Rubrics.Add(rubric);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.RubricAlreadyExists, "A rubric already exists for this question bank.");
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        return RubricServiceResult<RubricDto>.Success(ToDto(rubric));
    }

    public async Task<RubricServiceResult<RubricDto>> UpdateRubricAsync(Guid bankId, UpdateRubricRequest request, CancellationToken ct = default)
    {
        var validationErrors = RubricValidation.Validate(request);
        if (validationErrors is not null)
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.Invalid, "Invalid rubric request.", validationErrors);
        }

        if (!await BankExistsAsync(bankId, ct))
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        if (await IsBankLockedAsync(bankId, ct))
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.PreparationLocked, "Cannot modify rubric or criteria for a question bank used in an open or closed exam session.");
        }

        var rubric = await db.Rubrics.SingleOrDefaultAsync(r => r.QuestionBankId == bankId, ct);
        if (rubric is null)
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.RubricNotFound, $"Rubric for question bank '{bankId}' was not found.");
        }

        rubric.Name = request.Name;
        rubric.Description = request.Description;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            return RubricServiceResult<RubricDto>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        return RubricServiceResult<RubricDto>.Success(ToDto(rubric));
    }

    public async Task<RubricServiceResult<bool>> DeleteRubricAsync(Guid bankId, CancellationToken ct = default)
    {
        if (!await BankExistsAsync(bankId, ct))
        {
            return RubricServiceResult<bool>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        if (await IsBankLockedAsync(bankId, ct))
        {
            return RubricServiceResult<bool>.Fail(RubricServiceError.PreparationLocked, "Cannot modify rubric or criteria for a question bank used in an open or closed exam session.");
        }

        var rubric = await db.Rubrics.SingleOrDefaultAsync(r => r.QuestionBankId == bankId, ct);
        if (rubric is null)
        {
            return RubricServiceResult<bool>.Fail(RubricServiceError.RubricNotFound, $"Rubric for question bank '{bankId}' was not found.");
        }

        if (await db.RubricCriteria.AnyAsync(c => c.RubricId == rubric.Id, ct))
        {
            return RubricServiceResult<bool>.Fail(RubricServiceError.RubricHasCriteria, "Cannot delete a rubric that still contains criteria.");
        }

        db.Rubrics.Remove(rubric);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            return RubricServiceResult<bool>.Fail(RubricServiceError.RubricHasCriteria, "Cannot delete a rubric that still contains criteria.");
        }

        return RubricServiceResult<bool>.Success(true);
    }

    public async Task<RubricServiceResult<IReadOnlyCollection<RubricCriterionDto>>> GetCriteriaAsync(Guid bankId, CancellationToken ct = default)
    {
        if (!await BankExistsAsync(bankId, ct))
        {
            return RubricServiceResult<IReadOnlyCollection<RubricCriterionDto>>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        var rubric = await db.Rubrics.AsNoTracking().SingleOrDefaultAsync(r => r.QuestionBankId == bankId, ct);
        if (rubric is null)
        {
            return RubricServiceResult<IReadOnlyCollection<RubricCriterionDto>>.Fail(RubricServiceError.RubricNotFound, $"Rubric for question bank '{bankId}' was not found.");
        }

        var rawCriteria = await db.RubricCriteria.AsNoTracking()
            .Where(c => c.RubricId == rubric.Id)
            .ToArrayAsync(ct);

        var criteria = rawCriteria
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => ToCriterionDto(c))
            .ToArray();

        return RubricServiceResult<IReadOnlyCollection<RubricCriterionDto>>.Success(criteria);
    }

    public async Task<RubricServiceResult<RubricCriterionDto>> GetCriterionByIdAsync(Guid bankId, Guid criterionId, CancellationToken ct = default)
    {
        if (!await BankExistsAsync(bankId, ct))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        var rubric = await db.Rubrics.AsNoTracking().SingleOrDefaultAsync(r => r.QuestionBankId == bankId, ct);
        if (rubric is null)
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.RubricNotFound, $"Rubric for question bank '{bankId}' was not found.");
        }

        var criterion = await db.RubricCriteria.AsNoTracking()
            .SingleOrDefaultAsync(c => c.RubricId == rubric.Id && c.Id == criterionId, ct);
        if (criterion is null)
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.CriterionNotFound, $"Criterion with id '{criterionId}' was not found in rubric for bank '{bankId}'.");
        }

        return RubricServiceResult<RubricCriterionDto>.Success(ToCriterionDto(criterion));
    }

    public async Task<RubricServiceResult<RubricCriterionDto>> CreateCriterionAsync(Guid bankId, CreateRubricCriterionRequest request, CancellationToken ct = default)
    {
        var validationErrors = RubricValidation.Validate(request);
        if (validationErrors is not null)
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.Invalid, "Invalid criterion request.", validationErrors);
        }

        if (!await BankExistsAsync(bankId, ct))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        if (await IsBankLockedAsync(bankId, ct))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.PreparationLocked, "Cannot modify rubric or criteria for a question bank used in an open or closed exam session.");
        }

        var rubric = await db.Rubrics.SingleOrDefaultAsync(r => r.QuestionBankId == bankId, ct);
        if (rubric is null)
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.RubricNotFound, $"Rubric for question bank '{bankId}' was not found.");
        }

        if (await db.RubricCriteria.AnyAsync(c => c.RubricId == rubric.Id && c.Name == request.Name, ct))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.CriterionNameExists, $"A criterion named '{request.Name}' already exists in this rubric.");
        }

        var criterion = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            Name = request.Name,
            Description = request.Description,
            MaxScore = request.MaxScore,
            Weight = request.Weight,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.RubricCriteria.Add(criterion);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.CriterionNameExists, $"A criterion named '{request.Name}' already exists in this rubric.");
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.RubricNotFound, $"Rubric for question bank '{bankId}' was not found.");
        }

        return RubricServiceResult<RubricCriterionDto>.Success(ToCriterionDto(criterion));
    }

    public async Task<RubricServiceResult<RubricCriterionDto>> UpdateCriterionAsync(Guid bankId, Guid criterionId, UpdateRubricCriterionRequest request, CancellationToken ct = default)
    {
        var validationErrors = RubricValidation.Validate(request);
        if (validationErrors is not null)
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.Invalid, "Invalid criterion request.", validationErrors);
        }

        if (!await BankExistsAsync(bankId, ct))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        if (await IsBankLockedAsync(bankId, ct))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.PreparationLocked, "Cannot modify rubric or criteria for a question bank used in an open or closed exam session.");
        }

        var rubric = await db.Rubrics.SingleOrDefaultAsync(r => r.QuestionBankId == bankId, ct);
        if (rubric is null)
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.RubricNotFound, $"Rubric for question bank '{bankId}' was not found.");
        }

        var criterion = await db.RubricCriteria.SingleOrDefaultAsync(c => c.RubricId == rubric.Id && c.Id == criterionId, ct);
        if (criterion is null)
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.CriterionNotFound, $"Criterion with id '{criterionId}' was not found in rubric for bank '{bankId}'.");
        }

        if (await db.RubricCriteria.AnyAsync(c => c.RubricId == rubric.Id && c.Id != criterionId && c.Name == request.Name, ct))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.CriterionNameExists, $"A criterion named '{request.Name}' already exists in this rubric.");
        }

        criterion.Name = request.Name;
        criterion.Description = request.Description;
        criterion.MaxScore = request.MaxScore;
        criterion.Weight = request.Weight;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return RubricServiceResult<RubricCriterionDto>.Fail(RubricServiceError.CriterionNameExists, $"A criterion named '{request.Name}' already exists in this rubric.");
        }

        return RubricServiceResult<RubricCriterionDto>.Success(ToCriterionDto(criterion));
    }

    public async Task<RubricServiceResult<bool>> DeleteCriterionAsync(Guid bankId, Guid criterionId, CancellationToken ct = default)
    {
        if (!await BankExistsAsync(bankId, ct))
        {
            return RubricServiceResult<bool>.Fail(RubricServiceError.BankNotFound, $"Question bank with id '{bankId}' was not found.");
        }

        if (await IsBankLockedAsync(bankId, ct))
        {
            return RubricServiceResult<bool>.Fail(RubricServiceError.PreparationLocked, "Cannot modify rubric or criteria for a question bank used in an open or closed exam session.");
        }

        var rubric = await db.Rubrics.SingleOrDefaultAsync(r => r.QuestionBankId == bankId, ct);
        if (rubric is null)
        {
            return RubricServiceResult<bool>.Fail(RubricServiceError.RubricNotFound, $"Rubric for question bank '{bankId}' was not found.");
        }

        var criterion = await db.RubricCriteria.SingleOrDefaultAsync(c => c.RubricId == rubric.Id && c.Id == criterionId, ct);
        if (criterion is null)
        {
            return RubricServiceResult<bool>.Fail(RubricServiceError.CriterionNotFound, $"Criterion with id '{criterionId}' was not found in rubric for bank '{bankId}'.");
        }

        db.RubricCriteria.Remove(criterion);
        await db.SaveChangesAsync(ct);

        return RubricServiceResult<bool>.Success(true);
    }

    private Task<bool> BankExistsAsync(Guid bankId, CancellationToken ct) =>
        db.QuestionBanks.AnyAsync(b => b.Id == bankId, ct);

    private Task<bool> IsBankLockedAsync(Guid bankId, CancellationToken ct) =>
        db.ExamSessions.AnyAsync(
            s => s.QuestionBankId == bankId &&
                 (s.Status == ExamSession.Statuses.Open || s.Status == ExamSession.Statuses.Closed),
            ct);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static bool IsForeignKeyViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };

    private static RubricDto ToDto(Rubric r) =>
        new(r.Id, r.QuestionBankId, r.Name, r.Description, r.CreatedAt);

    private static RubricCriterionDto ToCriterionDto(RubricCriterion c) =>
        new(c.Id, c.RubricId, c.Name, c.Description, c.MaxScore, c.Weight, c.CreatedAt);
}
