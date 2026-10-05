using System.ComponentModel.DataAnnotations;

namespace AiViva.Application.Questions;

public sealed record QuestionDto(
    Guid Id,
    Guid QuestionBankId,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record CreateQuestionRequest
{
    public CreateQuestionRequest(string content)
    {
        Content = content?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Content is required.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must contain 1 to 2000 characters.")]
    public string Content { get; }
}

public sealed record UpdateQuestionRequest
{
    public UpdateQuestionRequest(string content)
    {
        Content = content?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Content is required.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must contain 1 to 2000 characters.")]
    public string Content { get; }
}
