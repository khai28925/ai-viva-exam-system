namespace AiViva.Application.Rubrics;

public interface IRubricService
{
    Task<RubricServiceResult<RubricDto>> GetRubricAsync(Guid bankId, CancellationToken ct = default);

    Task<RubricServiceResult<RubricDto>> CreateRubricAsync(Guid bankId, CreateRubricRequest request, CancellationToken ct = default);

    Task<RubricServiceResult<RubricDto>> UpdateRubricAsync(Guid bankId, UpdateRubricRequest request, CancellationToken ct = default);

    Task<RubricServiceResult<bool>> DeleteRubricAsync(Guid bankId, CancellationToken ct = default);

    Task<RubricServiceResult<IReadOnlyCollection<RubricCriterionDto>>> GetCriteriaAsync(Guid bankId, CancellationToken ct = default);

    Task<RubricServiceResult<RubricCriterionDto>> GetCriterionByIdAsync(Guid bankId, Guid criterionId, CancellationToken ct = default);

    Task<RubricServiceResult<RubricCriterionDto>> CreateCriterionAsync(Guid bankId, CreateRubricCriterionRequest request, CancellationToken ct = default);

    Task<RubricServiceResult<RubricCriterionDto>> UpdateCriterionAsync(Guid bankId, Guid criterionId, UpdateRubricCriterionRequest request, CancellationToken ct = default);

    Task<RubricServiceResult<bool>> DeleteCriterionAsync(Guid bankId, Guid criterionId, CancellationToken ct = default);
}
