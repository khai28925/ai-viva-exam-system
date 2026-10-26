namespace AiViva.Domain.Entities;

public sealed class Exam
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Title { get; init; }

    public ExamStatus Status { get; private set; } = ExamStatus.Draft;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public void Publish() => Status = ExamStatus.Published;
}
