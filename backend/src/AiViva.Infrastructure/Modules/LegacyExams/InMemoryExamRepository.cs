using AiViva.Application.Abstractions;
using AiViva.Domain.Entities;

namespace AiViva.Infrastructure.Persistence;

public sealed class InMemoryExamRepository : IExamRepository
{
    private static readonly IReadOnlyCollection<Exam> Exams =
    [
        new Exam
        {
            Id = Guid.Parse("04b9cac0-d327-43c3-8d0d-e6703884be11"),
            Title = "Entrepreneurship Fundamentals Viva",
            CreatedAt = DateTimeOffset.Parse("2026-09-20T08:00:00+07:00")
        }
    ];

    public Task<IReadOnlyCollection<Exam>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Exams);
    }
}
