using AiViva.Application.Questions;

namespace AiViva.Api.Endpoints;

public static class QuestionEndpoints
{
    public static RouteGroupBuilder MapQuestionEndpoints(this RouteGroupBuilder group)
    {
        var questions = group.MapGroup("/v1/questions")
            .WithTags("Questions");

        questions.MapGet("/", async (
            IQuestionService service,
            CancellationToken ct,
            Guid? questionBankId = null) =>
        {
            var result = questionBankId.HasValue
                ? await service.GetByQuestionBankIdAsync(questionBankId.Value, ct)
                : await service.GetAllAsync(ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetQuestions")
        .WithSummary("List questions, optionally filtered by question bank.")
        .WithDescription("Returns all non-deleted questions. Pass ?questionBankId=<guid> to filter by bank.");

        questions.MapGet("/{id:guid}", async (
            Guid id,
            IQuestionService service,
            CancellationToken ct) =>
        {
            var dto = await service.GetByIdAsync(id, ct);
            return dto is not null
                ? Results.Ok(dto)
                : Results.Problem(
                    title: "Question not found",
                    detail: $"No question with id '{id}' exists.",
                    statusCode: StatusCodes.Status404NotFound);
        })
        .WithName("GetQuestionById")
        .WithSummary("Get a question by ID.")
        .WithDescription("Returns a single question with its type, difficulty, and points.")
        .ProducesProblem(StatusCodes.Status404NotFound);

        questions.MapPost("/", async (
            CreateQuestionRequest request,
            IQuestionService service,
            CancellationToken ct) =>
        {
            var dto = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/v1/questions/{dto.Id}", dto);
        })
        .WithName("CreateQuestion")
        .WithSummary("Create a new question.")
        .WithDescription("Creates a question within an existing question bank. Content, Type, and DifficultyLevel are required.")
        .ProducesValidationProblem();

        questions.MapPut("/{id:guid}", async (
            Guid id,
            UpdateQuestionRequest request,
            IQuestionService service,
            CancellationToken ct) =>
        {
            var dto = await service.UpdateAsync(id, request, ct);
            return dto is not null
                ? Results.Ok(dto)
                : Results.Problem(
                    title: "Question not found",
                    detail: $"No question with id '{id}' exists.",
                    statusCode: StatusCodes.Status404NotFound);
        })
        .WithName("UpdateQuestion")
        .WithSummary("Update a question.")
        .WithDescription("Partially updates a question. Only provided fields are changed.")
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

        questions.MapDelete("/{id:guid}", async (
            Guid id,
            IQuestionService service,
            CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(id, ct);
            return deleted
                ? Results.NoContent()
                : Results.Problem(
                    title: "Question not found",
                    detail: $"No question with id '{id}' exists.",
                    statusCode: StatusCodes.Status404NotFound);
        })
        .WithName("DeleteQuestion")
        .WithSummary("Soft-delete a question.")
        .WithDescription("Marks a question as deleted. The record is not physically removed.")
        .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
