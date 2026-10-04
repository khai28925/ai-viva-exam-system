namespace AiViva.Application.QuestionBanks;

public sealed record QuestionBankDto(
    Guid Id,
    string Title,
    string? Description,
    Guid SubjectId,
    int QuestionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateQuestionBankRequest(
    string Title,
    string? Description,
    Guid SubjectId);

public sealed record UpdateQuestionBankRequest(
    string? Title,
    string? Description);
