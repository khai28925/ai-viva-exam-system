using AiViva.Application.Abstractions;

namespace AiViva.Application.QuestionBanks;

public interface IQuestionBankService
{
    Task<IReadOnlyCollection<QuestionBankDto>> GetAllAsync(CancellationToken ct = default);
    Task<QuestionBankDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<QuestionBankDto> CreateAsync(CreateQuestionBankRequest request, CancellationToken ct = default);
    Task<QuestionBankDto?> UpdateAsync(Guid id, UpdateQuestionBankRequest request, CancellationToken ct = default);
    Task<QuestionBankDeleteResult> DeleteAsync(Guid id, CancellationToken ct = default);
}
