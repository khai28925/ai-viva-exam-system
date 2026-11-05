namespace AiViva.Application.Modules.QuestionPreparation.Wf1;

public sealed record AiSourceDocument(string FileName, string MimeType, byte[] Content);

/// <summary>A retrieved excerpt of the teaching material, cited by its label (e.g. "S3").</summary>
public sealed record AiSourceChunk(string Label, string FileName, int PageStart, int PageEnd, string Text);

/// <summary>
/// Retrieved excerpts (RAG) plus whole PDFs that have no text layer (scanned slides),
/// which are attached so the model can still read them.
/// </summary>
public sealed record AiGroundingContext(
    IReadOnlyList<AiSourceChunk> Chunks, IReadOnlyCollection<AiSourceDocument> Documents)
{
    public bool IsEmpty => Chunks.Count == 0 && Documents.Count == 0;
}

public sealed record AiQuestionSuggestion(
    string Content, string BloomLevel, int DifficultyLevel, string Rationale,
    IReadOnlyList<string> ExpectedKeyPoints, IReadOnlyList<string>? SourceLabels = null);
public sealed record AiCriterionSuggestion(string Name, string? Description, decimal MaxScore);
public sealed record AiRubricSuggestion(
    string Name, string? Description, decimal WeightPercent,
    IReadOnlyList<AiCriterionSuggestion> Criteria);

public enum AiGenerationFailure { NotConfigured, RateLimited, ProviderUnavailable, InvalidResponse }

public sealed class AiGenerationException(AiGenerationFailure failure, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public AiGenerationFailure Failure { get; } = failure;
}

public interface IAiContentGenerator
{
    /// <summary>The configured model, or the fallback model that served the latest request.</summary>
    string Model { get; }

    Task<IReadOnlyList<AiQuestionSuggestion>> GenerateQuestionsAsync(
        string subjectName, string? topic, AiGroundingContext grounding, int count,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiRubricSuggestion>> GenerateRubricsAsync(
        string subjectName, AiGroundingContext grounding,
        CancellationToken cancellationToken = default);
}

public interface IEmbeddingGenerator
{
    string Model { get; }

    /// <param name="query">True for a search query, false for document passages.</param>
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts, bool query, CancellationToken cancellationToken = default);
}

public interface IDocumentTextExtractor
{
    /// <summary>Returns the text of each page; empty strings for pages without a text layer.</summary>
    IReadOnlyList<string> ExtractPages(byte[] content);
}
