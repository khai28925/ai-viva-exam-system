namespace AiViva.ModuleContracts.Viva;

public interface IExamUsageReader
{
    Task<bool> BankHasAnySessionAsync(Guid bankId, CancellationToken cancellationToken = default);

    Task<bool> BankHasPublishedSessionAsync(Guid bankId, CancellationToken cancellationToken = default);

    Task<bool> QuestionIsSelectedAsync(Guid questionId, CancellationToken cancellationToken = default);

    Task<bool> QuestionIsUsedInPublishedSessionAsync(Guid questionId, CancellationToken cancellationToken = default);
}
