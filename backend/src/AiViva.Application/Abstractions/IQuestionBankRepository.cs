using AiViva.Domain.Entities;

namespace AiViva.Application.Abstractions;

public interface IQuestionBankRepository
{
    Task<IReadOnlyCollection<QuestionBank>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<QuestionBank?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(QuestionBank questionBank, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(QuestionBank questionBank, CancellationToken cancellationToken = default);

    Task<QuestionBankDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Question>> GetQuestionsAsync(
        Guid questionBankId,
        CancellationToken cancellationToken = default);

    Task<Question?> GetQuestionByIdAsync(
        Guid questionBankId,
        Guid questionId,
        CancellationToken cancellationToken = default);

    Task<bool> AddQuestionAsync(Question question, CancellationToken cancellationToken = default);

    Task<bool> UpdateQuestionAsync(Question question, CancellationToken cancellationToken = default);

    Task<bool> DeleteQuestionAsync(
        Guid questionBankId,
        Guid questionId,
        CancellationToken cancellationToken = default);
}
