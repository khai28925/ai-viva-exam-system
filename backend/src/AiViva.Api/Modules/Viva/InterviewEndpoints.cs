using System.Security.Claims;
using System.Text;
using AiViva.Api.Extensions;
using AiViva.Application.ExamSessions;
using AiViva.Application.Modules.Viva;

namespace AiViva.Api.Modules.Viva;

public static class InterviewEndpoints
{
    public static RouteGroupBuilder MapInterviewEndpoints(this RouteGroupBuilder api)
    {
        var interviews = api.MapGroup("/v1/interviews").WithTags("AI Viva Interview")
            .RequireAuthorization(AuthPolicies.StudentOnly);
        interviews.MapPost("/{attemptId:guid}/resume", async (Guid attemptId, IInterviewService service,
            ClaimsPrincipal principal, CancellationToken ct) => await Execute(async () =>
                Results.Ok(await service.StartAsync(attemptId, Actor(principal), ct))));
        interviews.MapPost("/{attemptId:guid}/answers", async (Guid attemptId, SubmitInterviewAnswer request,
            IInterviewService service, ClaimsPrincipal principal, CancellationToken ct) => await Execute(async () =>
                Results.Ok(await service.SubmitAsync(attemptId, request, Actor(principal), ct))));
        interviews.MapPost("/{attemptId:guid}/questions/{questionId:guid}/transcribe", async (
            Guid attemptId, Guid questionId, IFormFile file, IInterviewService service, ClaimsPrincipal principal,
            CancellationToken ct) => await Execute(async () =>
        {
            if (file.Length is < 1 or > 10 * 1024 * 1024) throw new InterviewException(400, "Âm thanh tối đa 10 MB.");
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return Results.Ok(await service.TranscribeAsync(attemptId, questionId, buffer.ToArray(),
                file.ContentType.Split(';')[0], Actor(principal), ct));
        })).DisableAntiforgery();

        var settings = api.MapGroup("/v1/exam-sessions/{sessionId:guid}/interview-settings")
            .WithTags("AI Viva Interview").RequireAuthorization(AuthPolicies.ExamSessionManage);
        settings.MapGet("/", async (Guid sessionId, IInterviewService service, ClaimsPrincipal principal,
            CancellationToken ct) => await Execute(async () => Results.Ok(await service.GetSettingsAsync(sessionId, Actor(principal), ct))));
        settings.MapPut("/", async (Guid sessionId, InterviewSettings request, IInterviewService service,
            ClaimsPrincipal principal, CancellationToken ct) => await Execute(async () => Results.Ok(
                await service.SaveSettingsAsync(sessionId, request, Actor(principal), ct))));

        var reports = api.MapGroup("/v1/reports").WithTags("Viva Results and Reports")
            .RequireAuthorization(AuthPolicies.ExamSessionRead);
        reports.MapGet("/", async (IInterviewService service, ClaimsPrincipal principal, CancellationToken ct) =>
            await Execute(async () => Results.Ok(await service.ListReportsAsync(Actor(principal), ct))));
        reports.MapGet("/{attemptId:guid}", async (Guid attemptId, IInterviewService service, ClaimsPrincipal principal,
            CancellationToken ct) => await Execute(async () => Results.Ok(await service.GetReportAsync(attemptId, Actor(principal), ct))));
        reports.MapPost("/{attemptId:guid}/finalize", async (Guid attemptId, FinalizeReportRequest request,
            IInterviewService service, ClaimsPrincipal principal, CancellationToken ct) => await Execute(async () =>
                Results.Ok(await service.FinalizeAsync(attemptId, request, Actor(principal), ct))))
            .RequireAuthorization(AuthPolicies.ExamSessionManage);
        reports.MapGet("/sessions/{sessionId:guid}/statistics", async (Guid sessionId, IInterviewService service,
            ClaimsPrincipal principal, CancellationToken ct) => await Execute(async () => Results.Ok(
                await service.GetStatisticsAsync(sessionId, Actor(principal), ct))))
            .RequireAuthorization(AuthPolicies.ExamSessionManage);
        reports.MapGet("/sessions/{sessionId:guid}/export", async (Guid sessionId, string? format, IInterviewService service,
            ClaimsPrincipal principal, CancellationToken ct) => await Execute(async () =>
        {
            var sheet = await service.GetGradeSheetAsync(sessionId, Actor(principal), ct);
            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var utf8 = new UTF8Encoding(true);
                return Results.File(utf8.GetPreamble().Concat(utf8.GetBytes(GradeSheetExport.ToCsv(sheet))).ToArray(),
                    "text/csv; charset=utf-8", $"bang-diem-{sessionId}.csv");
            }
            return Results.File(GradeSheetExport.ToXlsx(sheet),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"bang-diem-{sessionId}.xlsx");
        })).RequireAuthorization(AuthPolicies.ExamSessionManage);
        return api;
    }

    private static ExamActor Actor(ClaimsPrincipal principal) => new(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), principal.FindFirstValue(ClaimTypes.Role)!);

    private static async Task<IResult> Execute(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (InterviewException exception)
        { return Results.Problem(statusCode: exception.Status, title: "Viva request failed", detail: exception.Message); }
    }
}
