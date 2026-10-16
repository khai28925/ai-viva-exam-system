using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace AiViva.Tests;

public sealed class RubricPersistenceModelTests
{
    private static QuestionBankDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseNpgsql("Host=localhost;Database=aives;Username=aives")
            .Options;
        return new QuestionBankDbContext(options);
    }

    [Fact]
    public void ModelMatchesRubricContract()
    {
        using var db = CreateContext();
        var designModel = db.GetService<IDesignTimeModel>().Model;
        var rubric = designModel.FindEntityType(typeof(Rubric))!;
        var criterion = designModel.FindEntityType(typeof(RubricCriterion))!;
        var bank = designModel.FindEntityType(typeof(QuestionBank))!;

        var rubricTable = StoreObjectIdentifier.Table("rubrics", null);
        var criterionTable = StoreObjectIdentifier.Table("rubric_criteria", null);

        // Rubric table assertions
        Assert.Equal("rubrics", rubric.GetTableName());
        Assert.Equal("id", rubric.FindProperty(nameof(Rubric.Id))!.GetColumnName(rubricTable));
        Assert.Equal("question_bank_id", rubric.FindProperty(nameof(Rubric.QuestionBankId))!.GetColumnName(rubricTable));
        Assert.Equal("name", rubric.FindProperty(nameof(Rubric.Name))!.GetColumnName(rubricTable));
        Assert.Equal(120, rubric.FindProperty(nameof(Rubric.Name))!.GetMaxLength());
        Assert.False(rubric.FindProperty(nameof(Rubric.Name))!.IsNullable);
        Assert.Equal(500, rubric.FindProperty(nameof(Rubric.Description))!.GetMaxLength());
        Assert.True(rubric.FindProperty(nameof(Rubric.Description))!.IsNullable);

        var bankFk = Assert.Single(rubric.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Restrict, bankFk.DeleteBehavior);
        Assert.Equal(bank, bankFk.PrincipalEntityType);

        var bankIndex = Assert.Single(rubric.GetIndexes());
        Assert.True(bankIndex.IsUnique);
        Assert.Contains(bankIndex.Properties, p => p.Name == nameof(Rubric.QuestionBankId));

        // RubricCriterion table assertions
        Assert.Equal("rubric_criteria", criterion.GetTableName());
        Assert.Equal("id", criterion.FindProperty(nameof(RubricCriterion.Id))!.GetColumnName(criterionTable));
        Assert.Equal("rubric_id", criterion.FindProperty(nameof(RubricCriterion.RubricId))!.GetColumnName(criterionTable));
        Assert.Equal("name", criterion.FindProperty(nameof(RubricCriterion.Name))!.GetColumnName(criterionTable));
        Assert.Equal(120, criterion.FindProperty(nameof(RubricCriterion.Name))!.GetMaxLength());
        Assert.False(criterion.FindProperty(nameof(RubricCriterion.Name))!.IsNullable);
        Assert.Equal(500, criterion.FindProperty(nameof(RubricCriterion.Description))!.GetMaxLength());
        Assert.True(criterion.FindProperty(nameof(RubricCriterion.Description))!.IsNullable);

        var maxScoreProperty = criterion.FindProperty(nameof(RubricCriterion.MaxScore))!;
        Assert.Equal(10, maxScoreProperty.GetPrecision());
        Assert.Equal(2, maxScoreProperty.GetScale());

        var weightProperty = criterion.FindProperty(nameof(RubricCriterion.Weight))!;
        Assert.Equal(5, weightProperty.GetPrecision());
        Assert.Equal(4, weightProperty.GetScale());

        var rubricFk = Assert.Single(criterion.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Restrict, rubricFk.DeleteBehavior);
        Assert.Equal(rubric, rubricFk.PrincipalEntityType);

        var criterionIndex = Assert.Single(criterion.GetIndexes());
        Assert.True(criterionIndex.IsUnique);
        Assert.Contains(criterionIndex.Properties, p => p.Name == nameof(RubricCriterion.RubricId));
        Assert.Contains(criterionIndex.Properties, p => p.Name == nameof(RubricCriterion.Name));

        var checkConstraints = criterion.GetCheckConstraints().ToArray();
        Assert.Contains(checkConstraints, c => c.Name == "ck_rubric_criteria_max_score" && c.Sql == "max_score > 0");
        Assert.Contains(checkConstraints, c => c.Name == "ck_rubric_criteria_weight" && c.Sql == "weight > 0 AND weight <= 1");
    }

    [Fact]
    public void FullVivaMigrationCreatesRubricTablesConstraintsAndIndexes()
    {
        using var db = CreateContext();
        var sql = db.GetService<IMigrator>().GenerateScript(
            fromMigration: "20261005022345_AddBasicExamSessions",
            toMigration: "20261008043407_AddFullVivaSchema");

        Assert.Contains("CREATE TABLE rubrics", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE rubric_criteria", sql, StringComparison.Ordinal);
        Assert.Contains("ck_rubric_criteria_max_score", sql, StringComparison.Ordinal);
        Assert.Contains("ck_rubric_criteria_weight", sql, StringComparison.Ordinal);
        Assert.Contains("IX_rubrics_question_bank_id", sql, StringComparison.Ordinal);
        Assert.Contains("IX_rubric_criteria_rubric_id_name", sql, StringComparison.Ordinal);
        Assert.Contains("ON DELETE RESTRICT", sql, StringComparison.Ordinal);
    }
}
