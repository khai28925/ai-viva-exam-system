using AiViva.Api.Extensions;
using AiViva.Application.ExpectedKeyPoints;
using Microsoft.AspNetCore.Mvc;

namespace AiViva.Api.Endpoints;

public static class ExpectedKeyEndpoints
{
    public static RouteGroupBuilder MapExpectedKeyEndpoints(this RouteGroupBuilder group)
    {
        var points = group.MapGroup("/v1/question-banks/{bankId:guid}/questions/{questionId:guid}/expected-key-points")
            .WithTags("Expected Key Points")
            .RequireAuthorization(AuthPolicies.QuestionBankManage);

        points.MapGet("/", async (
            Guid bankId, Guid questionId,
            [FromServices] IExpectedKeyPointsService service,
            CancellationToken ct) =>
        {
            var result = await service.GetByQuestionIdAsync(bankId, questionId, ct);
            return ToResult(result, value => Results.Ok(value));
        })
        .WithName("GetExpectedKeyPoints")
        .WithSummary("List expected key points for a bank question in display order.")
        .Produces<IReadOnlyCollection<ExpectedKeyPointsDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        points.MapGet("/{keyPointId:guid}", async (
            Guid bankId, Guid questionId, Guid keyPointId,
            [FromServices] IExpectedKeyPointsService service,
            CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(bankId, questionId, keyPointId, ct);
            return ToResult(result, value => Results.Ok(value));
        })
        .WithName("GetExpectedKeyPointById")
        .WithSummary("Get one expected key point belonging to a bank question.")
        .Produces<ExpectedKeyPointsDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        points.MapPost("/", async (
            Guid bankId, Guid questionId,
            [FromBody] CreateExpectedKeyPointsRequest request,
            [FromServices] IExpectedKeyPointsService service,
            CancellationToken ct) =>
        {
            var errors = ExpectedKeyPointsValidation.Validate(request);
            if (errors is not null)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await service.CreateAsync(bankId, questionId, request, ct);
            return ToResult(result, value => Results.Created(
                $"/api/v1/question-banks/{bankId}/questions/{questionId}/expected-key-points/{value.Id}", value));
        })
        .WithName("CreateExpectedKeyPoint")
        .WithSummary("Add an expected key point to a bank question.")
        .Produces<ExpectedKeyPointsDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        points.MapPut("/{keyPointId:guid}", async (
            Guid bankId, Guid questionId, Guid keyPointId,
            [FromBody] UpdateExpectedKeyPointsRequest request,
            [FromServices] IExpectedKeyPointsService service,
            CancellationToken ct) =>
        {
            var errors = ExpectedKeyPointsValidation.Validate(request);
            if (errors is not null)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await service.UpdateAsync(bankId, questionId, keyPointId, request, ct);
            return ToResult(result, value => Results.Ok(value));
        })
        .WithName("UpdateExpectedKeyPoint")
        .WithSummary("Update expected key point content without changing its display order.")
        .Produces<ExpectedKeyPointsDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        points.MapDelete("/{keyPointId:guid}", async (
            Guid bankId, Guid questionId, Guid keyPointId,
            [FromServices] IExpectedKeyPointsService service,
            CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(bankId, questionId, keyPointId, ct);
            return ToResult(result, _ => Results.NoContent());
        })
        .WithName("DeleteExpectedKeyPoint")
        .WithSummary("Delete an expected key point without renumbering the others.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static IResult ToResult<T>(ExpectedKeyPointsResult<T> result, Func<T, IResult> success)
    {
        if (result.IsSuccess)
        {
            return success(result.Value!);
        }

        return result.Error switch
        {
            ExpectedKeyPointsError.QuestionNotFound or ExpectedKeyPointsError.KeyPointNotFound =>
                Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: result.Detail),

            ExpectedKeyPointsError.PreparationLocked =>
                Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.Detail,
                    extensions: new Dictionary<string, object?> { ["code"] = "question_preparation_locked" }),

            ExpectedKeyPointsError.ConcurrentWrite =>
                Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.Detail,
                    extensions: new Dictionary<string, object?> { ["code"] = "expected_key_point_concurrent_write" }),

            ExpectedKeyPointsError.Invalid when result.ValidationErrors is not null =>
                Results.ValidationProblem(result.ValidationErrors),

            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
