using System.ComponentModel.DataAnnotations;
using AiViva.Application.Exams;

namespace AiViva.Api.Endpoints;

public static class ExamEndpoints
{
    public static RouteGroupBuilder MapExamEndpoints(this RouteGroupBuilder group)
    {
        var exams = group.MapGroup("/v1/exams");

        exams.MapGet("/", async (
            IExamService examService,
            CancellationToken cancellationToken,
            [Range(1, 100)] int limit = 50) =>
        {
            var result = await examService.GetAllAsync(cancellationToken);
            return TypedResults.Ok(result.Take(limit).ToArray());
        })
        .WithName("GetExams")
        .WithTags("Exams")
        .WithSummary("List sample exams (limit: 1–100, default: 50).")
        .ProducesValidationProblem();

        return group;
    }
}
