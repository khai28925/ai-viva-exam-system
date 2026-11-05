namespace AiViva.Api.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/health", () => TypedResults.Ok(new
        {
            status = "healthy",
            service = "ai-viva-api",
            utcTime = DateTimeOffset.UtcNow
        }))
        .WithName("GetHealth")
        .WithTags("Health")
        .WithSummary("Check API availability (not database readiness).");

        return group;
    }
}
