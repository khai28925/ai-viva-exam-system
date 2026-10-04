using AiViva.Application.Abstractions;
using AiViva.Application.QuestionBanks;
using AiViva.Application.Questions;

namespace AiViva.Api.Endpoints;

public static class QuestionBankEndpoints
{
    public static RouteGroupBuilder MapQuestionBankEndpoints(this RouteGroupBuilder group)
    {
        var banks = group.MapGroup("/v1/question-banks")
            .WithTags("Question Banks");

        // Question Bank Endpoints
        banks.MapGet("/", async (
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var result = await service.GetAllAsync(ct);
            return Results.Ok(result);
        })
        .WithName("GetQuestionBanks")
        .WithSummary("List all question banks.")
        .Produces<IReadOnlyCollection<QuestionBankDto>>(StatusCodes.Status200OK);

        banks.MapGet("/{bankId:guid}", async (
            Guid bankId,
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var dto = await service.GetByIdAsync(bankId, ct);
            return dto is not null
                ? Results.Ok(dto)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Question bank with id '{bankId}' was not found.");
        })
        .WithName("GetQuestionBankById")
        .WithSummary("Get a question bank by ID.")
        .Produces<QuestionBankDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        banks.MapPost("/", async (
            CreateQuestionBankRequest request,
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var dto = await service.CreateAsync(request, ct);
            return Results.Created($"/api/v1/question-banks/{dto.Id}", dto);
        })
        .WithName("CreateQuestionBank")
        .WithSummary("Create a new question bank.")
        .Produces<QuestionBankDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        banks.MapPut("/{bankId:guid}", async (
            Guid bankId,
            UpdateQuestionBankRequest request,
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var dto = await service.UpdateAsync(bankId, request, ct);
            return dto is not null
                ? Results.Ok(dto)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Question bank with id '{bankId}' was not found.");
        })
        .WithName("UpdateQuestionBank")
        .WithSummary("Update a question bank.")
        .Produces<QuestionBankDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

        banks.MapDelete("/{bankId:guid}", async (
            Guid bankId,
            IQuestionBankService service,
            CancellationToken ct) =>
        {
            var result = await service.DeleteAsync(bankId, ct);
            return result switch
            {
                QuestionBankDeleteResult.Deleted => Results.NoContent(),
                QuestionBankDeleteResult.HasQuestions => Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: "Cannot delete a question bank that still contains questions."),
                _ => Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Question bank with id '{bankId}' was not found.")
            };
        })
        .WithName("DeleteQuestionBank")
        .WithSummary("Delete a question bank.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // Question Sub-resource Endpoints
        banks.MapGet("/{bankId:guid}/questions", async (
            Guid bankId,
            IQuestionService service,
            CancellationToken ct) =>
        {
            var questions = await service.GetByQuestionBankIdAsync(bankId, ct);
            return questions is not null
                ? Results.Ok(questions)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Question bank with id '{bankId}' was not found.");
        })
        .WithName("GetQuestionsByBankId")
        .WithSummary("List all questions in a question bank.")
        .Produces<IReadOnlyCollection<QuestionDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        banks.MapGet("/{bankId:guid}/questions/{questionId:guid}", async (
            Guid bankId,
            Guid questionId,
            IQuestionService service,
            CancellationToken ct) =>
        {
            var question = await service.GetByIdAsync(bankId, questionId, ct);
            return question is not null
                ? Results.Ok(question)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Question with id '{questionId}' was not found in question bank '{bankId}'.");
        })
        .WithName("GetQuestionById")
        .WithSummary("Get a question by ID within a question bank.")
        .Produces<QuestionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        banks.MapPost("/{bankId:guid}/questions", async (
            Guid bankId,
            CreateQuestionRequest request,
            IQuestionService service,
            CancellationToken ct) =>
        {
            var dto = await service.CreateAsync(bankId, request, ct);
            return dto is not null
                ? Results.Created($"/api/v1/question-banks/{bankId}/questions/{dto.Id}", dto)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Question bank with id '{bankId}' was not found.");
        })
        .WithName("CreateQuestion")
        .WithSummary("Create a question within a question bank.")
        .Produces<QuestionDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

        banks.MapPut("/{bankId:guid}/questions/{questionId:guid}", async (
            Guid bankId,
            Guid questionId,
            UpdateQuestionRequest request,
            IQuestionService service,
            CancellationToken ct) =>
        {
            var dto = await service.UpdateAsync(bankId, questionId, request, ct);
            return dto is not null
                ? Results.Ok(dto)
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Question with id '{questionId}' was not found in question bank '{bankId}'.");
        })
        .WithName("UpdateQuestion")
        .WithSummary("Update a question within a question bank.")
        .Produces<QuestionDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

        banks.MapDelete("/{bankId:guid}/questions/{questionId:guid}", async (
            Guid bankId,
            Guid questionId,
            IQuestionService service,
            CancellationToken ct) =>
        {
            var deleted = await service.DeleteAsync(bankId, questionId, ct);
            return deleted
                ? Results.NoContent()
                : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: $"Question with id '{questionId}' was not found in question bank '{bankId}'.");
        })
        .WithName("DeleteQuestion")
        .WithSummary("Delete a question within a question bank.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
