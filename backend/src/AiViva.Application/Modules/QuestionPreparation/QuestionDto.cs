using System.ComponentModel.DataAnnotations;

namespace AiViva.Application.Questions;

public sealed record QuestionDto(
    Guid Id,
    Guid QuestionBankId,
    string Content,
    DateTimeOffset CreatedAt,
    string? BloomLevel = null,
    int? DifficultyLevel = null);

public sealed record CreateQuestionRequest
{
    public CreateQuestionRequest(string content, string? bloomLevel = null, int? difficultyLevel = null)
    {
        Content = content?.Trim() ?? string.Empty;
        BloomLevel = string.IsNullOrWhiteSpace(bloomLevel) ? null : bloomLevel.Trim();
        DifficultyLevel = difficultyLevel;
    }

    [Required(ErrorMessage = "Content is required.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must contain 1 to 2000 characters.")]
    public string Content { get; }

    [RegularExpression(QuestionClassification.BloomPattern, ErrorMessage = QuestionClassification.BloomMessage)]
    public string? BloomLevel { get; }

    [Range(1, 5, ErrorMessage = "Difficulty level must be from 1 to 5.")]
    public int? DifficultyLevel { get; }
}

public sealed record UpdateQuestionRequest
{
    public UpdateQuestionRequest(string content, string? bloomLevel = null, int? difficultyLevel = null)
    {
        Content = content?.Trim() ?? string.Empty;
        BloomLevel = string.IsNullOrWhiteSpace(bloomLevel) ? null : bloomLevel.Trim();
        DifficultyLevel = difficultyLevel;
    }

    [Required(ErrorMessage = "Content is required.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must contain 1 to 2000 characters.")]
    public string Content { get; }

    [RegularExpression(QuestionClassification.BloomPattern, ErrorMessage = QuestionClassification.BloomMessage)]
    public string? BloomLevel { get; }

    [Range(1, 5, ErrorMessage = "Difficulty level must be from 1 to 5.")]
    public int? DifficultyLevel { get; }
}

public static class QuestionClassification
{
    public const string BloomPattern = "^(Remember|Understand|Apply|Analyze)$";
    public const string BloomMessage = "Bloom level must be Remember, Understand, Apply, or Analyze.";
}
