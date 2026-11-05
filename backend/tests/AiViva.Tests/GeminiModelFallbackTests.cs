using System.Net;
using System.Text;
using System.Text.Json;
using AiViva.Application.Modules.QuestionPreparation.Wf1;
using AiViva.Application.Modules.Viva;
using AiViva.Infrastructure.Configuration;
using AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;
using AiViva.Infrastructure.Modules.Viva;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiViva.Tests;

public sealed class GeminiModelFallbackTests
{
    private static readonly AiGroundingContext Grounding = new([new AiSourceChunk("S1", "db.pdf", 1, 1, "Rows.")], []);

    [Fact]
    public void Model_chain_reads_primary_then_distinct_fallbacks_from_string_or_array()
    {
        new GeminiModelChain(Config(("Gemini:Model", "a"), ("Gemini:FallbackModels", " b, a ;c ")), TimeSpan.FromSeconds(5))
            .Models.Should().Equal("a", "b", "c");
        var array = new GeminiModelChain(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:FallbackModels:0"] = "x",
            ["Gemini:FallbackModels:1"] = "y",
            ["Gemini:FallbackAfterSeconds"] = "2.5"
        }).Build(), TimeSpan.FromSeconds(5));
        array.Models.Should().Equal(GeminiModelChain.DefaultModel, "x", "y");
        array.FallbackAfter.Should().Be(TimeSpan.FromSeconds(2.5));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Question_generation_moves_to_the_fallback_model_and_records_it(HttpStatusCode primaryFailure)
    {
        var handler = new ScriptedHandler(model => model == "primary"
            ? new HttpResponseMessage(primaryFailure)
            : Ok("""{"questions":[{"content":"Q?","bloomLevel":"Remember","difficultyLevel":1,"rationale":"r","expectedKeyPoints":["k"],"sourceLabels":["S1"]}]}"""));
        var generator = new GeminiContentGenerator(Http(handler), Config(("Gemini:FallbackModels", "backup")));

        var questions = await generator.GenerateQuestionsAsync("Databases", null, Grounding, 1);

        questions.Should().ContainSingle();
        handler.Models.Should().Equal("primary", "backup");
        generator.Model.Should().Be("backup");
    }

    [Fact]
    public async Task Request_errors_shared_by_every_model_are_not_retried()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var generator = new GeminiContentGenerator(Http(handler), Config(("Gemini:FallbackModels", "backup")));

        var action = () => generator.GenerateQuestionsAsync("Databases", null, Grounding, 1);

        (await action.Should().ThrowAsync<AiGenerationException>()).Which.Message.Should().Contain("400");
        handler.Models.Should().Equal("primary");
    }

    [Fact]
    public async Task The_last_models_error_surfaces_when_every_model_fails()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var generator = new GeminiContentGenerator(Http(handler),
            Config(("Gemini:FallbackModels", "backup"), ("Gemini:RetryDelaySeconds", "0")));

        var action = () => generator.GenerateQuestionsAsync("Databases", null, Grounding, 1);

        (await action.Should().ThrowAsync<AiGenerationException>()).Which.Failure.Should().Be(AiGenerationFailure.RateLimited);
        handler.Models.Should().Equal(["primary", "backup", "primary", "backup"], "the whole chain is retried once");
    }

    [Fact]
    public async Task A_briefly_overloaded_chain_succeeds_on_the_retry_round()
    {
        var calls = 0;
        var handler = new ScriptedHandler(_ => ++calls <= 2
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Ok("""{"text":"Saga pattern"}"""));
        var ai = new GeminiInterviewAi(Http(handler),
            Config(("Gemini:FallbackModels", "backup"), ("Gemini:RetryDelaySeconds", "0")));

        var text = await ai.TranscribeAsync([1, 2, 3], "audio/webm", "vi-VN");

        text.Should().Be("Saga pattern");
        handler.Models.Should().Equal("primary", "backup", "primary");
    }

    [Fact]
    public async Task Interview_transcription_falls_back_when_the_primary_model_is_too_slow()
    {
        var handler = new ScriptedHandler(model => Ok("""{"text":"Saga pattern"}"""), slowModel: "primary");
        var ai = new GeminiInterviewAi(Http(handler), Config(("Gemini:FallbackModels", "backup"), ("Gemini:FallbackAfterSeconds", "0.2")));

        var text = await ai.TranscribeAsync([1, 2, 3], "audio/webm", "vi-VN");

        text.Should().Be("Saga pattern");
        handler.Models.Should().Equal("primary", "backup");
    }

    [Fact]
    public async Task Interview_overload_on_the_only_model_keeps_the_existing_error()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var ai = new GeminiInterviewAi(Http(handler), Config());

        var action = () => ai.TranscribeAsync([1], "audio/webm", "vi-VN");

        (await action.Should().ThrowAsync<InterviewException>()).Which.Status.Should().Be(503);
    }

    private static HttpResponseMessage Ok(string structured) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            steps = new[] { new { type = "model_output", content = new[] { new { type = "text", text = structured } } } }
        }), Encoding.UTF8, "application/json")
    };

    private static HttpClient Http(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
    };

    private static IConfiguration Config(params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string?> { ["Gemini:ApiKey"] = "test-key", ["Gemini:Model"] = "primary" };
        foreach (var (key, value) in extra) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private sealed class ScriptedHandler(Func<string, HttpResponseMessage> respond, string? slowModel = null) : HttpMessageHandler
    {
        public List<string> Models { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var model = body.RootElement.GetProperty("model").GetString()!;
            Models.Add(model);
            if (model == slowModel) await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return respond(model);
        }
    }
}
