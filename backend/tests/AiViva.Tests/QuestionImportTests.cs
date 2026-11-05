using AiViva.Application.Abstractions;
using AiViva.Application.Questions;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Modules.QuestionPreparation;
using AiViva.Infrastructure.Modules.Viva;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Persistence.Models;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiViva.Tests;

public sealed class QuestionImportTests : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    private readonly QuestionBankDbContext db;
    private readonly QuestionBank bank = new("Kiến trúc phần mềm");

    public QuestionImportTests()
    {
        connection.Open();
        connection.CreateFunction("now", () => DateTimeOffset.UtcNow.ToString("O"));
        db = new(new DbContextOptionsBuilder<QuestionBankDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        var subject = new Subject("Kiến trúc phần mềm", null, null);
        bank.AssignSubject(subject.Id);
        db.Subjects.Add(subject);
        db.QuestionBanks.Add(bank);
        db.SaveChanges();
    }

    [Fact]
    public void Parser_accepts_vietnamese_excel_csv_with_semicolons_quotes_and_bloom_labels()
    {
        const string csv = "﻿Nội dung;Mức Bloom;Độ khó;Ý chính\r\n" +
            "\"Giải thích DI; cho ví dụ\";Hiểu;2;Tách phụ thuộc|Dễ kiểm thử\r\n" +
            "\"Phân tích \"\"layered\"\"\nkiến trúc\";phân tích;;\r\n" +
            ";;;\r\n" +
            "So sánh REST và gRPC;Apply;5;Giao thức\n";

        var (rows, errors) = QuestionCsvParser.Parse(csv);

        errors.Should().BeEmpty();
        rows.Should().HaveCount(3);
        rows[0].Content.Should().Be("Giải thích DI; cho ví dụ");
        rows[0].BloomLevel.Should().Be("Understand");
        rows[0].DifficultyLevel.Should().Be(2);
        rows[0].ExpectedKeyPoints.Should().Equal("Tách phụ thuộc", "Dễ kiểm thử");
        rows[1].Content.Should().Be("Phân tích \"layered\"\nkiến trúc");
        rows[1].BloomLevel.Should().Be("Analyze");
        rows[1].DifficultyLevel.Should().BeNull();
        rows[1].ExpectedKeyPoints.Should().BeEmpty();
        rows[2].BloomLevel.Should().Be("Apply");
        rows[2].RowNumber.Should().Be(5);
    }

    [Fact]
    public void Parser_reports_every_invalid_row_with_its_number()
    {
        const string csv = "content,bloomLevel,difficultyLevel,expectedKeyPoints\n" +
            "Valid question?,Remember,1,Point\n" +
            ",Remember,1,\n" +
            "Bad bloom?,Create,9,\n" +
            "Valid question?,,,\n";

        var (rows, errors) = QuestionCsvParser.Parse(csv);

        rows.Should().ContainSingle();
        errors.Select(e => e.RowNumber).Should().Equal(3, 4, 5);
        errors[1].Message.Should().Contain("Bloom").And.Contain("độ khó");
        errors[2].Message.Should().Contain("trùng");
    }

    [Fact]
    public void Parser_requires_a_content_column()
    {
        var (rows, errors) = QuestionCsvParser.Parse("bloom,difficulty\nHiểu,2\n");

        rows.Should().BeEmpty();
        errors.Should().ContainSingle().Which.Message.Should().Contain("content");
    }

    [Fact]
    public async Task Import_creates_questions_with_classification_and_expected_key_points()
    {
        var service = new PostgresQuestionImportService(db);

        var result = await service.ImportAsync(bank.Id,
            "content,bloom,difficulty,keyPoints\nGiải thích DI?,Hiểu,2,Tách phụ thuộc|Dễ kiểm thử\nNêu SOLID?,Nhớ,1,\n");

        result!.ImportedCount.Should().Be(2);
        result.Errors.Should().BeEmpty();
        result.Questions.Should().Contain(q => q.Content == "Giải thích DI?" && q.BloomLevel == "Understand" && q.DifficultyLevel == 2);
        var di = await db.Questions.SingleAsync(q => q.Content == "Giải thích DI?");
        (await db.ExpectedKeyPoints.Where(p => p.QuestionId == di.Id).OrderBy(p => p.DisplayOrder)
            .Select(p => p.Content).ToArrayAsync()).Should().Equal("Tách phụ thuộc", "Dễ kiểm thử");
    }

    [Fact]
    public async Task Import_is_all_or_nothing_and_rejects_questions_already_in_the_bank()
    {
        db.Questions.Add(new Question(bank.Id, "Nêu SOLID?"));
        await db.SaveChangesAsync();
        var service = new PostgresQuestionImportService(db);

        var result = await service.ImportAsync(bank.Id, "content\nCâu mới?\nnêu solid?\n");

        result!.ImportedCount.Should().Be(0);
        result.Errors.Should().ContainSingle(e => e.RowNumber == 3 && e.Message.Contains("đã có"));
        (await db.Questions.CountAsync()).Should().Be(1);
        (await service.ImportAsync(Guid.NewGuid(), "content\nX?\n")).Should().BeNull();
    }

    [Fact]
    public async Task Deleting_an_empty_bank_also_removes_its_obsolete_legacy_rubric()
    {
        var rubric = new Rubric { Id = Guid.NewGuid(), QuestionBankId = bank.Id, Name = "Legacy", CreatedAt = DateTimeOffset.UtcNow };
        db.Rubrics.Add(rubric);
        db.RubricCriteria.Add(new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubric.Id,
            Name = "Accuracy",
            MaxScore = 10,
            Weight = 0.5m,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var repository = new PostgresQuestionBankRepository(db, new PostgresExamUsageReader(db));

        var result = await repository.DeleteAsync(bank.Id);

        result.Should().Be(QuestionBankDeleteResult.Deleted);
        (await db.Rubrics.CountAsync()).Should().Be(0);
        (await db.RubricCriteria.CountAsync()).Should().Be(0);
    }

    public async ValueTask DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }
}
