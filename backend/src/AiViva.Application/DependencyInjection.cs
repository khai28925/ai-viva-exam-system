using AiViva.Application.Exams;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IExamService, ExamService>();
        return services;
    }
}
