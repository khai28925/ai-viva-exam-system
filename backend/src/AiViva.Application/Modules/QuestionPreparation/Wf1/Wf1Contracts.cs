using System.ComponentModel.DataAnnotations;

namespace AiViva.Application.Modules.QuestionPreparation.Wf1;

public sealed record Wf1Actor(Guid Id, string Role)
{
    public bool IsAdmin => Role == "ADMIN";
}

public sealed record SubjectSummaryDto(
    Guid Id, string Name, string? Description, Guid? CreatedBy, DateTimeOffset CreatedAt,
    IReadOnlyCollection<SubjectBankDto> QuestionBanks);
public sealed record SubjectBankDto(Guid Id, string Name, string? Description);
/// <param name="IndexStatus">Pending, Indexed, NoText (scanned PDF, sent whole to the model) or Failed.</param>
/// <param name="SemanticSearch">True when passages have embeddings; otherwise retrieval uses keyword ranking.</param>
public sealed record StudyDocumentDto(
    Guid Id, string FileName, string MimeType, long SizeBytes, DateTimeOffset CreatedAt,
    string IndexStatus = "Pending", int PageCount = 0, int ChunkCount = 0, bool SemanticSearch = false,
    string? IndexError = null);
public sealed record CandidateSourceDto(string FileName, int PageStart, int PageEnd, string Excerpt);
public sealed record AiQuestionCandidateDto(
    Guid Id, Guid QuestionBankId, string Content, string BloomLevel, int DifficultyLevel,
    string Rationale, IReadOnlyList<string> ExpectedKeyPoints, string Status, Guid? ApprovedQuestionId,
    IReadOnlyList<CandidateSourceDto>? Sources = null, string? Topic = null);
public sealed record SubjectRubricCriterionDto(Guid Id, string Name, string? Description, decimal MaxScore, int DisplayOrder);
public sealed record SubjectRubricDto(
    Guid Id, string Name, string? Description, decimal WeightPercent, int DisplayOrder,
    IReadOnlyCollection<SubjectRubricCriterionDto> Criteria);
public sealed record RubricSetDto(
    Guid Id, int Version, string Status, string Source, DateTimeOffset CreatedAt,
    IReadOnlyCollection<SubjectRubricDto> Rubrics);
public sealed record SubjectWorkspaceDto(
    SubjectSummaryDto Subject, IReadOnlyCollection<StudyDocumentDto> Documents,
    IReadOnlyCollection<AiQuestionCandidateDto> QuestionCandidates, RubricSetDto? DraftRubricSet,
    RubricSetDto? ApprovedRubricSet);

public sealed record CreateSubjectRequest(
    [Required, StringLength(120, MinimumLength = 1)] string Name,
    [StringLength(500)] string? Description,
    [Required, StringLength(120, MinimumLength = 1)] string QuestionBankName);
public sealed record CreateSubjectQuestionBankRequest(
    [Required, StringLength(120, MinimumLength = 1)] string Name,
    [StringLength(500)] string? Description);

/// <param name="Topic">Optional chapter/topic; retrieval then ranks passages by relevance to it.</param>
/// <param name="IncludeRubrics">False keeps the lecturer's current rubric draft untouched.</param>
public sealed record GenerateWf1ContentRequest(
    Guid QuestionBankId, [Range(1, 50)] int QuestionCount,
    [StringLength(200)] string? Topic = null, bool IncludeRubrics = true);
public sealed record UpdateAiQuestionCandidateRequest(
    [Required, StringLength(2000, MinimumLength = 1)] string Content,
    [Required] string BloomLevel,
    [Range(1, 5)] int DifficultyLevel,
    [StringLength(1000)] string Rationale,
    [MinLength(1), MaxLength(5)] IReadOnlyList<string> ExpectedKeyPoints);
public sealed record ReviewAiQuestionCandidateRequest(bool Approve);
public sealed record SaveSubjectRubricCriterionRequest(
    [Required, StringLength(120, MinimumLength = 1)] string Name,
    [StringLength(500)] string? Description,
    decimal MaxScore);
public sealed record SaveSubjectRubricRequest(
    [Required, StringLength(120, MinimumLength = 1)] string Name,
    [StringLength(500)] string? Description,
    decimal WeightPercent,
    IReadOnlyList<SaveSubjectRubricCriterionRequest> Criteria);
public sealed record SaveRubricSetRequest(IReadOnlyList<SaveSubjectRubricRequest> Rubrics);

public enum Wf1Failure { NotFound, Forbidden, Invalid, Conflict, AiUnavailable, AiRateLimited }

public sealed class Wf1Exception(Wf1Failure failure, string message, string? code = null) : Exception(message)
{
    public Wf1Failure Failure { get; } = failure;
    public string? Code { get; } = code;
}

public interface IWf1PreparationService
{
    Task<IReadOnlyCollection<SubjectSummaryDto>> GetSubjectsAsync(Wf1Actor actor, CancellationToken ct = default);
    Task<SubjectWorkspaceDto> GetWorkspaceAsync(Guid subjectId, Wf1Actor actor, CancellationToken ct = default);
    Task<SubjectSummaryDto> CreateSubjectAsync(CreateSubjectRequest request, Wf1Actor actor, CancellationToken ct = default);
    Task<SubjectBankDto> CreateQuestionBankAsync(Guid subjectId, CreateSubjectQuestionBankRequest request, Wf1Actor actor, CancellationToken ct = default);
    Task<StudyDocumentDto> UploadDocumentAsync(Guid subjectId, string fileName, string mimeType, byte[] content, Wf1Actor actor, CancellationToken ct = default);
    Task DeleteDocumentAsync(Guid subjectId, Guid documentId, Wf1Actor actor, CancellationToken ct = default);
    Task<StudyDocumentDto> ReindexDocumentAsync(Guid subjectId, Guid documentId, Wf1Actor actor, CancellationToken ct = default);
    Task<SubjectWorkspaceDto> GenerateAsync(Guid subjectId, GenerateWf1ContentRequest request, Wf1Actor actor, CancellationToken ct = default);
    Task<AiQuestionCandidateDto> UpdateCandidateAsync(Guid subjectId, Guid candidateId, UpdateAiQuestionCandidateRequest request, Wf1Actor actor, CancellationToken ct = default);
    Task<AiQuestionCandidateDto> ReviewCandidateAsync(Guid subjectId, Guid candidateId, ReviewAiQuestionCandidateRequest request, Wf1Actor actor, CancellationToken ct = default);
    Task<RubricSetDto> SaveDraftRubricSetAsync(Guid subjectId, SaveRubricSetRequest request, Wf1Actor actor, CancellationToken ct = default);
    Task<RubricSetDto> ApproveRubricSetAsync(Guid subjectId, Guid rubricSetId, Wf1Actor actor, CancellationToken ct = default);
}
