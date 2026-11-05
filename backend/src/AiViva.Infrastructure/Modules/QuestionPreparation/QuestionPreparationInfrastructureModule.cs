using AiViva.Application.Abstractions;
using AiViva.Application.Modules.QuestionPreparation.Wf1;
using AiViva.Application.Questions;
using AiViva.Application.Rubrics;
using AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Rubrics;
using AiViva.ModuleContracts.QuestionPreparation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure.Modules.QuestionPreparation;

public static class QuestionPreparationInfrastructureModule
{
    public static IServiceCollection AddQuestionPreparationInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IQuestionBankRepository, PostgresQuestionBankRepository>();
        services.AddScoped<IQuestionImportService, PostgresQuestionImportService>();
        services.AddScoped<IExpectedKeyPoints, ExpectedKeyPointsRepository>();
        services.AddScoped<IRubricService, PostgresRubricService>();
        services.AddScoped<IQuestionPreparationReader, PostgresQuestionPreparationReader>();
        services.AddScoped<IWf1PreparationService, Wf1PreparationService>();
        services.AddHttpClient<IAiContentGenerator, GeminiContentGenerator>(client =>
        {
            client.BaseAddress = new Uri(configuration["Gemini:BaseUrl"] ??
                "https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        services.AddHttpClient<IEmbeddingGenerator, GeminiEmbeddingGenerator>(client =>
        {
            client.BaseAddress = new Uri(configuration["Gemini:BaseUrl"] ??
                "https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddSingleton<IDocumentTextExtractor, PdfPigTextExtractor>();
        return services;
    }
}
