using AiViva.Application.Modules.LegacyExams;
using AiViva.Application.Modules.QuestionPreparation;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddLegacyExamsApplication();
        services.AddQuestionPreparationApplication();
        return services;
    }
}
