using System.Text.Json;
using AiViva.Application.Accounts;
using AiViva.Application.ExamSessions;
using AiViva.Application.Modules.Viva;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Persistence.Models;
using AiViva.ModuleContracts.QuestionPreparation;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Modules.Viva;

public sealed class InterviewService(QuestionBankDbContext db, IInterviewAi ai,
    IQuestionPreparationReader preparation, IAccountService accounts, TimeProvider clock) : IInterviewService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<InterviewSettings> GetSettingsAsync(Guid sessionId, ExamActor actor, CancellationToken ct = default)
    {
        var session = await ManagedSessionAsync(sessionId, actor, ct);
        return new(session.AnswerTimeSeconds, session.MaxFollowUps, session.Language, session.AnswerMode);
    }

    public async Task<InterviewSettings> SaveSettingsAsync(Guid sessionId, InterviewSettings settings, ExamActor actor, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await ManagedSessionAsync(sessionId, actor, ct, true);
        if (session.Status != "Draft") throw new InterviewException(409, "Chỉ sửa cấu hình khi phiên còn Draft.");
        if (settings.AnswerTimeSeconds is < 30 or > 600 || settings.MaxFollowUps is < 0 or > 5 ||
            settings.Language is not ("vi-VN" or "en-US") || !ExamSession.AnswerModes.IsValid(settings.AnswerMode))
            throw new InterviewException(400, "Cấu hình thi chưa hợp lệ.");
        session.ConfigureInterview(settings.AnswerTimeSeconds, settings.MaxFollowUps, settings.Language, settings.AnswerMode);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return settings;
    }

    public async Task<InterviewStateDto> StartAsync(Guid attemptId, ExamActor actor, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var attempt = await OwnedAttemptAsync(attemptId, actor, ct, true);
        var session = await db.ExamSessions.SingleAsync(s => s.Id == attempt.ExamSessionId, ct);
        if (attempt.Status == "InProgress" && session.Status is not ("Open" or "Closed"))
            throw new InterviewException(409, "Phiên thi chưa sẵn sàng.");
        if (attempt.Status == "InProgress" && !await db.AttemptQuestions.AnyAsync(q => q.ExamAttemptId == attemptId, ct))
        {
            if (session.RubricSetId is null) throw new InterviewException(409, "Phiên chưa ghim bộ rubric; giảng viên cần chuẩn bị lại phiên.");
            var selection = await db.ExamSessionQuestions.Where(q => q.ExamSessionId == session.Id)
                .OrderBy(q => q.DisplayOrder).ToArrayAsync(ct);
            if (selection.Length == 0) throw new InterviewException(409, "Phiên chưa có câu hỏi.");
            foreach (var question in selection)
            {
                var context = await preparation.GetAssessmentContextAsync(question.QuestionId, session.RubricSetId.Value, ct)
                    ?? throw new InterviewException(409, "Thiếu thông tin rubric/câu hỏi để bắt đầu bài thi.");
                if (context.ExpectedKeyPoints.Count == 0 || context.Rubrics.Count == 0)
                    throw new InterviewException(409, "Câu hỏi chưa có ý chính hoặc rubric.");
                db.AttemptQuestions.Add(new AttemptQuestion
                {
                    Id = Guid.NewGuid(),
                    ExamAttemptId = attempt.Id,
                    ExamSessionId = session.Id,
                    ExamSessionQuestionId = question.Id,
                    ContentSnapshot = question.ContentSnapshot,
                    Source = "Bank",
                    DisplayOrder = question.DisplayOrder,
                    CreatedAt = Now,
                    AssessmentContextJson = JsonSerializer.Serialize(context, Json)
                });
            }
            await db.SaveChangesAsync(ct);
        }
        var state = await StateAsync(attempt, session, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return state;
    }

    public async Task<InterviewStateDto> SubmitAsync(Guid attemptId, SubmitInterviewAnswer request, ExamActor actor, CancellationToken ct = default)
    {
        if (request.Transcript?.Length > 12000) throw new InterviewException(400, "Câu trả lời quá dài.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var attempt = await OwnedAttemptAsync(attemptId, actor, ct, true);
        var session = await db.ExamSessions.SingleAsync(s => s.Id == attempt.ExamSessionId, ct);
        var questions = await db.AttemptQuestions.Where(q => q.ExamAttemptId == attemptId).ToArrayAsync(ct);
        var current = questions.SingleOrDefault(q => q.Id == request.QuestionId)
            ?? throw new InterviewException(404, "Không tìm thấy câu hỏi trong bài thi.");
        // A response lost on the network can be retried without adding an answer/follow-up twice.
        if (await db.Answers.AnyAsync(a => a.AttemptQuestionId == current.Id, ct))
        {
            var repeatedState = await StateAsync(attempt, session, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return repeatedState;
        }
        if (attempt.Status != "InProgress" || session.Status is not ("Open" or "Closed"))
            throw new InterviewException(409, "Bài thi không còn nhận câu trả lời.");
        var state = await StateAsync(attempt, session, ct);
        if (state.CurrentQuestion?.Id != current.Id || current.PresentedAt is null)
            throw new InterviewException(409, "Chỉ được trả lời câu đang hiển thị.");
        var receivedAt = current.SubmittedAt ?? Now;
        var timedOut = receivedAt > current.PresentedAt.Value.AddSeconds(session.AnswerTimeSeconds + 5);
        // In voice-only sessions client text is never trusted: only the server-side transcript of
        // the uploaded recording counts, so an answer cannot be typed or pasted instead of spoken.
        var clientTextAllowed = !timedOut && session.AnswerMode != ExamSession.AnswerModes.Voice;
        var transcript = current.SubmittedTranscript ?? (clientTextAllowed ? request.Transcript?.Trim() ?? "" : "");
        current.SubmittedAt = receivedAt;
        current.SubmittedTranscript = transcript;
        var rootId = current.ParentAttemptQuestionId ?? current.Id;
        var root = questions.Single(q => q.Id == rootId);
        var context = JsonSerializer.Deserialize<AssessmentContext>(root.AssessmentContextJson ?? "", Json)
            ?? throw new InterviewException(409, "Thiếu snapshot chấm điểm của bài thi.");
        var chain = questions.Where(q => q.Id == rootId || q.ParentAttemptQuestionId == rootId)
            .OrderBy(q => q.Id == rootId ? -1 : q.DisplayOrder).ToArray();
        var chainIds = chain.Select(q => q.Id).ToArray();
        var previousAnswers = await db.Answers.Where(a => chainIds.Contains(a.AttemptQuestionId)).ToArrayAsync(ct);
        var turns = chain.Where(q => previousAnswers.Any(a => a.AttemptQuestionId == q.Id))
            .Select(q => new InterviewTurn(q.ContentSnapshot, previousAnswers.Single(a => a.AttemptQuestionId == q.Id).TextContent ?? ""))
            .Append(new InterviewTurn(current.ContentSnapshot, transcript)).ToArray();
        var allowFollowUp = chain.Count(q => q.Source == "AiFollowUp") < session.MaxFollowUps && transcript.Length > 0;
        // Persist the receipt before calling the provider. A disconnect/cancellation
        // must not erase an answer which arrived within its deadline.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await using var evaluationTransaction = await db.Database.BeginTransactionAsync(ct);
        await LoadAttemptAsync(attemptId, true, ct);
        if (await db.Answers.AnyAsync(a => a.AttemptQuestionId == current.Id, ct))
        {
            await db.Entry(attempt).ReloadAsync(ct);
            var alreadyProcessed = await StateAsync(attempt, session, ct);
            await db.SaveChangesAsync(ct);
            await evaluationTransaction.CommitAsync(ct);
            return alreadyProcessed;
        }
        var requestedAt = Now;
        InterviewAssessment assessment;
        if (turns.All(t => string.IsNullOrWhiteSpace(t.Answer)))
        {
            assessment = new InterviewAssessment(context.Rubrics.SelectMany(r => r.Criteria)
                .Select(c => new CriterionMark(c.Id, 0, "Chưa có câu trả lời.")).ToArray(),
                timedOut ? "Hết thời gian trả lời." : "Sinh viên bỏ qua câu hỏi.", [], context.ExpectedKeyPoints, null);
        }
        else
        {
            try { assessment = await ai.AssessAsync(context, turns, allowFollowUp, session.Language, ct); }
            catch (InterviewException)
            {
                // Retain the on-time submission so a provider outage cannot erase it
                // or turn a retry into a timeout. No marks or next question are invented.
                await db.SaveChangesAsync(ct);
                await evaluationTransaction.CommitAsync(ct);
                throw;
            }
        }
        var score = InterviewScoring.Calculate(context, assessment);
        var followUp = allowFollowUp && !string.IsNullOrWhiteSpace(assessment.FollowUpQuestion) &&
            !turns.Any(t => string.Equals(t.Question.Trim(), assessment.FollowUpQuestion.Trim(), StringComparison.OrdinalIgnoreCase))
            ? assessment.FollowUpQuestion.Trim() : null;
        var answer = new Answer
        {
            Id = Guid.NewGuid(),
            ExamAttemptId = attemptId,
            AttemptQuestionId = current.Id,
            TextContent = transcript,
            AnsweredAt = receivedAt,
            CreatedAt = receivedAt
        };
        db.Answers.Add(answer);
        db.Transcripts.Add(new Transcript { Id = Guid.NewGuid(), AnswerId = answer.Id, Content = transcript, CreatedAt = Now });
        db.AiEvaluations.Add(new AiEvaluation
        {
            Id = Guid.NewGuid(),
            AnswerId = answer.Id,
            Score = score,
            Analysis = JsonSerializer.Serialize(assessment, Json),
            Decision = followUp is not null ? "FollowUp" : "Next",
            MatchedKeyPoints = JsonSerializer.Serialize(assessment.Strengths, Json),
            MissingKeyPoints = JsonSerializer.Serialize(assessment.MissingKeyPoints, Json),
            CreatedAt = Now
        });
        db.ServiceInteractionLogs.Add(new ServiceInteractionLog
        {
            Id = Guid.NewGuid(),
            ExamAttemptId = attemptId,
            AnswerId = answer.Id,
            ServiceType = "LLM",
            Status = turns.All(t => string.IsNullOrWhiteSpace(t.Answer)) ? "Skipped" : "Completed",
            RequestTime = requestedAt,
            ResponseTime = Now,
            LatencyMs = (int)(Now - requestedAt).TotalMilliseconds
        });
        if (followUp is not null)
        {
            db.AttemptQuestions.Add(new AttemptQuestion
            {
                Id = Guid.NewGuid(),
                ExamAttemptId = attemptId,
                ExamSessionId = session.Id,
                ParentAttemptQuestionId = rootId,
                ContentSnapshot = followUp,
                Source = "AiFollowUp",
                DisplayOrder = questions.Max(q => q.DisplayOrder) + 1,
                CreatedAt = Now,
                AssessmentContextJson = root.AssessmentContextJson
            });
        }
        await db.SaveChangesAsync(ct);
        var next = await StateAsync(attempt, session, ct);
        if (next.CurrentQuestion is null)
        {
            attempt.Complete();
            var report = await BuildReportAsync(attempt, session, ct);
            db.Reports.Add(new Report
            {
                Id = Guid.NewGuid(),
                ExamAttemptId = attemptId,
                AiTotalScore = report.AiScore,
                CreatedAt = Now,
                IsFinalized = false
            });
            next = next with { Status = "Completed" };
        }
        await db.SaveChangesAsync(ct);
        await evaluationTransaction.CommitAsync(ct);
        return next;
    }

    public async Task<TranscriptDto> TranscribeAsync(Guid attemptId, Guid questionId, byte[] audio, string mimeType,
        ExamActor actor, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var attempt = await OwnedAttemptAsync(attemptId, actor, ct, true);
        var session = await db.ExamSessions.SingleAsync(s => s.Id == attempt.ExamSessionId, ct);
        var question = await db.AttemptQuestions.SingleOrDefaultAsync(q => q.Id == questionId && q.ExamAttemptId == attemptId, ct);
        if (attempt.Status != "InProgress" || session.Status is not ("Open" or "Closed") || question?.PresentedAt is null ||
            await db.Answers.AnyAsync(a => a.AttemptQuestionId == questionId, ct))
            throw new InterviewException(409, "Câu hỏi không còn nhận âm thanh.");
        if (audio.Length is < 1 or > 10 * 1024 * 1024 || mimeType is not ("audio/webm" or "audio/ogg" or "audio/wav" or "audio/mp4"))
            throw new InterviewException(400, "Âm thanh phải là WebM, Ogg, WAV hoặc MP4 và tối đa 10 MB.");
        if (Now > question.PresentedAt.Value.AddSeconds(session.AnswerTimeSeconds + 5))
            throw new InterviewException(409, "Đã hết thời gian gửi âm thanh cho câu này.");
        if (question.SubmittedAt is not null) throw new InterviewException(409, "Câu trả lời đã được tiếp nhận; hãy bấm gửi lại.");
        var receivedAt = Now;
        var text = await ai.TranscribeAsync(audio, mimeType, session.Language, question.ContentSnapshot, ct);
        text = text.Length <= 12000 ? text : text[..12000];
        question.SubmittedAt = receivedAt;
        question.SubmittedTranscript = text;
        db.ServiceInteractionLogs.Add(new ServiceInteractionLog
        {
            Id = Guid.NewGuid(),
            ExamAttemptId = attemptId,
            ServiceType = "STT",
            Status = "Completed",
            RequestTime = receivedAt,
            ResponseTime = Now,
            LatencyMs = (int)(Now - receivedAt).TotalMilliseconds
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new TranscriptDto(text);
    }

    private async Task<InterviewStateDto> StateAsync(ExamAttempt attempt, ExamSession session, CancellationToken ct)
    {
        var questions = await db.AttemptQuestions.Where(q => q.ExamAttemptId == attempt.Id).ToArrayAsync(ct);
        var answered = await db.Answers.Where(a => a.ExamAttemptId == attempt.Id).Select(a => a.AttemptQuestionId).ToArrayAsync(ct);
        var pending = questions.Where(q => !answered.Contains(q.Id)).OrderBy(q => q.Source == "AiFollowUp" ? 0 : 1)
            .ThenBy(q => q.DisplayOrder).FirstOrDefault();
        if (pending is not null && attempt.Status == "InProgress") pending.PresentedAt ??= Now;
        var rootId = pending?.ParentAttemptQuestionId ?? pending?.Id;
        var roots = questions.Where(q => q.Source == "Bank").OrderBy(q => q.DisplayOrder).ToArray();
        return new InterviewStateDto(attempt.Id, session.Id, session.Title, attempt.Status, session.Language,
            roots.Length, answered.Length, session.MaxFollowUps, Now,
            pending is null || attempt.Status != "InProgress" ? null : new InterviewQuestionDto(pending.Id,
                pending.ContentSnapshot, pending.Source, pending.ParentAttemptQuestionId,
                pending.PresentedAt?.AddSeconds(session.AnswerTimeSeconds), Array.FindIndex(roots, q => q.Id == rootId) + 1,
                pending.Source == "Bank" ? 0 : questions.Count(q => q.ParentAttemptQuestionId == rootId && q.DisplayOrder <= pending.DisplayOrder)),
            session.AnswerMode);
    }

    public async Task<IReadOnlyList<ReportSummaryDto>> ListReportsAsync(ExamActor actor, CancellationToken ct = default)
    {
        var query = db.ExamAttempts.AsNoTracking().Join(db.ExamSessions, a => a.ExamSessionId, s => s.Id,
            (a, s) => new { Attempt = a, Session = s });
        query = actor.CanManage ? query.Where(x => actor.Role == "ADMIN" || x.Session.CreatedBy == actor.Id)
            : actor.CanJoin ? query.Where(x => x.Attempt.StudentId == actor.Id) : query.Where(x => false);
        var rows = await query.ToArrayAsync(ct);
        var ids = rows.Select(r => r.Attempt.Id).ToArray();
        var reports = await db.Reports.AsNoTracking().Where(r => ids.Contains(r.ExamAttemptId)).ToArrayAsync(ct);
        var result = new List<ReportSummaryDto>();
        var students = new Dictionary<Guid, AccountDto?>();
        foreach (var row in rows.OrderByDescending(r => r.Attempt.StartedAt))
        {
            var student = await StudentAsync(row.Attempt.StudentId, students, ct);
            var report = reports.SingleOrDefault(r => r.ExamAttemptId == row.Attempt.Id);
            result.Add(new(row.Attempt.Id, row.Session.Id, row.Session.Title, row.Attempt.StudentId, student?.Email ?? "",
                report?.IsFinalized == true ? "Finalized" : row.Attempt.Status, row.Attempt.CompletedAt,
                report?.AiTotalScore, report?.IsFinalized == true ? report.FinalScore : null,
                student?.FullName, student?.StudentCode));
        }
        return result;
    }

    private async Task<AccountDto?> StudentAsync(Guid id, Dictionary<Guid, AccountDto?> cache, CancellationToken ct)
    {
        if (!cache.TryGetValue(id, out var account)) cache[id] = account = await accounts.GetByIdAsync(id, ct);
        return account;
    }

    private static int? Seconds(DateTimeOffset? from, DateTimeOffset? to) =>
        from is null || to is null ? null : Math.Max(0, (int)Math.Round((to.Value - from.Value).TotalSeconds));

    public async Task<AttemptReportDto> GetReportAsync(Guid attemptId, ExamActor actor, CancellationToken ct = default)
    {
        var attempt = await ReadableAttemptAsync(attemptId, actor, ct);
        if (attempt.Status != "Completed") throw new InterviewException(409, "Bài thi chưa hoàn thành.");
        var session = await db.ExamSessions.SingleAsync(s => s.Id == attempt.ExamSessionId, ct);
        return await BuildReportAsync(attempt, session, ct);
    }

    private async Task<AttemptReportDto> BuildReportAsync(ExamAttempt attempt, ExamSession session, CancellationToken ct,
        Dictionary<Guid, AccountDto?>? students = null, IReadOnlyDictionary<Guid, string?>? bloomBySessionQuestion = null)
    {
        var questions = await db.AttemptQuestions.AsNoTracking().Where(q => q.ExamAttemptId == attempt.Id).ToArrayAsync(ct);
        var answers = await db.Answers.AsNoTracking().Where(a => a.ExamAttemptId == attempt.Id).ToArrayAsync(ct);
        var ids = answers.Select(a => a.Id).ToArray();
        var evaluations = await db.AiEvaluations.AsNoTracking().Where(e => ids.Contains(e.AnswerId)).ToArrayAsync(ct);
        var lecturerMarks = await db.LecturerEvaluations.AsNoTracking().Where(e => ids.Contains(e.AnswerId)).ToArrayAsync(ct);
        var report = await db.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.ExamAttemptId == attempt.Id, ct);
        bloomBySessionQuestion ??= await BloomBySessionQuestionAsync(session.Id, ct);
        int? Duration(AttemptQuestion question) =>
            Seconds(question.PresentedAt, answers.SingleOrDefault(a => a.AttemptQuestionId == question.Id)?.AnsweredAt);
        // The evaluation of a turn proposed the follow-up asked after it, including the student words it probes.
        InterviewAssessment? AssessmentOf(AttemptQuestion question)
        {
            var answer = answers.SingleOrDefault(a => a.AttemptQuestionId == question.Id);
            var analysis = answer is null ? null : evaluations.SingleOrDefault(e => e.AnswerId == answer.Id)?.Analysis;
            return analysis is null ? null : JsonSerializer.Deserialize<InterviewAssessment>(analysis, Json);
        }
        var result = new List<AnswerReportDto>();
        foreach (var main in questions.Where(q => q.Source == "Bank").OrderBy(q => q.DisplayOrder))
        {
            var chain = questions.Where(q => q.Id == main.Id || q.ParentAttemptQuestionId == main.Id)
                .OrderBy(q => q.Id == main.Id ? -1 : q.DisplayOrder).ToArray();
            var chainAnswers = chain.Select(q => answers.SingleOrDefault(a => a.AttemptQuestionId == q.Id)).Where(a => a != null).ToArray();
            var last = chainAnswers.LastOrDefault();
            if (last is null) continue;
            var evaluation = evaluations.Single(e => e.AnswerId == last.Id);
            var assessment = JsonSerializer.Deserialize<InterviewAssessment>(evaluation.Analysis!, Json)!;
            var context = JsonSerializer.Deserialize<AssessmentContext>(main.AssessmentContextJson!, Json)!;
            var breakdown = context.Rubrics.SelectMany(r => r.Criteria.Select(c =>
            {
                var criterionMark = assessment.Criteria.Single(m => m.CriterionId == c.Id);
                return new CriterionResultDto(c.Id, c.Name, r.Name, r.WeightPercent, criterionMark.Score, c.MaxScore,
                    criterionMark.Feedback, criterionMark.Applicable);
            })).ToArray();
            var mark = lecturerMarks.SingleOrDefault(e => e.AnswerId == last.Id);
            result.Add(new(main.Id, main.ExamSessionQuestionId, last.Id, main.ContentSnapshot,
                answers.Single(a => a.AttemptQuestionId == main.Id).TextContent ?? "", evaluation.Score,
                report?.IsFinalized == true ? mark?.Score : null, assessment.Feedback, breakdown,
                assessment.Strengths, assessment.MissingKeyPoints, chain.Skip(1)
                    .Select((q, index) =>
                    {
                        // chain[index] is the turn answered just before follow-up q was asked.
                        var probe = AssessmentOf(chain[index]);
                        return new ReportTurnDto(q.ContentSnapshot,
                            answers.SingleOrDefault(a => a.AttemptQuestionId == q.Id)?.TextContent ?? "", Duration(q),
                            probe?.FollowUpTarget, probe?.FollowUpType);
                    }).ToArray(),
                Duration(main),
                main.ExamSessionQuestionId is { } sessionQuestionId ? bloomBySessionQuestion.GetValueOrDefault(sessionQuestionId) : null));
        }
        var student = await StudentAsync(attempt.StudentId, students ?? new Dictionary<Guid, AccountDto?>(), ct);
        var durations = result.SelectMany(q => q.FollowUps.Select(f => f.DurationSeconds).Prepend(q.DurationSeconds))
            .Where(seconds => seconds is not null).ToArray();
        return new(attempt.Id, session.Id, session.Title, attempt.StudentId,
            student?.Email ?? "", attempt.CompletedAt,
            result.Count == 0 ? 0 : Math.Round(result.Average(q => q.AiScore), 2),
            report?.IsFinalized == true ? report.FinalScore : null, report?.IsFinalized ?? false, report?.Feedback, result,
            student?.FullName, student?.StudentCode, durations.Length == 0 ? null : durations.Sum());
    }

    // Bloom level of each selected session question, read through the QuestionPreparation contract.
    private async Task<IReadOnlyDictionary<Guid, string?>> BloomBySessionQuestionAsync(Guid sessionId, CancellationToken ct)
    {
        var selection = await db.ExamSessionQuestions.AsNoTracking().Where(q => q.ExamSessionId == sessionId)
            .Select(q => new { q.Id, q.QuestionId }).ToArrayAsync(ct);
        var levels = await preparation.GetBloomLevelsAsync(selection.Select(q => q.QuestionId).ToArray(), ct);
        return selection.ToDictionary(q => q.Id, q => levels.GetValueOrDefault(q.QuestionId));
    }

    public async Task<AttemptReportDto> FinalizeAsync(Guid attemptId, FinalizeReportRequest request, ExamActor actor, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var attempt = await ReadableAttemptAsync(attemptId, actor, ct, true);
        var session = await ManagedSessionAsync(attempt.ExamSessionId, actor, ct);
        if (attempt.Status != "Completed") throw new InterviewException(409, "Bài thi chưa hoàn thành.");
        var report = await db.Reports.SingleOrDefaultAsync(r => r.ExamAttemptId == attemptId, ct)
            ?? throw new InterviewException(409, "Chưa có báo cáo AI.");
        var dto = await BuildReportAsync(attempt, session, ct);
        if (request.Questions is null || request.Questions.Count != dto.Questions.Count ||
            request.Questions.Select(q => q.QuestionId).Distinct().Count() != dto.Questions.Count ||
            request.Questions.Any(m => m.Score is < 0 or > 10 || !dto.Questions.Any(q => q.QuestionId == m.QuestionId)) ||
            request.Feedback?.Length > 2000) throw new InterviewException(400, "Cần nhập điểm từ 0 đến 10 cho từng câu hỏi chính.");
        foreach (var question in dto.Questions)
        {
            var mark = await db.LecturerEvaluations.SingleOrDefaultAsync(m => m.AnswerId == question.AnswerId, ct);
            if (mark is null)
            {
                mark = new LecturerEvaluation { Id = Guid.NewGuid(), AnswerId = question.AnswerId, CreatedAt = Now };
                db.LecturerEvaluations.Add(mark);
            }
            mark.LecturerId = actor.Id;
            mark.Score = request.Questions.Single(q => q.QuestionId == question.QuestionId).Score;
            mark.Comments = request.Feedback?.Trim();
            mark.UpdatedAt = Now;
        }
        report.LecturerTotalScore = report.FinalScore = Math.Round(request.Questions.Average(q => q.Score), 2);
        report.Feedback = request.Feedback?.Trim();
        report.IsFinalized = true;
        report.FinalizedBy = actor.Id;
        report.FinalizedAt = report.UpdatedAt = Now;
        db.ExamActivityLogs.Add(new ExamActivityLog
        {
            Id = Guid.NewGuid(),
            ExamAttemptId = attemptId,
            UserId = actor.Id,
            ActivityType = "ReportFinalized",
            Description = $"Final score: {report.FinalScore}",
            CreatedAt = Now
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await BuildReportAsync(attempt, session, ct);
    }

    private const decimal GoodAnswerThreshold = 7m;
    private static readonly string[] BloomOrder = ["Remember", "Understand", "Apply", "Analyze"];

    public async Task<SessionStatisticsDto> GetStatisticsAsync(Guid sessionId, ExamActor actor, CancellationToken ct = default)
    {
        var session = await ManagedSessionAsync(sessionId, actor, ct);
        var attempts = await db.ExamAttempts.Where(a => a.ExamSessionId == sessionId).ToArrayAsync(ct);
        var reports = await CompletedReportsAsync(session, attempts, ct);
        // The lecturer's mark replaces the AI suggestion as soon as a report is finalized.
        var scores = reports.Select(r => r.FinalScore ?? r.AiScore).ToArray();
        var distribution = new[] { new ScoreBand("0–<5", scores.Count(s => s < 5)),
            new ScoreBand("5–<7", scores.Count(s => s >= 5 && s < 7)), new ScoreBand("7–<9", scores.Count(s => s >= 7 && s < 9)),
            new ScoreBand("9–10", scores.Count(s => s >= 9)) };
        var answers = reports.SelectMany(r => r.Questions).ToArray();
        var questions = answers.GroupBy(q => q.SourceQuestionId)
            .Select(g => new QuestionStatistic(g.Key ?? g.First().QuestionId, g.First().Question, g.Count(),
                Math.Round(g.Average(q => q.FinalScore ?? q.AiScore), 2),
                Math.Round(100m * g.Count(q => (q.FinalScore ?? q.AiScore) >= GoodAnswerThreshold) / g.Count(), 2),
                g.First().BloomLevel,
                g.Any(q => q.DurationSeconds is not null) ? (int)Math.Round(g.Where(q => q.DurationSeconds is not null)
                    .Average(q => q.DurationSeconds!.Value)) : null,
                g.SelectMany(q => q.MissingKeyPoints).GroupBy(point => point.Trim(), StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(point => point.Count()).Take(3).Select(point => point.Key).ToArray()))
            .OrderBy(q => q.AverageScore).ToArray();
        var bloomLevels = answers.Where(q => q.BloomLevel is not null).GroupBy(q => q.BloomLevel!)
            .Select(g => new BloomStatistic(g.Key, g.Count(), Math.Round(g.Average(q => q.FinalScore ?? q.AiScore), 2),
                Math.Round(100m * g.Count(q => (q.FinalScore ?? q.AiScore) >= GoodAnswerThreshold) / g.Count(), 2)))
            .OrderBy(b => Array.IndexOf(BloomOrder, b.BloomLevel)).ToArray();
        return new(sessionId, session.Title, attempts.Length, reports.Count,
            scores.Length == 0 ? null : Math.Round(scores.Average(), 2), distribution, questions,
            reports.Count(r => r.IsFinalized),
            answers.Length == 0 ? null : Math.Round(100m * answers.Count(q => (q.FinalScore ?? q.AiScore) >= GoodAnswerThreshold) / answers.Length, 2),
            scores.Length == 0 ? null : scores.Max(), scores.Length == 0 ? null : scores.Min(), bloomLevels);
    }

    public async Task<GradeSheetDto> GetGradeSheetAsync(Guid sessionId, ExamActor actor, CancellationToken ct = default)
    {
        var session = await ManagedSessionAsync(sessionId, actor, ct);
        var attempts = await db.ExamAttempts.AsNoTracking().Where(a => a.ExamSessionId == sessionId).ToArrayAsync(ct);
        var students = new Dictionary<Guid, AccountDto?>();
        var reports = (await CompletedReportsAsync(session, attempts, ct, students)).ToDictionary(r => r.AttemptId);
        var selection = await db.ExamSessionQuestions.AsNoTracking().Where(q => q.ExamSessionId == sessionId)
            .OrderBy(q => q.DisplayOrder).Select(q => q.Id).ToArrayAsync(ct);
        var rows = new List<(AccountDto? Student, GradeSheetRow Row)>();
        foreach (var attempt in attempts)
        {
            var student = await StudentAsync(attempt.StudentId, students, ct);
            reports.TryGetValue(attempt.Id, out var report);
            var status = report?.IsFinalized == true ? "Đã chốt"
                : attempt.Status == "Completed" ? "Chờ giảng viên chốt" : "Đang thi";
            var marks = selection.Select(id => report?.Questions.FirstOrDefault(q => q.SourceQuestionId == id)?.FinalScore)
                .ToArray();
            rows.Add((student, new GradeSheetRow(0, student?.StudentCode, student?.FullName, student?.Email ?? "", marks,
                report?.AiScore, report?.FinalScore, status, attempt.CompletedAt, report?.LecturerFeedback)));
        }
        var ordered = rows.OrderBy(r => r.Student?.StudentCode ?? "￿", StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Student?.Email, StringComparer.OrdinalIgnoreCase)
            .Select((r, index) => r.Row with { No = index + 1 }).ToArray();
        return new(sessionId, session.Title, await preparation.GetSubjectNameAsync(session.QuestionBankId, ct), Now,
            selection.Length, ordered);
    }

    private async Task<List<AttemptReportDto>> CompletedReportsAsync(ExamSession session, IEnumerable<ExamAttempt> attempts,
        CancellationToken ct, Dictionary<Guid, AccountDto?>? students = null)
    {
        students ??= new Dictionary<Guid, AccountDto?>();
        var bloom = await BloomBySessionQuestionAsync(session.Id, ct);
        var reports = new List<AttemptReportDto>();
        foreach (var attempt in attempts.Where(a => a.Status == "Completed"))
            reports.Add(await BuildReportAsync(attempt, session, ct, students, bloom));
        return reports;
    }

    private async Task<ExamSession> ManagedSessionAsync(Guid id, ExamActor actor, CancellationToken ct, bool locked = false)
    {
        var session = locked && db.Database.IsNpgsql()
            ? await db.ExamSessions.FromSqlInterpolated($"SELECT * FROM exam_sessions WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.ExamSessions.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (session is null || !actor.CanManageSession(session.CreatedBy)) throw new InterviewException(404, "Không tìm thấy phiên thi.");
        return session;
    }

    private async Task<ExamAttempt> OwnedAttemptAsync(Guid id, ExamActor actor, CancellationToken ct, bool locked = false)
    {
        var attempt = await LoadAttemptAsync(id, locked, ct);
        if (attempt is null || !actor.CanJoin || attempt.StudentId != actor.Id)
            throw new InterviewException(404, "Không tìm thấy bài thi của bạn.");
        return attempt;
    }

    private async Task<ExamAttempt> ReadableAttemptAsync(Guid id, ExamActor actor, CancellationToken ct, bool locked = false)
    {
        var attempt = await LoadAttemptAsync(id, locked, ct);
        if (attempt is null) throw new InterviewException(404, "Không tìm thấy bài thi.");
        if (actor.CanJoin && attempt.StudentId == actor.Id) return attempt;
        await ManagedSessionAsync(attempt.ExamSessionId, actor, ct);
        return attempt;
    }

    private Task<ExamAttempt?> LoadAttemptAsync(Guid id, bool locked, CancellationToken ct) => locked && db.Database.IsNpgsql()
        ? db.ExamAttempts.FromSqlInterpolated($"SELECT * FROM exam_attempts WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
        : db.ExamAttempts.SingleOrDefaultAsync(a => a.Id == id, ct);
}
