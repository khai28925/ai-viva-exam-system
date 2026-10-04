using System.Collections.Concurrent;
using AiViva.Application.Abstractions;
using AiViva.Domain.Entities;

namespace AiViva.Infrastructure.Persistence;

public sealed class InMemoryQuestionRepository : IQuestionRepository
{
    private readonly ConcurrentDictionary<Guid, Question> _store = new();

    public InMemoryQuestionRepository()
    {
        var q1 = new Question 
        { 
            Id = Guid.Parse("b1b2c3d4-0001-0000-0000-000000000001"), 
            QuestionBankId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001"), 
            Content = "What is the purpose of version control?", 
            Type = QuestionType.Essay, 
            DifficultyLevel = DifficultyLevel.Easy, 
            Explanation = "Version control tracks changes to source code.", 
            Points = 5, 
            CreatedAt = DateTimeOffset.Parse("2026-09-02T08:00:00+07:00") 
        };
        var q2 = new Question 
        { 
            Id = Guid.Parse("b1b2c3d4-0002-0000-0000-000000000001"), 
            QuestionBankId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001"), 
            Content = "Git is a distributed version control system.", 
            Type = QuestionType.TrueFalse, 
            DifficultyLevel = DifficultyLevel.Easy, 
            Points = 1, 
            CreatedAt = DateTimeOffset.Parse("2026-09-02T09:00:00+07:00") 
        };
        var q3 = new Question 
        { 
            Id = Guid.Parse("b1b2c3d4-0003-0000-0000-000000000001"), 
            QuestionBankId = Guid.Parse("a1b2c3d4-0002-0000-0000-000000000001"), 
            Content = "What is the time complexity of binary search?", 
            Type = QuestionType.ShortAnswer, 
            DifficultyLevel = DifficultyLevel.Medium, 
            Explanation = "Binary search divides the search interval in half.", 
            Points = 3, 
            CreatedAt = DateTimeOffset.Parse("2026-09-16T08:00:00+07:00") 
        };
        
        _store.TryAdd(q1.Id, q1);
        _store.TryAdd(q2.Id, q2);
        _store.TryAdd(q3.Id, q3);
    }

    public Task<Question?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var entity);
        if (entity != null && !entity.IsDeleted)
        {
            return Task.FromResult<Question?>(entity);
        }
        return Task.FromResult<Question?>(null);
    }

    public Task<IReadOnlyCollection<Question>> GetAllAsync(CancellationToken ct = default)
    {
        var result = _store.Values.Where(x => !x.IsDeleted).ToList();
        return Task.FromResult<IReadOnlyCollection<Question>>((IReadOnlyCollection<Question>)result);
    }

    public Task<IReadOnlyCollection<Question>> GetByQuestionBankIdAsync(Guid questionBankId, CancellationToken ct = default)
    {
        var result = _store.Values.Where(x => x.QuestionBankId == questionBankId && !x.IsDeleted).ToList();
        return Task.FromResult<IReadOnlyCollection<Question>>((IReadOnlyCollection<Question>)result);
    }

    public Task<Question> AddAsync(Question entity, CancellationToken ct = default)
    {
        _store.TryAdd(entity.Id, entity);
        return Task.FromResult(entity);
    }

    public Task<Question> UpdateAsync(Question entity, CancellationToken ct = default)
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
