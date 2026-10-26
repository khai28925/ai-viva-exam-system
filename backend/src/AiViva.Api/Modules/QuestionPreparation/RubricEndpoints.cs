using AiViva.Api.Extensions;
using AiViva.Application.Rubrics;
using Microsoft.AspNetCore.Mvc;

namespace AiViva.Api.Endpoints;

public static class RubricEndpoints
{
    public static RouteGroupBuilder MapRubricEndpoints(this RouteGroupBuilder group)
    {
        var rubric = group.MapGroup("/v1/question-banks/{bankId:guid}/rubric")
            .WithTags("Rubrics")
            .RequireAuthorization(AuthPolicies.QuestionBankManage);

        rubric.MapGet("/", async (
            Guid bankId,
            [FromServices] IRubricService service,
            CancellationToken ct) =>
        {
            var result = await service.GetRubricAsync(bankId, ct);
            return ToResult(result, dto => Results.Ok(dto));
        })
        .WithName("GetRubric")
        .WithSummary("Get the rubric for a question bank.")
        .WithDescription("Retrieves the evaluation rubric configured for the specified question bank.")
        .Produces<RubricDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        rubric.MapPost("/", async (
            Guid bankId,
            [FromBody] CreateRubricRequest request,
            [FromServices] IRubricService service,
            CancellationToken ct) =>
        {
            var errors = RubricValidation.Validate(request);
            if (errors is not null)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await service.CreateRubricAsync(bankId, request, ct);
            return ToResult(result, dto => Results.Created($"/api/v1/question-banks/{bankId}/rubric", dto));
        })
        .WithName("CreateRubric")
        .WithSummary("Create a rubric for a question bank.")
        .WithDescription("Creates a single evaluation rubric for the question bank. Fails with 409 if a rubric already exists or if the bank is locked by an open/closed session.")
        .Produces<RubricDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        rubric.MapPut("/", async (
            Guid bankId,
            [FromBody] UpdateRubricRequest request,
            [FromServices] IRubricService service,
            CancellationToken ct) =>
        {
            var errors = RubricValidation.Validate(request);
            if (errors is not null)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await service.UpdateRubricAsync(bankId, request, ct);
            return ToResult(result, dto => Results.Ok(dto));
        })
        .WithName("UpdateRubric")
        .WithSummary("Update a rubric for a question bank.")
        .WithDescription("Updates the name and description of an existing rubric. Fails with 409 if the bank is locked by an open/closed session.")
        .Produces<RubricDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        rubric.MapDelete("/", async (
            Guid bankId,
            [FromServices] IRubricService service,
            CancellationToken ct) =>
        {
            var result = await service.DeleteRubricAsync(bankId, ct);
            return ToResult(result, _ => Results.NoContent());
        })
        .WithName("DeleteRubric")
        .WithSummary("Delete a rubric for a question bank.")
        .WithDescription("Deletes the rubric if it has no criteria. Fails with 409 if criteria exist or if the bank is locked by an open/closed session.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        rubric.MapGet("/criteria", async (
            Guid bankId,
            [FromServices] IRubricService service,
            CancellationToken ct) =>
        {
            var result = await service.GetCriteriaAsync(bankId, ct);
            return ToResult(result, criteria => Results.Ok(criteria));
        })
        .WithName("GetRubricCriteria")
        .WithSummary("Get all criteria for a rubric.")
        .WithDescription("Lists all evaluation criteria defined under the rubric for the given question bank.")
        .Produces<IReadOnlyCollection<RubricCriterionDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        rubric.MapGet("/criteria/{criterionId:guid}", async (
            Guid bankId,
            Guid criterionId,
            [FromServices] IRubricService service,
            CancellationToken ct) =>
        {
            var result = await service.GetCriterionByIdAsync(bankId, criterionId, ct);
            return ToResult(result, dto => Results.Ok(dto));
        })
        .WithName("GetRubricCriterionById")
        .WithSummary("Get a rubric criterion by ID.")
        .WithDescription("Retrieves a specific evaluation criterion by ID. Returns 404 if the criterion does not belong to the rubric.")
        .Produces<RubricCriterionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        rubric.MapPost("/criteria", async (
            Guid bankId,
            [FromBody] CreateRubricCriterionRequest request,
            [FromServices] IRubricService service,
            CancellationToken ct) =>
        {
            var errors = RubricValidation.Validate(request);
            if (errors is not null)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await service.CreateCriterionAsync(bankId, request, ct);
            return ToResult(result, dto => Results.Created($"/api/v1/question-banks/{bankId}/rubric/criteria/{dto.Id}", dto));
        })
        .WithName("CreateRubricCriterion")
        .WithSummary("Add a criterion to a rubric.")
        .WithDescription("Adds a new criterion to the rubric. Fails with 409 if a criterion with the same name already exists in this rubric, or if the bank is locked by an open/closed session.")
        .Produces<RubricCriterionDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        rubric.MapPut("/criteria/{criterionId:guid}", async (
            Guid bankId,
            Guid criterionId,
            [FromBody] UpdateRubricCriterionRequest request,
            [FromServices] IRubricService service,
            CancellationToken ct) =>
        {
            var errors = RubricValidation.Validate(request);
            if (errors is not null)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await service.UpdateCriterionAsync(bankId, criterionId, request, ct);
            return ToResult(result, dto => Results.Ok(dto));
        })
        .WithName("UpdateRubricCriterion")
        .WithSummary("Update a rubric criterion.")
        .WithDescription("Updates all fields of an existing criterion. Fails with 409 if another criterion has the same name in this rubric, or if the bank is locked by an open/closed session.")
        .Produces<RubricCriterionDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        rubric.MapDelete("/criteria/{criterionId:guid}", async (
            Guid bankId,
            Guid criterionId,
            [FromServices] IRubricService service,
            CancellationToken ct) =>
        {
            var result = await service.DeleteCriterionAsync(bankId, criterionId, ct);
            return ToResult(result, _ => Results.NoContent());
        })
        .WithName("DeleteRubricCriterion")
        .WithSummary("Delete a rubric criterion.")
        .WithDescription("Deletes an evaluation criterion. Fails with 404 if the criterion does not belong to the rubric, or with 409 if the bank is locked by an open/closed session.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static IResult ToResult<T>(RubricServiceResult<T> result, Func<T, IResult> success)
    {
        if (result.IsSuccess)
        {
            return success(result.Value!);
        }

        return result.Error switch
        {
            RubricServiceError.BankNotFound or
            RubricServiceError.RubricNotFound or
            RubricServiceError.CriterionNotFound =>
                Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: result.Detail),

            RubricServiceError.RubricAlreadyExists =>
                Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.Detail,
                    extensions: new Dictionary<string, object?> { ["code"] = "rubric_already_exists" }),

            RubricServiceError.RubricHasCriteria =>
                Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.Detail,
                    extensions: new Dictionary<string, object?> { ["code"] = "rubric_has_criteria" }),

            RubricServiceError.CriterionNameExists =>
                Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.Detail,
                    extensions: new Dictionary<string, object?> { ["code"] = "criterion_name_exists" }),

            RubricServiceError.PreparationLocked =>
                Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.Detail,
                    extensions: new Dictionary<string, object?> { ["code"] = "rubric_preparation_locked" }),

            RubricServiceError.Invalid =>
                result.ValidationErrors is not null
                    ? Results.ValidationProblem(result.ValidationErrors)
                    : Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: result.Detail),

            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: result.Detail)
        };
    }
}
