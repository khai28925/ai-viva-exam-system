using AiViva.Application.Abstractions;
using AiViva.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure.Modules.LegacyExams;

public static class LegacyExamsInfrastructureModule
{
    public static IServiceCollection AddLegacyExamsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IExamRepository, InMemoryExamRepository>();
        return services;
    }
}
