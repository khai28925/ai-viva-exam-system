namespace AiViva.Application.Questions;

public interface IQuestionService
{
    Task<QuestionDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyCollection<QuestionDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyCollection<QuestionDto>> GetByQuestionBankIdAsync(Guid questionBankId, CancellationToken ct = default);
    Task<QuestionDto> CreateAsync(CreateQuestionRequest request, CancellationToken ct = default);
    Task<QuestionDto?> UpdateAsync(Guid id, UpdateQuestionRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
