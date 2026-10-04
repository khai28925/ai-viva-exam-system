using AiViva.Application.QuestionBanks;

namespace AiViva.Api.Endpoints;

public static class QuestionBankEndpoints
{
    public static RouteGroupBuilder MapQuestionBankEndpoints(this RouteGroupBuilder group)
    {
        var banks = group.MapGroup("/v1/question-banks")
            .WithTags("Question Banks");

        banks.MapGet("/", async (
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var result = await service.GetAllAsync(ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetQuestionBanks")
        .WithSummary("List all question banks.")
        .WithDescription("Returns every non-deleted question bank with its question count.");

        banks.MapGet("/{id:guid}", async (
            Guid id,
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var dto = await service.GetByIdAsync(id, ct);
            return dto is not null
                ? Results.Ok(dto)
                : Results.Problem(
                    title: "Question bank not found",
                    detail: $"No question bank with id '{id}' exists.",
                    statusCode: StatusCodes.Status404NotFound);
        })
        .WithName("GetQuestionBankById")
        .WithSummary("Get a question bank by ID.")
        .WithDescription("Returns a single question bank including its question count.")
        .ProducesProblem(StatusCodes.Status404NotFound);

        banks.MapPost("/", async (
            CreateQuestionBankRequest request,
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var dto = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/v1/question-banks/{dto.Id}", dto);
        })
        .WithName("CreateQuestionBank")
        .WithSummary("Create a new question bank.")
        .WithDescription("Creates a question bank. Title and SubjectId are required.")
        .ProducesValidationProblem();

        banks.MapPut("/{id:guid}", async (
            Guid id,
            UpdateQuestionBankRequest request,
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var dto = await service.UpdateAsync(id, request, ct);
            return dto is not null
                ? Results.Ok(dto)
                : Results.Problem(
                    title: "Question bank not found",
                    detail: $"No question bank with id '{id}' exists.",
                    statusCode: StatusCodes.Status404NotFound);
        })
        .WithName("UpdateQuestionBank")
        .WithSummary("Update a question bank.")
        .WithDescription("Partially updates a question bank. Only provided fields are changed.")
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

        banks.MapDelete("/{id:guid}", async (
            Guid id,
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted
                ? Results.NoContent()
                : Results.Problem(
                    title: "Question bank not found",
                    detail: $"No question bank with id '{id}' exists.",
                    statusCode: StatusCodes.Status404NotFound);
        })
        .WithName("DeleteQuestionBank")
        .WithSummary("Soft-delete a question bank.")
        .WithDescription("Marks a question bank as deleted. The record is not physically removed.")
        .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
