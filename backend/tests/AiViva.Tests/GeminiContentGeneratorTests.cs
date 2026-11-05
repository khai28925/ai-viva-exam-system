using System.Net;
using System.Text;
using System.Text.Json;
using AiViva.Application.Modules.QuestionPreparation.Wf1;
using AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiViva.Tests;

public sealed class GeminiContentGeneratorTests
{
    [Fact]
    public async Task Sends_pdf_as_inline_document_and_parses_structured_questions()
    {
        const string structured =
            """
            {"questions":[{"content":"Explain dependency injection.","bloomLevel":"Understand","difficultyLevel":2,"rationale":"Tests understanding.","expectedKeyPoints":["Loose coupling"],"sourceLabels":["D1"]}]}
            """;
        var handler = new CaptureHandler(Interaction(structured));
        var generator = new GeminiContentGenerator(Http(handler), Configuration());

        var result = await generator.GenerateQuestionsAsync("Software Design", null,
            new AiGroundingContext([], [new AiSourceDocument("lesson.pdf", "application/pdf", [1, 2, 3])]), 1);

        result.Should().ContainSingle();
        result[0].BloomLevel.Should().Be("Understand");
        result[0].SourceLabels.Should().Equal("D1");
        handler.Uri.Should().Be("https://generativelanguage.googleapis.com/v1beta/interactions");
        handler.ApiKey.Should().Be("test-key");
        using var payload = JsonDocument.Parse(handler.Body!);
        payload.RootElement.GetProperty("store").GetBoolean().Should().BeFalse();
        payload.RootElement.GetProperty("response_format").GetProperty("mime_type")
            .GetString().Should().Be("application/json");
        var document = payload.RootElement.GetProperty("input")[1];
        document.GetProperty("type").GetString().Should().Be("document");
        document.GetProperty("data").GetString().Should().Be(Convert.ToBase64String([1, 2, 3]));
    }

    [Fact]
    public async Task Sends_retrieved_excerpts_with_labels_and_keeps_only_known_citations()
    {
        const string structured =
            """
            {"questions":[{"content":"Why normalise a schema?","bloomLevel":"Analyze","difficultyLevel":3,"rationale":"Trade-offs.","expectedKeyPoints":["Less redundancy"],"sourceLabels":["s2"," S2","S9","D1"]}]}
            """;
        var handler = new CaptureHandler(Interaction(structured));
        var generator = new GeminiContentGenerator(Http(handler), Configuration());

        var result = await generator.GenerateQuestionsAsync("Databases", "chuẩn hóa", new AiGroundingContext(
            [
                new AiSourceChunk("S1", "db.pdf", 1, 1, "Tables store rows."),
                new AiSourceChunk("S2", "db.pdf", 4, 5, "Normalization removes redundancy.")
            ], []), 1);

        result[0].SourceLabels.Should().Equal("S2");
        using var payload = JsonDocument.Parse(handler.Body!);
        var input = payload.RootElement.GetProperty("input");
        input.GetArrayLength().Should().Be(1);
        var text = input[0].GetProperty("text").GetString();
        text.Should().Contain("\"chuẩn hóa\"").And.Contain("[S2] db.pdf, pages 4-5:")
            .And.Contain("Normalization removes redundancy.").And.Contain("[S1] db.pdf, page 1:");
    }

    [Fact]
    public async Task Embeddings_use_batch_endpoint_with_query_task_type()
    {
        var handler = new CaptureHandler("""{"embeddings":[{"values":[0.1,0.2]},{"values":[0.3,0.4]}]}""");
        var generator = new GeminiEmbeddingGenerator(Http(handler), Configuration());

        var vectors = await generator.EmbedAsync(["phân tích", "thiết kế"], query: true);

        vectors.Should().HaveCount(2);
        vectors[1].Should().Equal(0.3f, 0.4f);
        handler.Uri.Should().Be(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents");
        using var payload = JsonDocument.Parse(handler.Body!);
        var first = payload.RootElement.GetProperty("requests")[0];
        first.GetProperty("taskType").GetString().Should().Be("RETRIEVAL_QUERY");
        first.GetProperty("model").GetString().Should().Be("models/gemini-embedding-001");
        first.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString().Should().Be("phân tích");
    }

    [Fact]
    public async Task Embeddings_report_missing_api_key_as_not_configured()
    {
        var generator = new GeminiEmbeddingGenerator(Http(new CaptureHandler("{}")),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Gemini:ApiKey"] = "" }).Build());

        var action = () => generator.EmbedAsync(["text"], query: false);

        (await action.Should().ThrowAsync<AiGenerationException>()).Which.Failure.Should().Be(AiGenerationFailure.NotConfigured);
    }

    [Fact]
    public async Task Provider_timeouts_become_retryable_generation_failures()
    {
        var timeout = new TimeoutHandler();
        var questions = () => new GeminiContentGenerator(Http(timeout), Configuration()).GenerateQuestionsAsync(
            "Databases", null, new AiGroundingContext([new AiSourceChunk("S1", "db.pdf", 1, 1, "Rows.")], []), 1);
        var embed = () => new GeminiEmbeddingGenerator(Http(timeout), Configuration()).EmbedAsync(["Rows."], query: false);

        (await questions.Should().ThrowAsync<AiGenerationException>()).Which.Failure
            .Should().Be(AiGenerationFailure.ProviderUnavailable);
        (await embed.Should().ThrowAsync<AiGenerationException>()).Which.Failure
            .Should().Be(AiGenerationFailure.ProviderUnavailable);
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
    }

    private static string Interaction(string structured) => JsonSerializer.Serialize(new
    {
        id = "interaction-1",
        status = "completed",
        steps = new[]
        {
            new
            {
                type = "model_output",
                content = new[] { new { type = "text", text = structured } }
            }
        }
    });

    private static HttpClient Http(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
    };

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-key",
            ["Gemini:Model"] = "gemini-3.8-flash"
        }).Build();

    private sealed class CaptureHandler(string response) : HttpMessageHandler
    {
        public string? Uri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri?.ToString();
            ApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
