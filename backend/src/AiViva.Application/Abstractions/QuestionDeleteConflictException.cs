namespace AiViva.Application.Abstractions;

public sealed class QuestionDeleteConflictException(string code) : Exception("Question is still referenced by exam preparation.")
{
    public string Code { get; } = code;
}
