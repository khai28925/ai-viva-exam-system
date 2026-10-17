using AiViva.Application.Rubrics;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure.Rubrics;

public static class RubricServiceCollectionExtensions
{
    public static IServiceCollection AddRubricServices(this IServiceCollection services)
    {
        services.AddScoped<IRubricService, PostgresRubricService>();
        return services;
    }
}
