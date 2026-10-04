using System.ComponentModel.DataAnnotations;

namespace AiViva.Application.Questions;

public sealed record QuestionDto(
    Guid Id,
    Guid QuestionBankId,
    string Content,
    DateTimeOffset CreatedAt);

public sealed record CreateQuestionRequest(
    [Required(ErrorMessage = "Content is required.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must contain 1 to 2000 characters.")]
    string Content);

public sealed record UpdateQuestionRequest(
    [Required(ErrorMessage = "Content is required.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must contain 1 to 2000 characters.")]
    string Content);
