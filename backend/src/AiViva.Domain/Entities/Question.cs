using System;

namespace AiViva.Domain.Entities;

public sealed class Question
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid QuestionBankId { get; init; }
    public required string Content { get; set; }
    public QuestionType Type { get; set; }
    public DifficultyLevel DifficultyLevel { get; set; }
    public string? Explanation { get; set; }
    public int Points { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    public void MarkDeleted() => IsDeleted = true;
}
