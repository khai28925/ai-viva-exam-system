namespace AiViva.Domain.Entities;

public sealed class Subject
{
    private Subject() { }

    public Subject(string name, string? description, Guid? createdBy)
    {
        if (createdBy == Guid.Empty) throw new ArgumentException("Creator must not be empty.", nameof(createdBy));
        Id = Guid.NewGuid();
        CreatedBy = createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdateDetails(name, description);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void UpdateDetails(string name, string? description)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrEmpty(normalizedName) || normalizedName.Length > 120)
            throw new ArgumentException("Name must contain 1 to 120 characters.", nameof(name));
        var normalizedDescription = description?.Trim();
        if (normalizedDescription?.Length > 500)
            throw new ArgumentException("Description must not exceed 500 characters.", nameof(description));
        Name = normalizedName;
        Description = string.IsNullOrEmpty(normalizedDescription) ? null : normalizedDescription;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
