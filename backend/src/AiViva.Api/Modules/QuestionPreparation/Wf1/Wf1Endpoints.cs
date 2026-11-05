using System.Security.Claims;
using AiViva.Api.Extensions;
using AiViva.Application.Modules.QuestionPreparation.Wf1;

namespace AiViva.Api.Modules.QuestionPreparation.Wf1;

public static class Wf1Endpoints
{
    public static RouteGroupBuilder MapWf1Endpoints(this RouteGroupBuilder api)
    {
        var subjects = api.MapGroup("/v1/subjects")
            .WithTags("WF1 - AI Question Preparation")
            .RequireAuthorization(AuthPolicies.QuestionBankManage);

        subjects.MapGet("/", async (IWf1PreparationService service, ClaimsPrincipal principal, CancellationToken ct) =>
            Results.Ok(await service.GetSubjectsAsync(Actor(principal), ct)));
        subjects.MapPost("/", async (CreateSubjectRequest request, IWf1PreparationService service,
                ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () => Results.Created("/api/v1/subjects",
                await service.CreateSubjectAsync(request, Actor(principal), ct))));
        subjects.MapGet("/{subjectId:guid}/workspace", async (Guid subjectId,
                IWf1PreparationService service, ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () => Results.Ok(
                await service.GetWorkspaceAsync(subjectId, Actor(principal), ct))));
        subjects.MapPost("/{subjectId:guid}/question-banks", async (Guid subjectId,
                CreateSubjectQuestionBankRequest request, IWf1PreparationService service,
                ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () => Results.Created($"/api/v1/subjects/{subjectId}/question-banks",
                await service.CreateQuestionBankAsync(subjectId, request, Actor(principal), ct))));

        subjects.MapPost("/{subjectId:guid}/documents", async (Guid subjectId, IFormFile file,
                IWf1PreparationService service, ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () =>
            {
                if (file.Length > 10 * 1024 * 1024)
                    return Results.Problem(statusCode: 400, title: "Invalid document",
                        detail: "PDF must not exceed 10 MB.", extensions: new Dictionary<string, object?> { ["code"] = "invalid_document_size" });
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                return Results.Created($"/api/v1/subjects/{subjectId}/documents",
                    await service.UploadDocumentAsync(subjectId, file.FileName, file.ContentType,
                        buffer.ToArray(), Actor(principal), ct));
            })).DisableAntiforgery();
        subjects.MapDelete("/{subjectId:guid}/documents/{documentId:guid}", async (
                Guid subjectId, Guid documentId, IWf1PreparationService service,
                ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () =>
            {
                await service.DeleteDocumentAsync(subjectId, documentId, Actor(principal), ct);
                return Results.NoContent();
            }));

        subjects.MapPost("/{subjectId:guid}/documents/{documentId:guid}/reindex", async (
                Guid subjectId, Guid documentId, IWf1PreparationService service,
                ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () => Results.Ok(
                await service.ReindexDocumentAsync(subjectId, documentId, Actor(principal), ct))));

        subjects.MapPost("/{subjectId:guid}/ai/generate", async (Guid subjectId,
                GenerateWf1ContentRequest request, IWf1PreparationService service,
                ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () => Results.Ok(
                await service.GenerateAsync(subjectId, request, Actor(principal), ct))));
        subjects.MapPut("/{subjectId:guid}/question-candidates/{candidateId:guid}", async (
                Guid subjectId, Guid candidateId, UpdateAiQuestionCandidateRequest request,
                IWf1PreparationService service, ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () => Results.Ok(
                await service.UpdateCandidateAsync(subjectId, candidateId, request, Actor(principal), ct))));
        subjects.MapPost("/{subjectId:guid}/question-candidates/{candidateId:guid}/review", async (
                Guid subjectId, Guid candidateId, ReviewAiQuestionCandidateRequest request,
                IWf1PreparationService service, ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () => Results.Ok(
                await service.ReviewCandidateAsync(subjectId, candidateId, request, Actor(principal), ct))));

        subjects.MapPut("/{subjectId:guid}/rubric-sets/draft", async (Guid subjectId,
                SaveRubricSetRequest request, IWf1PreparationService service,
                ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () => Results.Ok(
                await service.SaveDraftRubricSetAsync(subjectId, request, Actor(principal), ct))));
        subjects.MapPost("/{subjectId:guid}/rubric-sets/{rubricSetId:guid}/approve", async (
                Guid subjectId, Guid rubricSetId, IWf1PreparationService service,
                ClaimsPrincipal principal, CancellationToken ct) =>
            await ExecuteAsync(async () => Results.Ok(
                await service.ApproveRubricSetAsync(subjectId, rubricSetId, Actor(principal), ct))));
        return api;
    }

    private static Wf1Actor Actor(ClaimsPrincipal principal) => new(
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty,
        principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty);

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (Wf1Exception exception)
        {
            var status = exception.Failure switch
            {
                Wf1Failure.NotFound => 404,
                Wf1Failure.Forbidden => 403,
                Wf1Failure.Conflict => 409,
                Wf1Failure.AiRateLimited => 429,
                Wf1Failure.AiUnavailable => 503,
                _ => 400
            };
            return Results.Problem(statusCode: status, title: exception.Failure.ToString(),
                detail: exception.Message, extensions: exception.Code is null ? null :
                    new Dictionary<string, object?> { ["code"] = exception.Code });
        }
    }
}
