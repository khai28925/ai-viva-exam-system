using System.ComponentModel.DataAnnotations;
using AiViva.Application.ExamSessions;
using AiViva.ModuleContracts.QuestionPreparation;

namespace AiViva.Application.Modules.Viva;

/// <param name="AnswerMode">Voice: only the server transcript of the recording is graded. VoiceOrText: editable transcript.</param>
public sealed record InterviewSettings([Range(30, 600)] int AnswerTimeSeconds,
    [Range(0, 5)] int MaxFollowUps, [RegularExpression("^(vi-VN|en-US)$")] string Language,
    [RegularExpression("^(Voice|VoiceOrText)$")] string AnswerMode = "Voice");
public sealed record InterviewQuestionDto(Guid Id, string Content, string Source, Guid? ParentQuestionId,
    DateTimeOffset? Deadline, int MainQuestionNumber, int FollowUpNumber);
public sealed record InterviewStateDto(Guid Id, Guid ExamSessionId, string Title, string Status, string Language,
    int TotalMainQuestions, int AnsweredQuestions, int MaxFollowUps, DateTimeOffset ServerTime,
    InterviewQuestionDto? CurrentQuestion, string AnswerMode = "VoiceOrText");
public sealed record SubmitInterviewAnswer([Required] Guid QuestionId, [StringLength(12000)] string? Transcript);
public sealed record InterviewTurn(string Question, string Answer);
/// <param name="Applicable">False when the criterion is about another topic than the question; it is then left out
/// of the score. Defaults to true so assessments stored before this flag existed keep their meaning.</param>
public sealed record CriterionMark(Guid CriterionId, decimal Score, string Feedback, bool Applicable = true);
public sealed record InterviewAssessment(IReadOnlyList<CriterionMark> Criteria, string Feedback,
    IReadOnlyList<string> Strengths, IReadOnlyList<string> MissingKeyPoints, string? FollowUpQuestion);
public sealed record TranscriptDto(string Text);
public sealed record CriterionResultDto(Guid CriterionId, string Name, string Rubric, decimal RubricWeightPercent,
    decimal Score, decimal MaxScore, string Feedback, bool Applicable = true);
public sealed record ReportTurnDto(string Question, string Answer, int? DurationSeconds);
public sealed record AnswerReportDto(Guid QuestionId, Guid? SourceQuestionId, Guid AnswerId, string Question, string Transcript,
    decimal AiScore, decimal? FinalScore, string Feedback, IReadOnlyList<CriterionResultDto> Criteria,
    IReadOnlyList<string> Strengths, IReadOnlyList<string> MissingKeyPoints,
    IReadOnlyList<ReportTurnDto> FollowUps, int? DurationSeconds = null, string? BloomLevel = null);
public sealed record AttemptReportDto(Guid AttemptId, Guid ExamSessionId, string Title, Guid StudentId,
    string StudentEmail, DateTimeOffset? CompletedAt, decimal AiScore, decimal? FinalScore,
    bool IsFinalized, string? LecturerFeedback, IReadOnlyList<AnswerReportDto> Questions,
    string? StudentName = null, string? StudentCode = null, int? TotalDurationSeconds = null);
public sealed record ReportSummaryDto(Guid AttemptId, Guid ExamSessionId, string Title, Guid StudentId,
    string StudentEmail, string Status, DateTimeOffset? CompletedAt, decimal? AiScore, decimal? FinalScore,
    string? StudentName = null, string? StudentCode = null);
public sealed record QuestionStatistic(Guid QuestionId, string Content, int ResponseCount, decimal AverageScore,
    decimal GoodAnswerRate, string? BloomLevel = null, int? AverageDurationSeconds = null,
    IReadOnlyList<string>? CommonMissingKeyPoints = null);
public sealed record BloomStatistic(string BloomLevel, int ResponseCount, decimal AverageScore, decimal GoodAnswerRate);
public sealed record ScoreBand(string Label, int Count);
public sealed record SessionStatisticsDto(Guid SessionId, string Title, int Participants, int Completed,
    decimal? AverageScore, IReadOnlyList<ScoreBand> Distribution, IReadOnlyList<QuestionStatistic> Questions,
    int Finalized = 0, decimal? GoodAnswerRate = null, decimal? HighestScore = null, decimal? LowestScore = null,
    IReadOnlyList<BloomStatistic>? BloomLevels = null);
public sealed record FinalQuestionMark(Guid QuestionId, [Range(0, 10)] decimal Score);
public sealed record FinalizeReportRequest([Required] IReadOnlyList<FinalQuestionMark> Questions,
    [StringLength(2000)] string? Feedback);

/// <param name="QuestionScores">Lecturer marks per main question, in session order; null until finalized.</param>
public sealed record GradeSheetRow(int No, string? StudentCode, string? FullName, string Email,
    IReadOnlyList<decimal?> QuestionScores, decimal? AiScore, decimal? FinalScore, string Status,
    DateTimeOffset? CompletedAt, string? LecturerFeedback);
public sealed record GradeSheetDto(Guid SessionId, string Title, string? SubjectName, DateTimeOffset GeneratedAt,
    int QuestionCount, IReadOnlyList<GradeSheetRow> Rows);

public sealed class InterviewException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public interface IInterviewAi
{
    Task<InterviewAssessment> AssessAsync(AssessmentContext context, IReadOnlyList<InterviewTurn> turns,
        bool allowFollowUp, string language, CancellationToken ct = default);
    /// <param name="questionContext">The question being answered; it helps recognise technical terms.
    /// Expected key points are deliberately never supplied so the transcript cannot be biased towards them.</param>
    Task<string> TranscribeAsync(byte[] audio, string mimeType, string language, string? questionContext = null,
        CancellationToken ct = default);
}

public interface IInterviewService
{
    Task<InterviewSettings> GetSettingsAsync(Guid sessionId, ExamActor actor, CancellationToken ct = default);
    Task<InterviewSettings> SaveSettingsAsync(Guid sessionId, InterviewSettings settings, ExamActor actor, CancellationToken ct = default);
    Task<InterviewStateDto> StartAsync(Guid attemptId, ExamActor actor, CancellationToken ct = default);
    Task<InterviewStateDto> SubmitAsync(Guid attemptId, SubmitInterviewAnswer request, ExamActor actor, CancellationToken ct = default);
    Task<TranscriptDto> TranscribeAsync(Guid attemptId, Guid questionId, byte[] audio, string mimeType,
        ExamActor actor, CancellationToken ct = default);
    Task<IReadOnlyList<ReportSummaryDto>> ListReportsAsync(ExamActor actor, CancellationToken ct = default);
    Task<AttemptReportDto> GetReportAsync(Guid attemptId, ExamActor actor, CancellationToken ct = default);
    Task<AttemptReportDto> FinalizeAsync(Guid attemptId, FinalizeReportRequest request, ExamActor actor, CancellationToken ct = default);
    Task<SessionStatisticsDto> GetStatisticsAsync(Guid sessionId, ExamActor actor, CancellationToken ct = default);
    Task<GradeSheetDto> GetGradeSheetAsync(Guid sessionId, ExamActor actor, CancellationToken ct = default);
}

public static class InterviewScoring
{
    public static decimal Calculate(AssessmentContext context, InterviewAssessment assessment)
    {
        var expected = context.Rubrics.SelectMany(r => r.Criteria).ToArray();
        if (context.Rubrics.Count == 0 || context.Rubrics.Sum(r => r.WeightPercent) != 100 ||
            context.Rubrics.Any(r => r.Criteria.Count == 0 || r.WeightPercent <= 0) ||
            assessment.Criteria.Count != expected.Length ||
            assessment.Criteria.Select(c => c.CriterionId).Distinct().Count() != expected.Length ||
            expected.Any(c => !assessment.Criteria.Any(m => m.CriterionId == c.Id && m.Score >= 0 && m.Score <= c.MaxScore)))
            throw new InterviewException(502, "AI trả kết quả chấm không hợp lệ. Hãy thử gửi lại câu trả lời.");
        var marks = assessment.Criteria.ToDictionary(m => m.CriterionId);
        // One rubric set grades every question, so criteria about other topics are excluded and the
        // remaining rubric weights are renormalised. If nothing is marked applicable, every criterion counts.
        var scored = context.Rubrics
            .Select(r => (Rubric: r, Criteria: r.Criteria.Where(c => marks[c.Id].Applicable).ToArray()))
            .Where(r => r.Criteria.Length > 0).ToArray();
        if (scored.Length == 0) scored = context.Rubrics.Select(r => (Rubric: r, Criteria: r.Criteria.ToArray())).ToArray();
        var weight = scored.Sum(r => r.Rubric.WeightPercent);
        var total = scored.Sum(r => r.Rubric.WeightPercent / weight *
            r.Criteria.Sum(c => marks[c.Id].Score) / r.Criteria.Sum(c => c.MaxScore) * 10m);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
}
