namespace AiViva.ModuleContracts.QuestionPreparation;

public interface IQuestionPreparationReader
{
    Task<bool> BankExistsAsync(Guid bankId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetBankNamesAsync(
        IReadOnlyCollection<Guid> bankIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetSelectableQuestionsAsync(
        Guid bankId, IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken = default);

    Task<QuestionPreparationReadiness> GetReadinessAsync(
        Guid bankId, IReadOnlyCollection<Guid> selectedQuestionIds, CancellationToken cancellationToken = default);

    Task<AssessmentContext?> GetAssessmentContextAsync(
        Guid questionId, Guid rubricSetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string?>> GetBloomLevelsAsync(
        IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken = default);

    Task<string?> GetSubjectNameAsync(Guid bankId, CancellationToken cancellationToken = default);
}

public sealed record QuestionPreparationReadiness(
    IReadOnlyCollection<Guid> ValidQuestionIds,
    IReadOnlyCollection<Guid> QuestionIdsWithKeyPoints,
    Guid? ApprovedRubricSetId,
    IReadOnlyCollection<decimal> RubricWeights,
    bool EveryRubricHasCriteria);

public sealed record AssessmentContext(IReadOnlyList<string> ExpectedKeyPoints, IReadOnlyList<AssessmentRubric> Rubrics);
public sealed record AssessmentRubric(Guid Id, string Name, decimal WeightPercent, IReadOnlyList<AssessmentCriterion> Criteria);
public sealed record AssessmentCriterion(Guid Id, string Name, string? Description, decimal MaxScore);
