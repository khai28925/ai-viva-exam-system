using System;
using System.Collections.Generic;
using System.Text;
using AiViva.Application.Abstractions;
using AiViva.Application.ExpectedKeyPoints;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Persistence
{
    internal class ExpectedKeyPointsRepository(QuestionBankDbContext db) : IExpectedKeyPoints
    {
        private async Task SaveChangesSafeAsync(CancellationToken ct)
        {
            try { await db.SaveChangesAsync(ct); }
            catch { db.ChangeTracker.Clear(); throw; }
        }
        public async Task<ExpectedKeyPoint?> CreateAsync(CreateExpectedKeyPointsRequest request, CancellationToken ct = default)
        {
            var list = GetByQuestionIdAsync(request.QuestionId, ct);
            var displayOrder = 0;
            if (list.Result is not null)
            {
                displayOrder = list.Result.First().DisplayOrder + 1;
            }
            var entity = new ExpectedKeyPoint
            {
                Id = Guid.NewGuid(),
                QuestionId = request.QuestionId,
                Content = request.Content,
                DisplayOrder = displayOrder
            };
            db.ExpectedKeyPoints.Add(entity);
            await SaveChangesSafeAsync(ct);
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid keyPointsId, CancellationToken ct = default)
        {
            var entity = GetByIdAsync(keyPointsId);
            if (entity is not null)
            {
                db.ExpectedKeyPoints.Remove(entity);
                await SaveChangesSafeAsync(ct);
                return true;
            } ;       
            return false;
        }

        public async Task<ExpectedKeyPoint?> GetByIdAsync(Guid keyPointsId, CancellationToken ct = default)
        {
            return await db.ExpectedKeyPoints.AsNoTracking().SingleOrDefaultAsync(x => x.Id == keyPointsId, ct);
        }

        public async Task<IReadOnlyCollection<ExpectedKeyPoint>?> GetByQuestionIdAsync(Guid questionId, CancellationToken ct = default)
        {
            return await db.ExpectedKeyPoints.AsNoTracking().Where(x => x.QuestionId == questionId).OrderByDescending(x => x.DisplayOrder).ThenBy(x => x.Id).ToArrayAsync(ct);
        }

        public async Task<ExpectedKeyPoint?> UpdateAsync(Guid keyPointsId, UpdateExpectedKeyPointsRequest request, CancellationToken ct = default)
        {
            var entity = GetByIdAsync(keyPointsId, ct).Result;
            if (entity is not null) {
                entity.QuestionId = request.QuestionId;
                entity.Content = request.Content;
                await SaveChangesSafeAsync(ct);
                return entity;
            }
            return null;
        }
    }
}
