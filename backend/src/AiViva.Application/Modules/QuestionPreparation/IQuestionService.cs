namespace AiViva.Application.Questions;

public interface IQuestionService
{
    Task<IReadOnlyCollection<QuestionDto>?> GetByQuestionBankIdAsync(Guid questionBankId, CancellationToken ct = default);
    Task<QuestionDto?> GetByIdAsync(Guid questionBankId, Guid questionId, CancellationToken ct = default);
    Task<QuestionDto?> CreateAsync(Guid questionBankId, CreateQuestionRequest request, CancellationToken ct = default);
    Task<QuestionDto?> UpdateAsync(Guid questionBankId, Guid questionId, UpdateQuestionRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid questionBankId, Guid questionId, CancellationToken ct = default);
}
