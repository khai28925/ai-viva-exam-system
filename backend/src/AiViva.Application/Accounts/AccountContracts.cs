using System.ComponentModel.DataAnnotations;

namespace AiViva.Application.Accounts;

public sealed record AccountDto(Guid Id, string Email, string Role);

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128)] string Password);

public sealed record CreateAccountRequest(
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required] string Role);

public enum CreateAccountStatus
{
    Created,
    DuplicateEmail,
    InvalidRole
}

public sealed record CreateAccountResult(CreateAccountStatus Status, AccountDto? Account = null);
