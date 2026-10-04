using System.Collections.Concurrent;
using AiViva.Application.Abstractions;
using AiViva.Domain.Entities;

namespace AiViva.Infrastructure.Persistence;

public sealed class InMemoryQuestionBankRepository : IQuestionBankRepository
{
    private readonly ConcurrentDictionary<Guid, QuestionBank> _store = new();

    public InMemoryQuestionBankRepository()
    {
        var bank1 = new QuestionBank 
        { 
            Id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001"), 
            Title = "Software Engineering Basics", 
            Description = "Fundamental SE concepts", 
            SubjectId = Guid.Parse("00000000-0000-0000-0000-000000000001"), 
            CreatedAt = DateTimeOffset.Parse("2026-09-01T08:00:00+07:00") 
        };
        var bank2 = new QuestionBank 
        { 
            Id = Guid.Parse("a1b2c3d4-0002-0000-0000-000000000001"), 
            Title = "Data Structures & Algorithms", 
            Description = "DSA concepts and problems", 
            SubjectId = Guid.Parse("00000000-0000-0000-0000-000000000002"), 
            CreatedAt = DateTimeOffset.Parse("2026-09-15T10:00:00+07:00") 
        };
        _store.TryAdd(bank1.Id, bank1);
        _store.TryAdd(bank2.Id, bank2);
    }

    public Task<QuestionBank?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var entity);
        if (entity != null && !entity.IsDeleted)
        {
            return Task.FromResult<QuestionBank?>(entity);
        }
        return Task.FromResult<QuestionBank?>(null);
    }

    public Task<IReadOnlyCollection<QuestionBank>> GetAllAsync(CancellationToken ct = default)
    {
        var result = _store.Values.Where(x => !x.IsDeleted).ToList();
        return Task.FromResult<IReadOnlyCollection<QuestionBank>>((IReadOnlyCollection<QuestionBank>)result);
    }

    public Task<QuestionBank> AddAsync(QuestionBank entity, CancellationToken ct = default)
    {
        _store.TryAdd(entity.Id, entity);
        return Task.FromResult(entity);
    }

    public Task<QuestionBank> UpdateAsync(QuestionBank entity, CancellationToken ct = default)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_store.TryGetValue(id, out var entity) && !entity.IsDeleted)
        {
            entity.MarkDeleted();
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
