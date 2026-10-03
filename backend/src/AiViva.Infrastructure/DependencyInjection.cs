using AiViva.Application.Abstractions;
using AiViva.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IExamRepository, InMemoryExamRepository>();
        services.AddSingleton<IQuestionBankRepository, InMemoryQuestionBankRepository>();
        return services;
    }
}
