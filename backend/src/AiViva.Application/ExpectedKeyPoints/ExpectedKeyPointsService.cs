using System;
using System.Collections.Generic;
using System.Text;
using AiViva.Application.Abstractions;
using AiViva.Application.QuestionBanks;
using AiViva.Application.Questions;
using AiViva.Domain.Entities;

namespace AiViva.Application.ExpectedKeyPoints
{
    internal class ExpectedKeyPointsService(IExpectedKeyPoints expectedKeyPointsRepos) : IExpectedKeyPointsService
    {
        
        private static ExpectedKeyPointsDto? ToDto(ExpectedKeyPoint entity)
        {
            if (entity == null) return null;
            return new ExpectedKeyPointsDto(
                Id = entity.Id,
                QuestionId = entity.QuestionId,
                Content = entity.Content,
                DisplayOrder = entity.DisplayOrder,
                CreatedAt = entity.CreatedAt);

        }

        public async Task<ExpectedKeyPointsDto?> CreateAsync(CreateExpectedKeyPointsRequest request, CancellationToken ct = default)
        {
            return ToDto(expectedKeyPointsRepos.CreateAsync(request, ct).Result);
        }

        public async Task<bool> DeleteAsync(Guid keyPointId, CancellationToken ct = default)
        {
            return expectedKeyPointsRepos.DeleteAsync(keyPointId, ct).Result;
        }

        public async Task<ExpectedKeyPointsDto?> GetByIdAsync(Guid keyPointId, CancellationToken ct = default)
        {
           return ToDto(expectedKeyPointsRepos.GetByIdAsync(keyPointId, ct).Result);
        }

        public async Task<IReadOnlyCollection<ExpectedKeyPointsDto>?> GetByQuestionIdAsync(Guid questionId, CancellationToken ct = default)
        {
            return expectedKeyPointsRepos.GetByQuestionIdAsync(questionId, ct).Result;
        }

        public async Task<ExpectedKeyPointsDto?> UpdateAsync(Guid keyPointId, UpdateExpectedKeyPointsRequest request, CancellationToken ct = default)
        {
            throw expectedKeyPointsRepos.UpdateAsync(keyPointId, request, ct);
        }
    }
}
