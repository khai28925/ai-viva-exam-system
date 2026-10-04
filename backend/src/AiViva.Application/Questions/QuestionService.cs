using AiViva.Application.Abstractions;
using AiViva.Domain.Entities;

namespace AiViva.Application.Questions;

public sealed class QuestionService(IQuestionBankRepository questionBankRepository) : IQuestionService
{
    public async Task<IReadOnlyCollection<QuestionDto>?> GetByQuestionBankIdAsync(Guid questionBankId, CancellationToken ct = default)
    {
        var bank = await questionBankRepository.GetByIdAsync(questionBankId, ct);
        if (bank is null) return null;

        var questions = await questionBankRepository.GetQuestionsAsync(questionBankId, ct);
        return questions.Select(ToDto).ToArray();
    }

    public async Task<QuestionDto?> GetByIdAsync(Guid questionBankId, Guid questionId, CancellationToken ct = default)
    {
        var question = await questionBankRepository.GetQuestionByIdAsync(questionBankId, questionId, ct);
        return question is null ? null : ToDto(question);
    }

    public async Task<QuestionDto?> CreateAsync(Guid questionBankId, CreateQuestionRequest request, CancellationToken ct = default)
    {
        var bank = await questionBankRepository.GetByIdAsync(questionBankId, ct);
        if (bank is null) return null;

        var question = new Question(questionBankId, request.Content);
        var added = await questionBankRepository.AddQuestionAsync(question, ct);
        if (!added) return null;

        return ToDto(question);
    }

    public async Task<QuestionDto?> UpdateAsync(Guid questionBankId, Guid questionId, UpdateQuestionRequest request, CancellationToken ct = default)
    {
        var question = await questionBankRepository.GetQuestionByIdAsync(questionBankId, questionId, ct);
        if (question is null) return null;

        question.UpdateContent(request.Content);
        var updated = await questionBankRepository.UpdateQuestionAsync(question, ct);
        if (!updated) return null;

        return ToDto(question);
    }

    public async Task<bool> DeleteAsync(Guid questionBankId, Guid questionId, CancellationToken ct = default)
    {
        return await questionBankRepository.DeleteQuestionAsync(questionBankId, questionId, ct);
    }

    private static QuestionDto ToDto(Question question)
    {
        return new QuestionDto(
            question.Id,
            question.QuestionBankId,
            question.Content,
            question.CreatedAt);
    }
}
