using AiViva.Application.Exams;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Application.Modules.LegacyExams;

public static class LegacyExamsApplicationModule
{
    public static IServiceCollection AddLegacyExamsApplication(this IServiceCollection services)
    {
        services.AddScoped<IExamService, ExamService>();
        return services;
    }
}
