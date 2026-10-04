using AiViva.Application.Abstractions;
using AiViva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("QuestionBank");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:QuestionBank is required. Set the ConnectionStrings__QuestionBank environment variable.");
        }

        services.AddScoped<IExamRepository, InMemoryExamRepository>();
        services.AddDbContext<QuestionBankDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IQuestionBankRepository, PostgresQuestionBankRepository>();
        return services;
    }
}
