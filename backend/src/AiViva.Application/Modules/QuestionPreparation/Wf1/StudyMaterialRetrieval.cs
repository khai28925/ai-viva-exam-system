using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AiViva.Application.Modules.QuestionPreparation.Wf1;

public sealed record TextChunk(int PageStart, int PageEnd, string Text);

public sealed record RetrievableChunk(
    Guid Id, Guid DocumentId, int ChunkIndex, int PageStart, int PageEnd, string Text, float[]? Embedding);

/// <summary>Splits extracted page text into overlapping passages that keep their page numbers.</summary>
public static partial class StudyMaterialChunker
{
    public const int ChunkSize = 1200;
    public const int Overlap = 200;
    public const int MaxChunksPerDocument = 1000;
    private const int MinimumChunkLength = 40;

    public static IReadOnlyList<TextChunk> Chunk(IReadOnlyList<string> pages)
    {
        var chunks = new List<TextChunk>();
        var buffer = new StringBuilder();
        var bufferStart = 0;
        var bufferEnd = 0;

        void Flush()
        {
            var text = buffer.ToString().Trim();
            if (text.Length >= MinimumChunkLength) chunks.Add(new(bufferStart, bufferEnd, text));
            buffer.Clear();
        }

        for (var index = 0; index < pages.Count && chunks.Count < MaxChunksPerDocument; index++)
        {
            var page = index + 1;
            var text = Whitespace().Replace(pages[index] ?? "", " ").Trim();
            if (text.Length == 0) continue;

            // Short pages (typical for slides) are merged so one passage carries enough context.
            if (text.Length <= ChunkSize)
            {
                if (buffer.Length > 0 && buffer.Length + text.Length + 1 > ChunkSize) Flush();
                if (buffer.Length == 0) bufferStart = page;
                bufferEnd = page;
                buffer.Append(text).Append(' ');
                continue;
            }

            Flush();
            for (var start = 0; start < text.Length && chunks.Count < MaxChunksPerDocument;)
            {
                var length = Math.Min(ChunkSize, text.Length - start);
                if (start + length < text.Length)
                {
                    var boundary = text.LastIndexOfAny(['.', '!', '?', ';', ' '], start + length - 1, length / 2);
                    if (boundary > start) length = boundary - start + 1;
                }
                var passage = text.Substring(start, length).Trim();
                if (passage.Length >= MinimumChunkLength) chunks.Add(new(page, page, passage));
                if (start + length >= text.Length) break;
                start += Math.Max(length - Overlap, 1);
            }
        }
        Flush();
        return chunks.Take(MaxChunksPerDocument).ToArray();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

/// <summary>
/// Selects the passages sent to the model. With a topic it ranks passages by embedding
/// similarity (falling back to BM25 keyword scoring when embeddings are unavailable);
/// without a topic it samples passages evenly so the whole syllabus is covered.
/// </summary>
public static class StudyMaterialRetriever
{
    public static IReadOnlyList<RetrievableChunk> Retrieve(
        IReadOnlyList<RetrievableChunk> chunks, string? query, float[]? queryEmbedding, int limit)
    {
        var ordered = chunks.OrderBy(c => c.DocumentId).ThenBy(c => c.ChunkIndex).ToArray();
        if (limit <= 0 || ordered.Length == 0) return [];
        if (ordered.Length <= limit) return ordered;
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Range(0, limit).Select(i => ordered[(int)((long)i * ordered.Length / limit)]).ToArray();

        var lexical = Bm25(ordered, query);
        var maxLexical = lexical.Max();
        var scores = ordered.Select((chunk, i) =>
        {
            var keyword = maxLexical > 0 ? lexical[i] / maxLexical : 0;
            if (queryEmbedding is null || chunk.Embedding is null || chunk.Embedding.Length != queryEmbedding.Length)
                return keyword;
            return 0.75 * Cosine(queryEmbedding, chunk.Embedding) + 0.25 * keyword;
        }).ToArray();
        return ordered.Select((chunk, i) => (chunk, score: scores[i]))
            .OrderByDescending(item => item.score).Take(limit).Select(item => item.chunk)
            .OrderBy(c => c.DocumentId).ThenBy(c => c.ChunkIndex).ToArray();
    }

    public static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static double[] Bm25(IReadOnlyList<RetrievableChunk> chunks, string query)
    {
        const double k1 = 1.2, b = 0.75;
        var documents = chunks.Select(c => Tokenize(c.Text)).ToArray();
        var averageLength = Math.Max(1, documents.Average(d => d.Count));
        var terms = Tokenize(query).Distinct().ToArray();
        var scores = new double[chunks.Count];
        foreach (var term in terms)
        {
            var containing = documents.Count(d => d.Contains(term));
            if (containing == 0) continue;
            var idf = Math.Log(1 + (documents.Length - containing + 0.5) / (containing + 0.5));
            for (var i = 0; i < documents.Length; i++)
            {
                var frequency = documents[i].Count(token => token == term);
                if (frequency == 0) continue;
                scores[i] += idf * frequency * (k1 + 1) /
                    (frequency + k1 * (1 - b + b * documents[i].Count / averageLength));
            }
        }
        return scores;
    }

    // Diacritics are folded so "phan tich" still matches "phân tích" in Vietnamese material.
    private static List<string> Tokenize(string text)
    {
        var folded = new StringBuilder();
        foreach (var c in text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            folded.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return folded.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 1).ToList();
    }
}
