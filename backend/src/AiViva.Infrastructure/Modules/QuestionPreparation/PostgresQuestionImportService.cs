using AiViva.Application.Questions;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Modules.QuestionPreparation;

public sealed class PostgresQuestionImportService(QuestionBankDbContext db) : IQuestionImportService
{
    public async Task<QuestionImportResult?> ImportAsync(Guid questionBankId, string csv, CancellationToken ct = default)
    {
        if (!await db.QuestionBanks.AnyAsync(bank => bank.Id == questionBankId, ct)) return null;
        var (rows, parseErrors) = QuestionCsvParser.Parse(csv);
        var existing = (await db.Questions.AsNoTracking().Where(q => q.QuestionBankId == questionBankId)
            .Select(q => q.Content).ToArrayAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var errors = parseErrors.Concat(rows.Where(row => existing.Contains(row.Content))
                .Select(row => new QuestionImportError(row.RowNumber, "câu hỏi đã có trong bộ câu hỏi")))
            .OrderBy(error => error.RowNumber).ToArray();
        if (errors.Length > 0) return new QuestionImportResult(0, errors, []);

        var now = DateTimeOffset.UtcNow;
        var questions = new List<Question>();
        foreach (var row in rows)
        {
            var question = new Question(questionBankId, row.Content);
            question.UpdateClassification(row.BloomLevel, row.DifficultyLevel);
            questions.Add(question);
            db.Questions.Add(question);
            db.ExpectedKeyPoints.AddRange(row.ExpectedKeyPoints.Select((point, index) => new ExpectedKeyPoint
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Content = point,
                DisplayOrder = index,
                CreatedAt = now
            }));
        }
        await db.SaveChangesAsync(ct);
        return new QuestionImportResult(questions.Count, [], questions.Select(q => new QuestionDto(
            q.Id, q.QuestionBankId, q.Content, q.CreatedAt, q.BloomLevel, q.DifficultyLevel)).ToArray());
    }
}
