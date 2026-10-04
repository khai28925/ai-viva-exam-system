using System.ComponentModel.DataAnnotations;

namespace AiViva.Application.QuestionBanks;

public sealed record QuestionBankDto(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record CreateQuestionBankRequest(
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 120 characters.")]
    string Name,
    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    string? Description = null);

public sealed record UpdateQuestionBankRequest(
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 120 characters.")]
    string Name,
    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    string? Description = null);
