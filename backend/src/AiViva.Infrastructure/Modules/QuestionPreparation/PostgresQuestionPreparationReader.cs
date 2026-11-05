using AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;
using AiViva.Infrastructure.Persistence;
using AiViva.ModuleContracts.QuestionPreparation;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Modules.QuestionPreparation;

public sealed class PostgresQuestionPreparationReader(QuestionBankDbContext db) : IQuestionPreparationReader
{
    public async Task<AssessmentContext?> GetAssessmentContextAsync(
        Guid questionId, Guid rubricSetId, CancellationToken cancellationToken = default)
    {
        var subjectId = await db.Questions.Where(q => q.Id == questionId)
            .Join(db.QuestionBanks, q => q.QuestionBankId, b => b.Id, (q, b) => (Guid?)b.SubjectId)
            .SingleOrDefaultAsync(cancellationToken);
        if (subjectId is null || !await db.RubricSets.AnyAsync(s => s.Id == rubricSetId &&
                s.SubjectId == subjectId && s.Status != "Draft", cancellationToken)) return null;
        var points = await db.ExpectedKeyPoints.Where(p => p.QuestionId == questionId)
            .OrderBy(p => p.DisplayOrder).Select(p => p.Content).ToArrayAsync(cancellationToken);
        var rubrics = await db.SubjectRubrics.AsNoTracking().Where(r => r.RubricSetId == rubricSetId)
            .OrderBy(r => r.DisplayOrder).ToArrayAsync(cancellationToken);
        var ids = rubrics.Select(r => r.Id).ToArray();
        var criteria = await db.SubjectRubricCriteria.AsNoTracking().Where(c => ids.Contains(c.SubjectRubricId))
            .OrderBy(c => c.DisplayOrder).ToArrayAsync(cancellationToken);
        return new AssessmentContext(points, rubrics.Select(r => new AssessmentRubric(r.Id, r.Name,
            r.WeightPercent, criteria.Where(c => c.SubjectRubricId == r.Id)
                .Select(c => new AssessmentCriterion(c.Id, c.Name, c.Description, c.MaxScore)).ToArray())).ToArray());
    }

    public async Task<IReadOnlyDictionary<Guid, string?>> GetBloomLevelsAsync(
        IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken = default)
    {
        var ids = questionIds.Distinct().ToArray();
        return await db.Questions.AsNoTracking().Where(question => ids.Contains(question.Id))
            .ToDictionaryAsync(question => question.Id, question => question.BloomLevel, cancellationToken);
    }

    public Task<string?> GetSubjectNameAsync(Guid bankId, CancellationToken cancellationToken = default) =>
        db.QuestionBanks.AsNoTracking().Where(bank => bank.Id == bankId)
            .Join(db.Subjects, bank => bank.SubjectId, subject => subject.Id, (_, subject) => subject.Name)
            .SingleOrDefaultAsync(cancellationToken);

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
        var subjectId = await db.QuestionBanks.AsNoTracking().Where(bank => bank.Id == bankId)
            .Select(bank => (Guid?)bank.SubjectId).SingleOrDefaultAsync(cancellationToken);
        var set = subjectId is null ? null : await db.RubricSets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.SubjectId == subjectId && item.Status == "Approved", cancellationToken);
        var rubrics = set is null ? [] : await db.SubjectRubrics.AsNoTracking()
            .Where(item => item.RubricSetId == set.Id).ToArrayAsync(cancellationToken);
        var rubricIds = rubrics.Select(item => item.Id).ToArray();
        var criterionRubricIds = await db.SubjectRubricCriteria.AsNoTracking()
            .Where(item => rubricIds.Contains(item.SubjectRubricId))
            .Select(item => item.SubjectRubricId).Distinct().ToArrayAsync(cancellationToken);
        var everyRubricHasCriteria = rubrics.Length > 0 &&
            rubrics.All(rubric => criterionRubricIds.Contains(rubric.Id));

        return new QuestionPreparationReadiness(validIds, withKeyPoints, set?.Id,
            rubrics.Select(rubric => rubric.WeightPercent).ToArray(), everyRubricHasCriteria);
    }
}
