using AiViva.Domain.Entities;

namespace AiViva.Application.Questions;

public sealed record QuestionDto(
    Guid Id,
    Guid QuestionBankId,
    string Content,
    QuestionType Type,
    string TypeName,
    DifficultyLevel DifficultyLevel,
    string DifficultyLevelName,
    string? Explanation,
    int Points,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateQuestionRequest(
    Guid QuestionBankId,
    string Content,
    QuestionType Type,
    DifficultyLevel DifficultyLevel,
    string? Explanation,
    int Points = 1);

public sealed record UpdateQuestionRequest(
    string? Content,
    QuestionType? Type,
    DifficultyLevel? DifficultyLevel,
    string? Explanation,
    int? Points);
