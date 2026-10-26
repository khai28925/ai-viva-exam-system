using AiViva.Application.ExpectedKeyPoints;
using AiViva.Application.QuestionBanks;
using AiViva.Application.Questions;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Application.Modules.QuestionPreparation;

public static class QuestionPreparationApplicationModule
{
    public static IServiceCollection AddQuestionPreparationApplication(this IServiceCollection services)
    {
        services.AddScoped<IQuestionBankService, QuestionBankService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IExpectedKeyPointsService, ExpectedKeyPointsService>();
        return services;
    }
}
