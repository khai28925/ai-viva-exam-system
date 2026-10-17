using System;
using System.Collections.Generic;
using System.Text;
using AiViva.Application.Questions;

namespace AiViva.Application.ExpectedKeyPoints
{
    public interface IExpectedKeyPointsService
    {
        Task<IReadOnlyCollection<ExpectedKeyPointsDto>?> GetByQuestionIdAsync(Guid questionId, CancellationToken ct = default);
        Task<ExpectedKeyPointsDto?> GetByIdAsync(Guid keyPointId, CancellationToken ct = default);
        Task<ExpectedKeyPointsDto?> CreateAsync(CreateExpectedKeyPointsRequest request, CancellationToken ct = default);
        Task<ExpectedKeyPointsDto?> UpdateAsync(Guid keyPointId, UpdateExpectedKeyPointsRequest request, CancellationToken ct = default);
        Task<bool> DeleteAsync(Guid keyPointId, CancellationToken ct = default);
    }
}
