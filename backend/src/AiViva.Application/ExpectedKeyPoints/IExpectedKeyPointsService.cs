namespace AiViva.Application.ExpectedKeyPoints;

public interface IExpectedKeyPointsService
{
    Task<ExpectedKeyPointsResult<IReadOnlyCollection<ExpectedKeyPointsDto>>> GetByQuestionIdAsync(
        Guid bankId, Guid questionId, CancellationToken ct = default);

    Task<ExpectedKeyPointsResult<ExpectedKeyPointsDto>> GetByIdAsync(
        Guid bankId, Guid questionId, Guid keyPointId, CancellationToken ct = default);

    Task<ExpectedKeyPointsResult<ExpectedKeyPointsDto>> CreateAsync(
        Guid bankId, Guid questionId, CreateExpectedKeyPointsRequest request, CancellationToken ct = default);

    Task<ExpectedKeyPointsResult<ExpectedKeyPointsDto>> UpdateAsync(
        Guid bankId, Guid questionId, Guid keyPointId, UpdateExpectedKeyPointsRequest request, CancellationToken ct = default);

    Task<ExpectedKeyPointsResult<bool>> DeleteAsync(
        Guid bankId, Guid questionId, Guid keyPointId, CancellationToken ct = default);
}
