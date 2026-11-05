namespace AiViva.Application.Exams;

public sealed record ExamDto(
    Guid Id,
    string Title,
    string Status,
    DateTimeOffset CreatedAt);
