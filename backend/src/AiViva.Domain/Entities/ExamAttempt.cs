namespace AiViva.Domain.Entities;

public sealed class ExamAttempt
{
    private ExamAttempt() { }

    public ExamAttempt(Guid examSessionId, Guid studentId)
    {
        if (examSessionId == Guid.Empty)
            throw new ArgumentException("Exam session is required.", nameof(examSessionId));
        if (studentId == Guid.Empty)
            throw new ArgumentException("Student is required.", nameof(studentId));
        Id = Guid.NewGuid();
        ExamSessionId = examSessionId;
        StudentId = studentId;
        StartedAt = DateTimeOffset.UtcNow;
        CreatedAt = StartedAt;
    }

    public Guid Id { get; private set; }
    public Guid ExamSessionId { get; private set; }
    public Guid StudentId { get; private set; }
    public string Status { get; private set; } = "InProgress";
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
