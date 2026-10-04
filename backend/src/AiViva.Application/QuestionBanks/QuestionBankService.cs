using AiViva.Application.Abstractions;
using AiViva.Domain.Entities;

namespace AiViva.Application.QuestionBanks;

public sealed class QuestionBankService(IQuestionBankRepository questionBankRepository) : IQuestionBankService
{
    public async Task<IReadOnlyCollection<QuestionBankDto>> GetAllAsync(CancellationToken ct = default)
    {
        var banks = await questionBankRepository.GetAllAsync(ct);
        return banks.Select(ToDto).ToArray();
    }

    public async Task<QuestionBankDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var bank = await questionBankRepository.GetByIdAsync(id, ct);
        return bank is null ? null : ToDto(bank);
    }

    public async Task<QuestionBankDto> CreateAsync(CreateQuestionBankRequest request, CancellationToken ct = default)
    {
        var bank = new QuestionBank(request.Name, request.Description);
        await questionBankRepository.AddAsync(bank, ct);
        return ToDto(bank);
    }

    public async Task<QuestionBankDto?> UpdateAsync(Guid id, UpdateQuestionBankRequest request, CancellationToken ct = default)
    {
        var bank = await questionBankRepository.GetByIdAsync(id, ct);
        if (bank is null) return null;

        bank.UpdateDetails(request.Name, request.Description);
        var updated = await questionBankRepository.UpdateAsync(bank, ct);
        if (!updated) return null;

        return ToDto(bank);
    }

    public async Task<QuestionBankDeleteResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return await questionBankRepository.DeleteAsync(id, ct);
    }

    private static QuestionBankDto ToDto(QuestionBank bank)
    {
        return new QuestionBankDto(
            bank.Id,
            bank.Name,
            bank.Description,
            bank.CreatedAt);
    }
}
