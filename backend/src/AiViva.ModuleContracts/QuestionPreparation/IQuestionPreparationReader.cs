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
}

public sealed record QuestionPreparationReadiness(
    IReadOnlyCollection<Guid> ValidQuestionIds,
    IReadOnlyCollection<Guid> QuestionIdsWithKeyPoints,
    bool HasRubric,
    IReadOnlyCollection<decimal> CriterionWeights);
