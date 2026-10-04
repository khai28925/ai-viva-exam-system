using AiViva.Domain.Entities;

namespace AiViva.Application.Abstractions;

public interface IQuestionRepository
{
    Task<Question?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyCollection<Question>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<Question>> GetByQuestionBankIdAsync(Guid questionBankId, CancellationToken ct = default);
    Task<Question> AddAsync(Question entity, CancellationToken ct = default);
    Task<Question> UpdateAsync(Question entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
