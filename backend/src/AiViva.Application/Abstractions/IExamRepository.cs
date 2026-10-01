using AiViva.Domain.Entities;

namespace AiViva.Application.Abstractions;

public interface IExamRepository
{
    Task<IReadOnlyCollection<Exam>> GetAllAsync(CancellationToken cancellationToken = default);
}
