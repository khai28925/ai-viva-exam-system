using System;
using System.Collections.Generic;
using System.Text;
using AiViva.Application.ExpectedKeyPoints;
using AiViva.Application.Questions;
using AiViva.Domain.Entities;

namespace AiViva.Application.Abstractions
{
    public interface IExpectedKeyPoints
    {
        Task<IReadOnlyCollection<ExpectedKeyPoint>?> GetByQuestionIdAsync(Guid questionId, CancellationToken ct = default);
        Task<ExpectedKeyPoint> GetByIdAsync(Guid keyPointsId, CancellationToken ct = default);
        Task<ExpectedKeyPoint> CreateAsync(CreateExpectedKeyPointsRequest request, CancellationToken ct = default);
        Task<ExpectedKeyPoint> UpdateAsync(Guid keyPointsId, UpdateExpectedKeyPointsRequest request, CancellationToken ct = default);
        Task<bool> DeleteAsync(Guid keyPointsId, CancellationToken ct = default);
    }
}
