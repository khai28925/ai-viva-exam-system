namespace AiViva.Application.QuestionBanks;

public interface IQuestionBankService
{
    Task<QuestionBankDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyCollection<QuestionBankDto>> GetAllAsync(CancellationToken ct = default);
    Task<QuestionBankDto> CreateAsync(CreateQuestionBankRequest request, CancellationToken ct = default);
    Task<QuestionBankDto?> UpdateAsync(Guid id, UpdateQuestionBankRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
