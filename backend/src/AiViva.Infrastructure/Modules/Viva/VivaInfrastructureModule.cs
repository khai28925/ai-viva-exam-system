using AiViva.Application.ExamSessions;
using AiViva.Infrastructure.Persistence;
using AiViva.ModuleContracts.Viva;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure.Modules.Viva;

public static class VivaInfrastructureModule
{
    public static IServiceCollection AddVivaInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IExamSessionService, PostgresExamSessionService>();
        services.AddScoped<IExamUsageReader, PostgresExamUsageReader>();
        return services;
    }
}
