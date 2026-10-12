namespace AiViva.Domain.Entities;

public sealed class Role
{
    private Role() { }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;
    public string? Name { get; private set; }
    public string? Description { get; private set; }

    public static class Codes
    {
        public const string Admin = "ADMIN";
        public const string Lecturer = "LECTURER";
        public const string Student = "STUDENT";
    }
}
