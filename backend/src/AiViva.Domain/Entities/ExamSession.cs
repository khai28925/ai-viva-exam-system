namespace AiViva.Domain.Entities;

public sealed class ExamSession
{
    private ExamSession() { }

    public ExamSession(string title, Guid questionBankId, string status, Guid createdBy)
    {
        if (createdBy == Guid.Empty) throw new ArgumentException("Creator is required.", nameof(createdBy));
        Id = Guid.NewGuid();
        CreatedBy = createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdateDetails(title, questionBankId, status);
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid QuestionBankId { get; private set; }
    public Guid? ScheduleId { get; private set; }
    public string Status { get; private set; } = Statuses.Draft;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void UpdateDetails(string title, Guid questionBankId, string status)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrEmpty(normalizedTitle) || normalizedTitle.Length > 120)
            throw new ArgumentException("Title must contain 1 to 120 characters.", nameof(title));
        if (questionBankId == Guid.Empty)
            throw new ArgumentException("Question bank is required.", nameof(questionBankId));
        if (!Statuses.IsValid(status))
            throw new ArgumentException("Status must be Draft, Open, or Closed.", nameof(status));

        Title = normalizedTitle;
        QuestionBankId = questionBankId;
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static class Statuses
    {
        public const string Draft = "Draft";
        public const string Open = "Open";
        public const string Closed = "Closed";

        public static bool IsValid(string? status) => status is Draft or Open or Closed;
    }
}
