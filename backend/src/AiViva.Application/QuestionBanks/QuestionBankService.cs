using AiViva.Application.Abstractions;
using AiViva.Domain.Entities;

namespace AiViva.Application.QuestionBanks;

public sealed class QuestionBankService(IQuestionBankRepository questionBankRepository, IQuestionRepository questionRepository) : IQuestionBankService
{
    public async Task<QuestionBankDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var bank = await questionBankRepository.GetByIdAsync(id, ct);
        if (bank is null) return null;

        var questions = await questionRepository.GetByQuestionBankIdAsync(id, ct);
        return ToDto(bank, questions.Count);
    }

    public async Task<IReadOnlyCollection<QuestionBankDto>> GetAllAsync(CancellationToken ct = default)
    {
        var banks = await questionBankRepository.GetAllAsync(ct);
        var dtos = new List<QuestionBankDto>(banks.Count);

        foreach (var bank in banks)
        {
            var questions = await questionRepository.GetByQuestionBankIdAsync(bank.Id, ct);
            dtos.Add(ToDto(bank, questions.Count));
        }

        return dtos.AsReadOnly();
    }

    public async Task<QuestionBankDto> CreateAsync(CreateQuestionBankRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title cannot be empty.", nameof(request.Title));

        var bank = new QuestionBank
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            SubjectId = request.SubjectId,
            CreatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        await questionBankRepository.AddAsync(bank, ct);
        return ToDto(bank, 0);
    }

    public async Task<QuestionBankDto?> UpdateAsync(Guid id, UpdateQuestionBankRequest request, CancellationToken ct = default)
    {
        var bank = await questionBankRepository.GetByIdAsync(id, ct);
        if (bank is null) return null;

        if (request.Title is not null) bank.Title = request.Title;
        if (request.Description is not null) bank.Description = request.Description;
        
        bank.UpdatedAt = DateTimeOffset.UtcNow;

        await questionBankRepository.UpdateAsync(bank, ct);

        var questions = await questionRepository.GetByQuestionBankIdAsync(id, ct);
        return ToDto(bank, questions.Count);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return await questionBankRepository.DeleteAsync(id, ct);
    }

    private static QuestionBankDto ToDto(QuestionBank bank, int questionCount)
    {
        return new QuestionBankDto(
            bank.Id,
            bank.Title,
            bank.Description,
            bank.SubjectId,
            questionCount,
            bank.CreatedAt,
            bank.UpdatedAt);
    }
}
