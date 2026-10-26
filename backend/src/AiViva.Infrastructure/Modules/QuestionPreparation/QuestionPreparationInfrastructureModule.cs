using AiViva.Application.Abstractions;
using AiViva.Application.Rubrics;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Rubrics;
using AiViva.ModuleContracts.QuestionPreparation;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure.Modules.QuestionPreparation;

public static class QuestionPreparationInfrastructureModule
{
    public static IServiceCollection AddQuestionPreparationInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IQuestionBankRepository, PostgresQuestionBankRepository>();
        services.AddScoped<IExpectedKeyPoints, ExpectedKeyPointsRepository>();
        services.AddScoped<IRubricService, PostgresRubricService>();
        services.AddScoped<IQuestionPreparationReader, PostgresQuestionPreparationReader>();
        return services;
    }
}
