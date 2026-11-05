using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiViva.Application.Modules.QuestionPreparation.Wf1;
using Microsoft.Extensions.Configuration;

namespace AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;

public sealed class GeminiEmbeddingGenerator(HttpClient httpClient, IConfiguration configuration) : IEmbeddingGenerator
{
    private const int BatchSize = 100;
    private const int Dimensions = 768;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string? apiKey = configuration["Gemini:ApiKey"] ??
        Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    public string Model { get; } = configuration["Gemini:EmbeddingModel"] ?? "gemini-embedding-001";

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts, bool query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiGenerationException(AiGenerationFailure.NotConfigured,
                "Dịch vụ AI chưa được cấu hình.");
        var result = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(BatchSize))
        {
            var payload = new
            {
                requests = batch.Select(text => new
                {
                    model = $"models/{Model}",
                    content = new { parts = new[] { new { text } } },
                    taskType = query ? "RETRIEVAL_QUERY" : "RETRIEVAL_DOCUMENT",
                    outputDimensionality = Dimensions
                })
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Model}:batchEmbedContents")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            request.Headers.Add("x-goog-api-key", apiKey);
            HttpResponseMessage response;
            try { response = await httpClient.SendAsync(request, cancellationToken); }
            catch (HttpRequestException exception)
            {
                throw new AiGenerationException(AiGenerationFailure.ProviderUnavailable,
                    "Không kết nối được dịch vụ tìm kiếm ngữ nghĩa.", exception);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiGenerationException(AiGenerationFailure.ProviderUnavailable,
                    "Dịch vụ tìm kiếm ngữ nghĩa phản hồi quá lâu.", exception);
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new AiGenerationException(AiGenerationFailure.RateLimited, "Dịch vụ tìm kiếm ngữ nghĩa đang quá tải lượt gọi.");
                if (!response.IsSuccessStatusCode)
                    throw new AiGenerationException(AiGenerationFailure.ProviderUnavailable,
                        $"Dịch vụ tìm kiếm ngữ nghĩa trả lỗi HTTP {(int)response.StatusCode}.");
                try
                {
                    using var json = await JsonDocument.ParseAsync(
                        await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                    var vectors = json.RootElement.GetProperty("embeddings").EnumerateArray()
                        .Select(item => item.GetProperty("values").EnumerateArray().Select(v => v.GetSingle()).ToArray())
                        .ToArray();
                    if (vectors.Length != batch.Length || vectors.Any(v => v.Length == 0))
                        throw new AiGenerationException(AiGenerationFailure.InvalidResponse,
                            "Dịch vụ tìm kiếm ngữ nghĩa trả về số vector không khớp.");
                    result.AddRange(vectors);
                }
                catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
                {
                    throw new AiGenerationException(AiGenerationFailure.InvalidResponse,
                        "Dịch vụ tìm kiếm ngữ nghĩa trả về phản hồi không đọc được.", exception);
                }
            }
        }
        return result;
    }
}
