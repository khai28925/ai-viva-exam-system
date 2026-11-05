using AiViva.Application.Modules.Viva;
using AiViva.ModuleContracts.QuestionPreparation;
using FluentAssertions;
using Xunit;

namespace AiViva.Tests;

public sealed class FollowUpProbeTests
{
    private const string MainQuestion =
        "Trong kiến trúc Microservices, cơ chế nào đảm bảo nhất quán dữ liệu giữa các dịch vụ có cơ sở dữ liệu riêng?";
    private const string VagueAnswer =
        "Dạ, vì mỗi service có database riêng nên dữ liệu khó đồng bộ. Em nghĩ là cần dùng giao dịch để đồng bộ dữ liệu giữa các service.";

    private static readonly AssessmentContext Context = new(
        ["Hệ thống áp dụng tính nhất quán cuối cùng (eventual consistency).",
         "Mô hình xử lý giao dịch phân tán cụ thể được sử dụng là Saga pattern."], []);

    private static readonly InterviewTurn[] Turns = [new(MainQuestion, VagueAnswer)];

    [Fact]
    public void A_question_that_quotes_the_student_without_revealing_the_answer_is_accepted()
    {
        var followUp = new FollowUpCandidate(
            "Em vừa nói “cần dùng giao dịch để đồng bộ dữ liệu giữa các service”. Nếu dịch vụ thanh toán đã ghi xong " +
            "mà dịch vụ giao hàng lỗi thì giao dịch đó xử lý thế nào?",
            "cần dùng giao dịch để đồng bộ dữ liệu giữa các service", "challenge");

        FollowUpProbe.Problems(followUp, Context, Turns).Should().BeEmpty();
    }

    [Fact]
    public void Naming_an_unsaid_key_point_term_is_reported_as_revealing_the_answer()
    {
        var followUp = new FollowUpCandidate(
            "Em vừa nói “cần dùng giao dịch để đồng bộ dữ liệu”. Em có biết Saga pattern hoạt động thế nào không?",
            "cần dùng giao dịch để đồng bộ dữ liệu", "clarify");

        FollowUpProbe.RevealedTerms(followUp.Question, Context, Turns).Should().Contain("saga");
        FollowUpProbe.Problems(followUp, Context, Turns).Should().ContainSingle(p => p.Contains("reveals"));
    }

    [Fact]
    public void Copying_key_point_wording_is_reported_even_without_technical_terms()
    {
        var context = new AssessmentContext(
            ["Sinkhole anti-pattern xảy ra khi một yêu cầu chỉ đi xuyên qua các lớp mà không có xử lý nghiệp vụ thực sự nào."], []);
        InterviewTurn[] turns = [new("Sinkhole anti-pattern được hiểu như thế nào?",
            "Sinkhole anti-pattern là khi một lớp phải xử lý quá nhiều logic nghiệp vụ nên bị quá tải.")];

        FollowUpProbe.RevealedTerms(
                "Em giải thích rõ hơn yêu cầu đi xuyên qua các lớp mà không có xử lý nghiệp vụ diễn ra thế nào?", context, turns)
            .Should().Contain("đi xuyên qua các");
        FollowUpProbe.RevealedTerms(
                "Em vừa nói “một lớp phải xử lý quá nhiều logic nghiệp vụ”. Vì sao điều đó được gọi là sinkhole?", context, turns)
            .Should().BeEmpty("the student's own words and the main question's words may be repeated");
    }

    [Fact]
    public void A_quote_the_student_never_said_or_a_question_without_the_quote_is_rejected()
    {
        FollowUpProbe.Problems(new FollowUpCandidate("Vì sao cần nhất quán?", null, "clarify"), Context, Turns)
            .Should().Contain(p => p.Contains("does not name"));
        FollowUpProbe.Problems(new FollowUpCandidate("Em vừa nói “dùng khóa phân tán toàn cục”. Vì sao?",
                "dùng khóa phân tán toàn cục", "justify"), Context, Turns)
            .Should().Contain(p => p.Contains("not in the student's latest answer"));
        FollowUpProbe.Problems(new FollowUpCandidate("Vì sao em chọn cách đó?",
                "cần dùng giao dịch để đồng bộ dữ liệu", "justify"), Context, Turns)
            .Should().Contain(p => p.Contains("does not quote"));
    }

    [Fact]
    public void Quotes_match_despite_punctuation_case_and_small_transcription_differences()
    {
        FollowUpProbe.Covers(VagueAnswer, "Cần dùng giao dịch, để đồng bộ dữ liệu!").Should().BeTrue();
        FollowUpProbe.Covers(VagueAnswer, "cần dùng một giao dịch để đồng bộ dữ liệu").Should().BeTrue();
        FollowUpProbe.Covers(VagueAnswer, "dùng khóa phân tán").Should().BeFalse();
    }
}
