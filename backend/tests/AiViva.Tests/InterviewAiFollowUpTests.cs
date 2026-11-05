using System.Net;
using System.Text;
using System.Text.Json;
using AiViva.Application.Modules.Viva;
using AiViva.Infrastructure.Modules.Viva;
using AiViva.ModuleContracts.QuestionPreparation;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiViva.Tests;

public sealed class InterviewAiFollowUpTests
{
    private static readonly AssessmentCriterion Accuracy = new(Guid.NewGuid(), "Accuracy", null, 5);
    private static readonly AssessmentContext Context = new(
        ["Mô hình xử lý giao dịch phân tán cụ thể được sử dụng là Saga pattern."],
        [new AssessmentRubric(Guid.NewGuid(), "Knowledge", 100, [Accuracy])]);
    private const string Answer = "Em nghĩ là cần dùng giao dịch để đồng bộ dữ liệu giữa các service.";
    private static readonly InterviewTurn[] Turns =
        [new("Cơ chế nào đảm bảo nhất quán dữ liệu giữa các dịch vụ có cơ sở dữ liệu riêng?", Answer)];
    private const string Quote = "cần dùng giao dịch để đồng bộ dữ liệu";
    private const string Grounded =
        "Em vừa nói “cần dùng giao dịch để đồng bộ dữ liệu”. Nếu dịch vụ thanh toán đã ghi xong mà dịch vụ giao hàng lỗi thì sao?";
    private const string Revealing = "Em vừa nói “cần dùng giao dịch để đồng bộ dữ liệu”. Em có biết Saga pattern không?";

    [Fact]
    public async Task A_follow_up_grounded_in_the_students_words_is_kept_with_its_target_and_claims()
    {
        var handler = new ScriptedHandler(Assessment(Grounded, Quote, "challenge"));

        var result = await Ai(handler).AssessAsync(Context, Turns, allowFollowUp: true, "vi-VN");

        result.FollowUpQuestion.Should().Be(Grounded);
        result.FollowUpTarget.Should().Be(Quote);
        result.FollowUpType.Should().Be("challenge");
        result.Claims.Should().ContainSingle(c => c.Quote == Quote && c.Assessment == "vague");
        handler.Prompts.Should().ContainSingle("a valid follow-up needs no rewrite");
    }

    [Fact]
    public async Task A_follow_up_that_reveals_the_answer_is_rewritten_once()
    {
        var handler = new ScriptedHandler(Assessment(Revealing, Quote, "clarify"), Rewrite(Grounded, Quote, "challenge"));

        var result = await Ai(handler).AssessAsync(Context, Turns, allowFollowUp: true, "vi-VN");

        result.FollowUpQuestion.Should().Be(Grounded);
        result.FollowUpType.Should().Be("challenge");
        handler.Prompts.Should().HaveCount(2);
        handler.Prompts[1].Should().Contain("rejectedQuestion").And.Contain("reveals expected answer content")
            .And.Contain("\"claims\":[{\"quote\":\"" + Quote + "\",\"assessment\":\"vague\"}]");
        handler.Prompts[1].Should().NotContain(Context.ExpectedKeyPoints[0],
            "the rewrite may only work from the student's claims, never from the expected answer");
    }

    [Fact]
    public async Task A_follow_up_is_dropped_rather_than_revealing_the_answer_but_the_grading_is_kept()
    {
        var handler = new ScriptedHandler(Assessment(Revealing, Quote, "clarify"),
            Rewrite("Em vừa nói “cần dùng giao dịch”. Saga pattern có giúp được không?", "cần dùng giao dịch", "clarify"));

        var result = await Ai(handler).AssessAsync(Context, Turns, allowFollowUp: true, "vi-VN");

        result.FollowUpQuestion.Should().BeNull();
        result.FollowUpTarget.Should().BeNull();
        result.Criteria.Should().ContainSingle(c => c.CriterionId == Accuracy.Id && c.Score == 2);
    }

    [Fact]
    public async Task A_complete_first_answer_is_still_probed_once_with_a_deepening_question()
    {
        const string deepen = "Em vừa nói “cần dùng giao dịch để đồng bộ dữ liệu”. Em cho một ví dụ cụ thể được không?";
        var handler = new ScriptedHandler(Assessment(null, null, null), Rewrite(deepen, Quote, "example"));

        var result = await Ai(handler).AssessAsync(Context, Turns, allowFollowUp: true, "vi-VN");

        result.FollowUpQuestion.Should().Be(deepen);
        result.FollowUpType.Should().Be("example");
        handler.Prompts[0].Should().Contain("\"followUpRequired\":true");
        handler.Prompts[1].Should().Contain("every main question must be probed once");
    }

    [Fact]
    public async Task A_complete_answer_after_a_follow_up_ends_the_probing()
    {
        var handler = new ScriptedHandler(Assessment(null, null, null));
        InterviewTurn[] turns = [Turns[0], new(Grounded, "Dịch vụ thanh toán sẽ hoàn tiền bằng một giao dịch bù.")];

        var result = await Ai(handler).AssessAsync(Context, turns, allowFollowUp: true, "vi-VN");

        result.FollowUpQuestion.Should().BeNull();
        handler.Prompts.Should().ContainSingle().Which.Should().Contain("\"followUpRequired\":false");
    }

    [Fact]
    public async Task No_follow_up_is_returned_or_rewritten_when_follow_ups_are_not_allowed()
    {
        var handler = new ScriptedHandler(Assessment(Revealing, Quote, "clarify"));

        var result = await Ai(handler).AssessAsync(Context, Turns, allowFollowUp: false, "vi-VN");

        result.FollowUpQuestion.Should().BeNull();
        result.FollowUpType.Should().BeNull();
        handler.Prompts.Should().ContainSingle();
    }

    [Fact]
    public async Task The_prompt_sends_vietnamese_text_unescaped_so_the_model_can_quote_it()
    {
        var handler = new ScriptedHandler(Assessment(null, null, null));

        await Ai(handler).AssessAsync(Context, Turns, allowFollowUp: true, "vi-VN");

        handler.Prompts[0].Should().Contain("\"latestAnswer\":\"" + Answer + "\"").And.NotContain("\\u");
    }

    private static GeminiInterviewAi Ai(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") },
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-key",
            ["Gemini:Model"] = "primary"
        }).Build());

    private static string Assessment(string? question, string? target, string? type) => JsonSerializer.Serialize(new
    {
        criteria = new[] { new { criterionId = Accuracy.Id, score = 2, applicable = true, feedback = "Còn mơ hồ." } },
        feedback = "Câu trả lời còn mơ hồ.",
        strengths = Array.Empty<string>(),
        missingKeyPoints = new[] { "Saga pattern" },
        claims = new[] { new { quote = Quote, assessment = "vague" } },
        followUpQuestion = question,
        followUpTarget = target,
        followUpType = type
    });

    private static string Rewrite(string question, string target, string type) =>
        JsonSerializer.Serialize(new { followUpQuestion = question, followUpTarget = target, followUpType = type });

    /// <summary>Returns the scripted structured outputs in order and records the prompt text of each request.</summary>
    private sealed class ScriptedHandler(params string[] outputs) : HttpMessageHandler
    {
        private int next;
        public List<string> Prompts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Prompts.Add(body.RootElement.GetProperty("input")[0].GetProperty("text").GetString()!);
            var output = outputs[Math.Min(next++, outputs.Length - 1)];
            var interaction = JsonSerializer.Serialize(new
            {
                steps = new[] { new { type = "model_output", content = new[] { new { type = "text", text = output } } } }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(interaction, Encoding.UTF8, "application/json")
            };
        }
    }
}
