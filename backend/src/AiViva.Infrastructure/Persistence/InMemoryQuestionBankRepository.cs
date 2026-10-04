using AiViva.Application.Abstractions;
using AiViva.Domain.Entities;

namespace AiViva.Infrastructure.Persistence;

// Temporary adapter for developing the Question Bank API without PostgreSQL.
public sealed class InMemoryQuestionBankRepository : IQuestionBankRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, QuestionBank> _questionBanks = [];
    private readonly Dictionary<Guid, Question> _questions = [];

    public Task<IReadOnlyCollection<QuestionBank>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyCollection<QuestionBank>>(
                _questionBanks.Values
                    .OrderBy(bank => bank.CreatedAt)
                    .ThenBy(bank => bank.Id)
                    .ToArray());
        }
    }

    public Task<QuestionBank?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_questionBanks.GetValueOrDefault(id));
        }
    }

    public Task AddAsync(QuestionBank questionBank, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _questionBanks.Add(questionBank.Id, questionBank);
        }

        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(
        QuestionBank questionBank,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_questionBanks.ContainsKey(questionBank.Id))
            {
                return Task.FromResult(false);
            }

            _questionBanks[questionBank.Id] = questionBank;
            return Task.FromResult(true);
        }
    }

    public Task<QuestionBankDeleteResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_questionBanks.ContainsKey(id))
            {
                return Task.FromResult(QuestionBankDeleteResult.NotFound);
            }

            if (_questions.Values.Any(question => question.QuestionBankId == id))
            {
                return Task.FromResult(QuestionBankDeleteResult.HasQuestions);
            }

            _questionBanks.Remove(id);
            return Task.FromResult(QuestionBankDeleteResult.Deleted);
        }
    }

    public Task<IReadOnlyCollection<Question>> GetQuestionsAsync(
        Guid questionBankId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyCollection<Question>>(
                _questions.Values
                    .Where(question => question.QuestionBankId == questionBankId)
                    .OrderBy(question => question.CreatedAt)
                    .ThenBy(question => question.Id)
                    .ToArray());
        }
    }

    public Task<Question?> GetQuestionByIdAsync(
        Guid questionBankId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var question = _questions.GetValueOrDefault(questionId);
            return Task.FromResult(question?.QuestionBankId == questionBankId ? question : null);
        }
    }

    public Task<bool> AddQuestionAsync(
        Question question,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_questionBanks.ContainsKey(question.QuestionBankId))
            {
                return Task.FromResult(false);
            }

            _questions.Add(question.Id, question);
            return Task.FromResult(true);
        }
    }

    public Task<bool> UpdateQuestionAsync(
        Question question,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_questions.TryGetValue(question.Id, out var existing) ||
                existing.QuestionBankId != question.QuestionBankId)
            {
                return Task.FromResult(false);
            }

            _questions[question.Id] = question;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteQuestionAsync(
        Guid questionBankId,
        Guid questionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_questions.TryGetValue(questionId, out var existing) ||
                existing.QuestionBankId != questionBankId)
            {
                return Task.FromResult(false);
            }

            _questions.Remove(questionId);
            return Task.FromResult(true);
        }
    }
}
