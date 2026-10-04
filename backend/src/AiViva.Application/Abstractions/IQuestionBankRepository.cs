using AiViva.Domain.Entities;

namespace AiViva.Application.Abstractions;

public interface IQuestionBankRepository
{
    Task<QuestionBank?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyCollection<QuestionBank>> GetAllAsync(CancellationToken ct = default);
    Task<QuestionBank> AddAsync(QuestionBank entity, CancellationToken ct = default);
    Task<QuestionBank> UpdateAsync(QuestionBank entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
