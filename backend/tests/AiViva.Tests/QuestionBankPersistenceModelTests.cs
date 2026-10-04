using AiViva.Domain.Entities;
using AiViva.Infrastructure;
using AiViva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiViva.Tests;

public sealed class QuestionBankPersistenceModelTests
{
    private static QuestionBankDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseNpgsql("Host=localhost;Database=aives;Username=aives")
            .Options;
        return new QuestionBankDbContext(options);
    }

    [Fact]
    public void InfrastructureRequiresConnectionString()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();

        var exception = Assert.Throws<InvalidOperationException>(
            () => { services.AddInfrastructure(configuration); });

        Assert.Contains("ConnectionStrings__QuestionBank", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InfrastructureRegistersPostgresRepository()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager
        {
            ["ConnectionStrings:QuestionBank"] = "Host=localhost;Database=aives;Username=aives"
        };
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<AiViva.Application.Abstractions.IQuestionBankRepository>();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();

        Assert.IsType<PostgresQuestionBankRepository>(repository);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
    }

    [Fact]
    public void ModelMatchesQuestionBankContract()
    {
        using var db = CreateContext();
        var bank = db.Model.FindEntityType(typeof(QuestionBank))!;
        var question = db.Model.FindEntityType(typeof(Question))!;
        var bankTable = StoreObjectIdentifier.Table("question_banks", null);
        var questionTable = StoreObjectIdentifier.Table("questions", null);

        Assert.Equal("question_banks", bank.GetTableName());
        Assert.Equal("questions", question.GetTableName());
        Assert.Equal("id", bank.FindProperty(nameof(QuestionBank.Id))!.GetColumnName(bankTable));
        Assert.Equal("name", bank.FindProperty(nameof(QuestionBank.Name))!.GetColumnName(bankTable));
        Assert.Equal(120, bank.FindProperty(nameof(QuestionBank.Name))!.GetMaxLength());
        Assert.Equal(500, bank.FindProperty(nameof(QuestionBank.Description))!.GetMaxLength());
        Assert.Equal("question_bank_id", question.FindProperty(nameof(Question.QuestionBankId))!.GetColumnName(questionTable));
        Assert.Equal(2000, question.FindProperty(nameof(Question.Content))!.GetMaxLength());
        Assert.Equal("timestamp with time zone", question.FindProperty(nameof(Question.CreatedAt))!.GetColumnType());

        var foreignKey = Assert.Single(question.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(bank, foreignKey.PrincipalEntityType);
        Assert.Equal("ix_questions_question_bank_id", Assert.Single(question.GetIndexes()).GetDatabaseName());
    }

    [Fact]
    public void InitialMigrationCreatesBothTablesAndRestrictForeignKey()
    {
        using var db = CreateContext();
        var sql = db.GetService<IMigrator>().GenerateScript();

        Assert.Contains("CREATE TABLE question_banks", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE questions", sql, StringComparison.Ordinal);
        Assert.Contains("ON DELETE RESTRICT", sql, StringComparison.Ordinal);
        Assert.Contains("ix_questions_question_bank_id", sql, StringComparison.Ordinal);
    }
}
