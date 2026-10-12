namespace AiViva.Infrastructure.Persistence.Models;

// Persistence models for the full viva workflow. The MVP API still uses the
// Domain entities for accounts, question banks, sessions and attempts.
public sealed class ExamSchedule
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ExamScheduleStudent
{
    public Guid ExamScheduleId { get; set; }
    public Guid StudentId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
}

public sealed class Rubric
{
    public Guid Id { get; set; }
    public Guid QuestionBankId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RubricCriterion
{
    public Guid Id { get; set; }
    public Guid RubricId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MaxScore { get; set; }
    public decimal Weight { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ExpectedKeyPoint
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ExamSessionQuestion
{
    public Guid Id { get; set; }
    public Guid ExamSessionId { get; set; }
    public Guid QuestionBankId { get; set; }
    public Guid QuestionId { get; set; }
    public string ContentSnapshot { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsRequired { get; set; } = true;
}

public sealed class AttemptQuestion
{
    public Guid Id { get; set; }
    public Guid ExamAttemptId { get; set; }
    public Guid ExamSessionId { get; set; }
    public Guid? ExamSessionQuestionId { get; set; }
    public Guid? ParentAttemptQuestionId { get; set; }
    public string ContentSnapshot { get; set; } = string.Empty;
    public string Source { get; set; } = "Bank";
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Answer
{
    public Guid Id { get; set; }
    public Guid ExamAttemptId { get; set; }
    public Guid AttemptQuestionId { get; set; }
    public string? AudioObjectKey { get; set; }
    public string? TextContent { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Transcript
{
    public Guid Id { get; set; }
    public Guid AnswerId { get; set; }
    public string Content { get; set; } = string.Empty;
    public decimal? Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AiEvaluation
{
    public Guid Id { get; set; }
    public Guid AnswerId { get; set; }
    public decimal Score { get; set; }
    public string? Analysis { get; set; }
    public string? Decision { get; set; }
    public string? MatchedKeyPoints { get; set; }
    public string? MissingKeyPoints { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LecturerEvaluation
{
    public Guid Id { get; set; }
    public Guid AnswerId { get; set; }
    public Guid LecturerId { get; set; }
    public decimal Score { get; set; }
    public string? Comments { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class Report
{
    public Guid Id { get; set; }
    public Guid ExamAttemptId { get; set; }
    public decimal AiTotalScore { get; set; }
    public decimal LecturerTotalScore { get; set; }
    public decimal FinalScore { get; set; }
    public string? Feedback { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ExamActivityLog
{
    public Guid Id { get; set; }
    public Guid ExamAttemptId { get; set; }
    public Guid UserId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ServiceInteractionLog
{
    public Guid Id { get; set; }
    public Guid ExamAttemptId { get; set; }
    public Guid? AnswerId { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset RequestTime { get; set; }
    public DateTimeOffset? ResponseTime { get; set; }
    public int? LatencyMs { get; set; }
    public string? ErrorMessage { get; set; }
}
