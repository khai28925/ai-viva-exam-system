namespace AiViva.Domain.Entities;

public sealed class UserAccount
{
    private UserAccount() { }

    public UserAccount(string email, Guid roleId)
    {
        Id = Guid.NewGuid();
        SetEmail(email);
        RoleId = roleId;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public Guid RoleId { get; private set; }

    public Role Role { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    private void SetEmail(string email)
    {
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(Email);
    }
}
