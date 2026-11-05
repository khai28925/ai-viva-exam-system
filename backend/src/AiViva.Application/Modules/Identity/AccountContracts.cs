using System.ComponentModel.DataAnnotations;

namespace AiViva.Application.Accounts;

public sealed record AccountDto(Guid Id, string Email, string Role, string? FullName = null, string? StudentCode = null);

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128)] string Password);

public sealed record CreateAccountRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required] string Role,
    [StringLength(160)] string? FullName = null,
    [StringLength(32)] string? StudentCode = null);

public sealed record UpdateAccountProfileRequest(
    [StringLength(160)] string? FullName,
    [StringLength(32)] string? StudentCode);

public enum CreateAccountStatus
{
    Created,
    DuplicateEmail,
    InvalidRole
}

public sealed record CreateAccountResult(CreateAccountStatus Status, AccountDto? Account = null);
