namespace AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;

public sealed class StudyDocument
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = [];
    public Guid UploadedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string IndexStatus { get; set; } = "Pending";
    public int PageCount { get; set; }
    public string? EmbeddingModel { get; set; }
    public string? IndexError { get; set; }
    public DateTimeOffset? IndexedAt { get; set; }
}

/// <summary>A retrievable passage of a study document (RAG index).</summary>
public sealed class StudyDocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid SubjectId { get; set; }
    public int ChunkIndex { get; set; }
    public int PageStart { get; set; }
    public int PageEnd { get; set; }
    public string Content { get; set; } = string.Empty;
    // float32 little-endian vector; null when the embedding provider was unavailable.
    public byte[]? Embedding { get; set; }
}

public sealed class AiGenerationRun
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public Guid QuestionBankId { get; set; }
    public int RequestedQuestionCount { get; set; }
    public string? Topic { get; set; }
    public int RetrievedChunkCount { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    public string? ErrorMessage { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class AiQuestionCandidate
{
    public Guid Id { get; set; }
    public Guid GenerationRunId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid QuestionBankId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string BloomLevel { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public string ExpectedKeyPointsJson { get; set; } = "[]";
    public string SourceRefsJson { get; set; } = "[]";
    public string Status { get; set; } = "Pending";
    public Guid? ApprovedQuestionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public sealed class RubricSet
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = "Draft";
    public string Source { get; set; } = "Manual";
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class SubjectRubric
{
    public Guid Id { get; set; }
    public Guid RubricSetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal WeightPercent { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class SubjectRubricCriterion
{
    public Guid Id { get; set; }
    public Guid SubjectRubricId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MaxScore { get; set; }
    public int DisplayOrder { get; set; }
}
