using System.ComponentModel.DataAnnotations;

namespace AiViva.Application.QuestionBanks;

public sealed record QuestionBankDto(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record CreateQuestionBankRequest
{
    public CreateQuestionBankRequest(string name, string? description = null)
    {
        Name = name?.Trim() ?? string.Empty;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 120 characters.")]
    public string Name { get; }
    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string? Description { get; }
}

public sealed record UpdateQuestionBankRequest
{
    public UpdateQuestionBankRequest(string name, string? description = null)
    {
        Name = name?.Trim() ?? string.Empty;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 120 characters.")]
    public string Name { get; }
    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string? Description { get; }
}
