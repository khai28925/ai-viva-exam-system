using AiViva.Application.Abstractions;
using AiViva.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IExamRepository, InMemoryExamRepository>();
        services.AddSingleton<IQuestionBankRepository, InMemoryQuestionBankRepository>();
        services.AddSingleton<IQuestionRepository, InMemoryQuestionRepository>();
        return services;
    }
}
