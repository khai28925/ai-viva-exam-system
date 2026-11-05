using AiViva.Application.ExamSessions;
using AiViva.Application.Modules.Viva;
using AiViva.Infrastructure.Persistence;
using AiViva.ModuleContracts.Viva;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure.Modules.Viva;

public static class VivaInfrastructureModule
{
    public static IServiceCollection AddVivaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IExamSessionService, PostgresExamSessionService>();
        services.AddScoped<IExamUsageReader, PostgresExamUsageReader>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddHttpClient<IInterviewAi, GeminiInterviewAi>(client =>
        {
            client.BaseAddress = new Uri(configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(90);
        });
        return services;
    }
}
