using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiViva.Infrastructure.Persistence;

public sealed class QuestionBankDbContextFactory : IDesignTimeDbContextFactory<QuestionBankDbContext>
{
    public QuestionBankDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__QuestionBank");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__QuestionBank before running dotnet ef commands.");
        }

        var options = new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new QuestionBankDbContext(options);
    }
}
