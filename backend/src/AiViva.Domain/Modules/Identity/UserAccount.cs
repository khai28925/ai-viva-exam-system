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
    public string? FullName { get; private set; }
    public string? StudentCode { get; private set; }

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public Guid RoleId { get; private set; }

    public Role Role { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    public void UpdateProfile(string? fullName, string? studentCode)
    {
        var name = fullName?.Trim();
        var code = studentCode?.Trim().ToUpperInvariant();
        if (name?.Length > 160) throw new ArgumentException("Full name must not exceed 160 characters.", nameof(fullName));
        if (code?.Length > 32) throw new ArgumentException("Student code must not exceed 32 characters.", nameof(studentCode));
        FullName = string.IsNullOrEmpty(name) ? null : name;
        StudentCode = string.IsNullOrEmpty(code) ? null : code;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetEmail(string email)
    {
        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(Email);
    }
}
