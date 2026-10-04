namespace AiViva.Domain.Entities;

public sealed class Role
{
    private Role() { }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public static class Codes
    {
        public const string Admin = "ADMIN";
        public const string Lecturer = "LECTURER";
        public const string Student = "STUDENT";
    }
}
