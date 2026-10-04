using AiViva.Domain.Entities;
using AiViva.Infrastructure;
using AiViva.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiViva.Tests;

public sealed class AuthPersistenceModelTests
{
    private static QuestionBankDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseNpgsql("Host=localhost;Database=aives;Username=aives")
            .Options;
        return new QuestionBankDbContext(options);
    }

    [Fact]
    public void UserModelHasUniqueNormalizedEmailAndRequiredRestrictedRole()
    {
        using var db = CreateContext();
        var role = db.Model.FindEntityType(typeof(Role))!;
        var user = db.Model.FindEntityType(typeof(UserAccount))!;
        var userTable = StoreObjectIdentifier.Table("users", null);

        Assert.Equal("roles", role.GetTableName());
        Assert.Equal("users", user.GetTableName());
        Assert.Equal("normalized_email", user.FindProperty(nameof(UserAccount.NormalizedEmail))!.GetColumnName(userTable));
        Assert.Equal(254, user.FindProperty(nameof(UserAccount.NormalizedEmail))!.GetMaxLength());
        Assert.Equal(512, user.FindProperty(nameof(UserAccount.PasswordHash))!.GetMaxLength());
        Assert.Equal("role_id", user.FindProperty(nameof(UserAccount.RoleId))!.GetColumnName(userTable));
        Assert.Equal("timestamp with time zone", user.FindProperty(nameof(UserAccount.CreatedAt))!.GetColumnType());

        var emailIndex = Assert.Single(user.GetIndexes(), index =>
            index.Properties.Any(property => property.Name == nameof(UserAccount.NormalizedEmail)));
        Assert.True(emailIndex.IsUnique);
        var roleForeignKey = Assert.Single(user.GetForeignKeys());
        Assert.True(roleForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, roleForeignKey.DeleteBehavior);
        Assert.Equal(role, roleForeignKey.PrincipalEntityType);
    }

    [Fact]
    public void ModelSeedsExactlyThreeDistinctRoles()
    {
        using var db = CreateContext();
        var role = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Role))!;
        var seeds = role.GetSeedData().ToArray();

        Assert.Equal(3, seeds.Length);
        Assert.Equal(
            new[] { Role.Codes.Admin, Role.Codes.Lecturer, Role.Codes.Student },
            seeds.Select(seed => Assert.IsType<string>(seed[nameof(Role.Code)]))
                .OrderBy(code => code)
                .ToArray());
        Assert.Equal(3, seeds.Select(seed => Assert.IsType<Guid>(seed[nameof(Role.Id)])).Distinct().Count());
    }

    [Fact]
    public void AuthMigrationCreatesRolesUsersConstraintsAndSeeds()
    {
        using var db = CreateContext();
        var sql = db.GetService<IMigrator>().GenerateScript(
            fromMigration: "20261004200441_InitialQuestionBank",
            toMigration: "20261004212256_AddAuthUsers");

        Assert.Contains("CREATE TABLE roles", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE users", sql, StringComparison.Ordinal);
        Assert.Contains("normalized_email", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_users_normalized_email\"", sql, StringComparison.Ordinal);
        Assert.Contains("ON DELETE RESTRICT", sql, StringComparison.Ordinal);
        Assert.Contains("'ADMIN'", sql, StringComparison.Ordinal);
        Assert.Contains("'LECTURER'", sql, StringComparison.Ordinal);
        Assert.Contains("'STUDENT'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void InfrastructureRegistersPasswordHasherAndHashesAreVerifiable()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager
        {
            ["ConnectionStrings:QuestionBank"] = "Host=localhost;Database=aives;Username=aives"
        };
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>();
        var user = new UserAccount("  Student@Example.COM  ", Guid.NewGuid());
        var hash = hasher.HashPassword(user, "test-password-123");
        user.SetPasswordHash(hash);

        Assert.Equal("Student@Example.COM", user.Email);
        Assert.Equal("STUDENT@EXAMPLE.COM", user.NormalizedEmail);
        Assert.NotEqual("test-password-123", user.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, hash, "test-password-123"));
        Assert.Equal(PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, hash, "wrong-password"));
    }
}
