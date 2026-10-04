using AiViva.Application.Abstractions;
using AiViva.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiViva.Infrastructure.Persistence;

public sealed class PostgresQuestionBankRepository(QuestionBankDbContext db) : IQuestionBankRepository
{
    public async Task<IReadOnlyCollection<QuestionBank>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.QuestionBanks
            .AsNoTracking()
            .OrderBy(bank => bank.CreatedAt)
            .ThenBy(bank => bank.Id)
            .ToArrayAsync(cancellationToken);
    }

    public Task<QuestionBank?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return db.QuestionBanks
            .AsNoTracking()
            .FirstOrDefaultAsync(bank => bank.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        QuestionBank questionBank,
        CancellationToken cancellationToken = default)
    {
        db.QuestionBanks.Add(questionBank);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        QuestionBank questionBank,
        CancellationToken cancellationToken = default)
    {
        db.QuestionBanks.Attach(questionBank);
        var entry = db.Entry(questionBank);
        entry.Property(bank => bank.Name).IsModified = true;
        entry.Property(bank => bank.Description).IsModified = true;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<QuestionBankDeleteResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!await db.QuestionBanks.AnyAsync(bank => bank.Id == id, cancellationToken))
        {
            return QuestionBankDeleteResult.NotFound;
        }

        if (await db.Questions.AnyAsync(question => question.QuestionBankId == id, cancellationToken))
        {
            return QuestionBankDeleteResult.HasQuestions;
        }

        try
        {
            var deleted = await db.QuestionBanks
                .Where(bank => bank.Id == id)
                .ExecuteDeleteAsync(cancellationToken);
            return deleted == 0 ? QuestionBankDeleteResult.NotFound : QuestionBankDeleteResult.Deleted;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return QuestionBankDeleteResult.HasQuestions;
        }
    }

    public async Task<IReadOnlyCollection<Question>> GetQuestionsAsync(
        Guid questionBankId,
        CancellationToken cancellationToken = default)
    {
        return await db.Questions
            .AsNoTracking()
            .Where(question => question.QuestionBankId == questionBankId)
            .OrderBy(question => question.CreatedAt)
            .ThenBy(question => question.Id)
            .ToArrayAsync(cancellationToken);
    }

    public Task<Question?> GetQuestionByIdAsync(
        Guid questionBankId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        return db.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                question => question.Id == questionId && question.QuestionBankId == questionBankId,
                cancellationToken);
    }

    public async Task<bool> AddQuestionAsync(
        Question question,
        CancellationToken cancellationToken = default)
    {
        if (!await db.QuestionBanks.AnyAsync(bank => bank.Id == question.QuestionBankId, cancellationToken))
        {
            return false;
        }

        db.Questions.Add(question);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            return false;
        }
    }

    public async Task<bool> UpdateQuestionAsync(
        Question question,
        CancellationToken cancellationToken = default)
    {
        db.Questions.Attach(question);
        db.Entry(question).Property(item => item.Content).IsModified = true;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteQuestionAsync(
        Guid questionBankId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await db.Questions
            .Where(question => question.Id == questionId && question.QuestionBankId == questionBankId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }
}
