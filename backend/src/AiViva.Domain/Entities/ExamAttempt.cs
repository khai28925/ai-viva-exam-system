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
    }

    public Guid Id { get; private set; }
    public Guid ExamSessionId { get; private set; }
    public Guid StudentId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
}
