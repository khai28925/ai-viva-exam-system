using AiViva.Infrastructure.Persistence;
using AiViva.ModuleContracts.QuestionPreparation;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Modules.QuestionPreparation;

public sealed class PostgresQuestionPreparationReader(QuestionBankDbContext db) : IQuestionPreparationReader
{
    public Task<bool> BankExistsAsync(Guid bankId, CancellationToken cancellationToken = default) =>
        db.QuestionBanks.AsNoTracking().AnyAsync(bank => bank.Id == bankId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetBankNamesAsync(
        IReadOnlyCollection<Guid> bankIds, CancellationToken cancellationToken = default)
    {
        var ids = bankIds.Distinct().ToArray();
        return await db.QuestionBanks.AsNoTracking().Where(bank => ids.Contains(bank.Id))
            .ToDictionaryAsync(bank => bank.Id, bank => bank.Name, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetSelectableQuestionsAsync(
        Guid bankId, IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken = default)
    {
        var ids = questionIds.Distinct().ToArray();
        return await db.Questions.AsNoTracking()
            .Where(question => ids.Contains(question.Id) &&
                question.QuestionBankId == bankId && question.QuestionType == "Bank")
            .ToDictionaryAsync(question => question.Id, question => question.Content, cancellationToken);
    }

    public async Task<QuestionPreparationReadiness> GetReadinessAsync(
        Guid bankId, IReadOnlyCollection<Guid> selectedQuestionIds, CancellationToken cancellationToken = default)
    {
        var ids = selectedQuestionIds.Distinct().ToArray();
        var validIds = await db.Questions.AsNoTracking()
            .Where(question => ids.Contains(question.Id) &&
                question.QuestionBankId == bankId && question.QuestionType == "Bank")
            .Select(question => question.Id).ToArrayAsync(cancellationToken);
        var withKeyPoints = await db.ExpectedKeyPoints.AsNoTracking()
            .Where(keyPoint => ids.Contains(keyPoint.QuestionId))
            .Select(keyPoint => keyPoint.QuestionId).Distinct().ToArrayAsync(cancellationToken);
        var rubric = await db.Rubrics.AsNoTracking()
            .SingleOrDefaultAsync(item => item.QuestionBankId == bankId, cancellationToken);
        var weights = rubric is null
            ? []
            : await db.RubricCriteria.AsNoTracking().Where(item => item.RubricId == rubric.Id)
                .Select(item => item.Weight).ToArrayAsync(cancellationToken);

        return new QuestionPreparationReadiness(validIds, withKeyPoints, rubric is not null, weights);
    }
}
