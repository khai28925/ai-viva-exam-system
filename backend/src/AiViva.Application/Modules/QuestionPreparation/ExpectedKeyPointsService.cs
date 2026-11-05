using AiViva.Application.Abstractions;

namespace AiViva.Application.ExpectedKeyPoints;

internal sealed class ExpectedKeyPointsService(IExpectedKeyPoints repository) : IExpectedKeyPointsService
{
    public Task<ExpectedKeyPointsResult<IReadOnlyCollection<ExpectedKeyPointsDto>>> GetByQuestionIdAsync(
        Guid bankId, Guid questionId, CancellationToken ct = default) =>
        repository.GetByQuestionIdAsync(bankId, questionId, ct);

    public Task<ExpectedKeyPointsResult<ExpectedKeyPointsDto>> GetByIdAsync(
        Guid bankId, Guid questionId, Guid keyPointId, CancellationToken ct = default) =>
        repository.GetByIdAsync(bankId, questionId, keyPointId, ct);

    public Task<ExpectedKeyPointsResult<ExpectedKeyPointsDto>> CreateAsync(
        Guid bankId, Guid questionId, CreateExpectedKeyPointsRequest request, CancellationToken ct = default) =>
        repository.CreateAsync(bankId, questionId, request, ct);

    public Task<ExpectedKeyPointsResult<ExpectedKeyPointsDto>> UpdateAsync(
        Guid bankId, Guid questionId, Guid keyPointId, UpdateExpectedKeyPointsRequest request, CancellationToken ct = default) =>
        repository.UpdateAsync(bankId, questionId, keyPointId, request, ct);

    public Task<ExpectedKeyPointsResult<bool>> DeleteAsync(
        Guid bankId, Guid questionId, Guid keyPointId, CancellationToken ct = default) =>
        repository.DeleteAsync(bankId, questionId, keyPointId, ct);
}
