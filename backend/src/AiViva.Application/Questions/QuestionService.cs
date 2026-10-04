using AiViva.Application.Abstractions;
using AiViva.Domain.Entities;

namespace AiViva.Application.Questions;

public sealed class QuestionService(IQuestionRepository questionRepository, IQuestionBankRepository questionBankRepository) : IQuestionService
{
    public async Task<QuestionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var question = await questionRepository.GetByIdAsync(id, ct);
        if (question is null) return null;

        return ToDto(question);
    }

    public async Task<IReadOnlyCollection<QuestionDto>> GetAllAsync(CancellationToken ct = default)
    {
        var questions = await questionRepository.GetAllAsync(ct);
        return questions.Select(ToDto).ToArray();
    }

    public async Task<IReadOnlyCollection<QuestionDto>> GetByQuestionBankIdAsync(Guid questionBankId, CancellationToken ct = default)
    {
        var questions = await questionRepository.GetByQuestionBankIdAsync(questionBankId, ct);
        return questions.Select(ToDto).ToArray();
    }

    public async Task<QuestionDto> CreateAsync(CreateQuestionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Content cannot be empty.", nameof(request.Content));

        var bank = await questionBankRepository.GetByIdAsync(request.QuestionBankId, ct);
        if (bank is null)
            throw new ArgumentException("Question bank not found.", nameof(request.QuestionBankId));

        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuestionBankId = request.QuestionBankId,
            Content = request.Content,
            Type = request.Type,
            DifficultyLevel = request.DifficultyLevel,
            Explanation = request.Explanation,
            Points = request.Points,
            CreatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        await questionRepository.AddAsync(question, ct);
        return ToDto(question);
    }

    public async Task<QuestionDto?> UpdateAsync(Guid id, UpdateQuestionRequest request, CancellationToken ct = default)
    {
        var question = await questionRepository.GetByIdAsync(id, ct);
        if (question is null) return null;

        if (request.Content is not null) question.Content = request.Content;
        if (request.Type.HasValue) question.Type = request.Type.Value;
        if (request.DifficultyLevel.HasValue) question.DifficultyLevel = request.DifficultyLevel.Value;
        if (request.Explanation is not null) question.Explanation = request.Explanation;
        if (request.Points.HasValue) question.Points = request.Points.Value;

        question.UpdatedAt = DateTimeOffset.UtcNow;

        await questionRepository.UpdateAsync(question, ct);
        return ToDto(question);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return await questionRepository.DeleteAsync(id, ct);
    }

    private static QuestionDto ToDto(Question question)
    {
        return new QuestionDto(
            question.Id,
            question.QuestionBankId,
            question.Content,
            question.Type,
            question.Type.ToString(),
            question.DifficultyLevel,
            question.DifficultyLevel.ToString(),
            question.Explanation,
            question.Points,
            question.CreatedAt,
            question.UpdatedAt);
    }
}
