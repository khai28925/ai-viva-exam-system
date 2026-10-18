using System.Security.Claims;
using AiViva.Api.Extensions;
using AiViva.Application.ExamSessions;

namespace AiViva.Api.Endpoints;

public static class ExamSessionEndpoints
{
    public static RouteGroupBuilder MapExamSessionEndpoints(this RouteGroupBuilder group)
    {
        var sessions = group.MapGroup("/v1/exam-sessions")
            .WithTags("Exam Sessions")
            .RequireAuthorization(AuthPolicies.ExamSessionRead);

        sessions.MapGet("/", async (IExamSessionService service, ClaimsPrincipal principal, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(Actor(principal), ct)))
            .WithName("GetExamSessions")
            .Produces<IReadOnlyCollection<ExamSessionDto>>();

        sessions.MapGet("/{id:guid}", async (Guid id, IExamSessionService service, ClaimsPrincipal principal, CancellationToken ct) =>
            Respond(await service.GetByIdAsync(id, Actor(principal), ct), Results.Ok))
            .WithName("GetExamSession")
            .Produces<ExamSessionDto>().ProducesProblem(404);

        sessions.MapPost("/", async (SaveExamSessionRequest request, IExamSessionService service, ClaimsPrincipal principal, CancellationToken ct) =>
            Respond(await service.CreateAsync(request, Actor(principal), ct), dto => Results.Created($"/api/v1/exam-sessions/{dto.Id}", dto)))
            .RequireAuthorization(AuthPolicies.ExamSessionManage)
            .WithName("CreateExamSession")
            .Produces<ExamSessionDto>(201).ProducesValidationProblem().ProducesProblem(404).ProducesProblem(409);

        sessions.MapPut("/{id:guid}", async (Guid id, SaveExamSessionRequest request, IExamSessionService service, ClaimsPrincipal principal, CancellationToken ct) =>
            Respond(await service.UpdateAsync(id, request, Actor(principal), ct), Results.Ok))
            .RequireAuthorization(AuthPolicies.ExamSessionManage)
            .WithName("UpdateExamSession")
            .Produces<ExamSessionDto>().ProducesValidationProblem().ProducesProblem(404).ProducesProblem(409);

        sessions.MapGet("/{id:guid}/questions", async (Guid id, IExamSessionService service, ClaimsPrincipal principal, CancellationToken ct) =>
            Respond(await service.GetQuestionsAsync(id, Actor(principal), ct), Results.Ok))
            .RequireAuthorization(AuthPolicies.ExamSessionManage)
            .WithName("GetExamSessionQuestions")
            .Produces<IReadOnlyCollection<SelectedExamQuestionDto>>().ProducesProblem(404);

        sessions.MapPut("/{id:guid}/questions", async (Guid id, SaveExamQuestionSelectionRequest request, IExamSessionService service, ClaimsPrincipal principal, CancellationToken ct) =>
            Respond(await service.ReplaceQuestionsAsync(id, request, Actor(principal), ct), Results.Ok))
            .RequireAuthorization(AuthPolicies.ExamSessionManage)
            .WithName("ReplaceExamSessionQuestions")
            .Produces<IReadOnlyCollection<SelectedExamQuestionDto>>().ProducesValidationProblem()
            .ProducesProblem(404).ProducesProblem(409);

        sessions.MapDelete("/{id:guid}", async (Guid id, IExamSessionService service, ClaimsPrincipal principal, CancellationToken ct) =>
            Respond(await service.DeleteAsync(id, Actor(principal), ct), _ => Results.NoContent()))
            .RequireAuthorization(AuthPolicies.ExamSessionManage)
            .WithName("DeleteExamSession")
            .Produces(204).ProducesProblem(404).ProducesProblem(409);

        sessions.MapPost("/{id:guid}/join", async (Guid id, IExamSessionService service, ClaimsPrincipal principal, CancellationToken ct) =>
            Respond(await service.JoinAsync(id, Actor(principal), ct), Results.Ok))
            .RequireAuthorization(AuthPolicies.StudentOnly)
            .WithName("JoinExamSession")
            .Produces<ExamAttemptDto>().ProducesProblem(404).ProducesProblem(409);

        return group;
    }

    private static ExamActor Actor(ClaimsPrincipal principal) => new(
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
        principal.FindFirstValue(ClaimTypes.Role)!);

    private static IResult Respond<T>(ExamSessionResult<T> result, Func<T, IResult> success)
    {
        if (result.Error == ExamSessionError.None) return success(result.Value!);
        var (status, title) = result.Error switch
        {
            ExamSessionError.Invalid => (400, "Invalid request"),
            ExamSessionError.NotFound => (404, "Not Found"),
            ExamSessionError.Forbidden => (403, "Forbidden"),
            ExamSessionError.Conflict => (409, "Conflict"),
            _ => (500, "Internal Server Error")
        };
        if (result.Error == ExamSessionError.Invalid && result.ValidationErrors is not null)
            return Results.ValidationProblem(result.ValidationErrors, statusCode: status, title: title);
        var extensions = new Dictionary<string, object?>();
        if (result.Code is not null) extensions["code"] = result.Code;
        if (result.Missing is not null) extensions["missing"] = result.Missing;
        if (result.QuestionIdsWithoutKeyPoints is not null)
            extensions["questionIdsWithoutKeyPoints"] = result.QuestionIdsWithoutKeyPoints;
        return Results.Problem(statusCode: status, title: title, detail: result.Detail, extensions: extensions);
    }
}
