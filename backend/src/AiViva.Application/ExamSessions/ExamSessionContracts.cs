using System.ComponentModel.DataAnnotations;
using EntitiesRole = AiViva.Domain.Entities.Role;

namespace AiViva.Application.ExamSessions;

public sealed record ExamSessionDto(
    Guid Id, string Title, Guid QuestionBankId, string QuestionBankName,
    string Status, Guid CreatedBy, DateTimeOffset CreatedAt, int ParticipantCount);

public sealed record ExamQuestionDto(Guid Id, string Content);

public sealed record ExamAttemptDto(
    Guid Id, Guid ExamSessionId, Guid StudentId, DateTimeOffset StartedAt,
    IReadOnlyCollection<ExamQuestionDto> Questions);

public sealed record SaveExamSessionRequest : IValidatableObject
{
    public SaveExamSessionRequest(string title, Guid questionBankId, string status)
    {
        Title = title?.Trim() ?? string.Empty;
        QuestionBankId = questionBankId;
        Status = status;
    }

    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Title { get; }

    public Guid QuestionBankId { get; }

    [Required]
    [RegularExpression("^(Draft|Open|Closed)$")]
    public string Status { get; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (QuestionBankId == Guid.Empty)
            yield return new ValidationResult("Question bank is required.", [nameof(QuestionBankId)]);
    }
}

public sealed record ExamActor(Guid Id, string Role)
{
    public bool CanManage => Id != Guid.Empty && Role is EntitiesRole.Codes.Admin or EntitiesRole.Codes.Lecturer;
    public bool CanManageSession(Guid createdBy) => CanManage && (Role == EntitiesRole.Codes.Admin || Id == createdBy);
    public bool CanJoin => Id != Guid.Empty && Role == EntitiesRole.Codes.Student;
}

public enum ExamSessionError { None, Invalid, NotFound, Forbidden, Conflict }

public sealed record ExamSessionResult<T>(T? Value, ExamSessionError Error = ExamSessionError.None, string? Detail = null)
{
    public static ExamSessionResult<T> Fail(ExamSessionError error, string detail) => new(default, error, detail);
}
