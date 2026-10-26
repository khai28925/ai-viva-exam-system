using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using AiViva.ModuleContracts.Viva;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Modules.Viva;

public sealed class PostgresExamUsageReader(QuestionBankDbContext db) : IExamUsageReader
{
    public Task<bool> BankHasAnySessionAsync(Guid bankId, CancellationToken cancellationToken = default) =>
        db.ExamSessions.AnyAsync(session => session.QuestionBankId == bankId, cancellationToken);

    public Task<bool> BankHasPublishedSessionAsync(Guid bankId, CancellationToken cancellationToken = default) =>
        db.ExamSessions.AnyAsync(session =>
            session.QuestionBankId == bankId &&
            (session.Status == ExamSession.Statuses.Open || session.Status == ExamSession.Statuses.Closed),
            cancellationToken);

    public Task<bool> QuestionIsSelectedAsync(Guid questionId, CancellationToken cancellationToken = default) =>
        db.ExamSessionQuestions.AnyAsync(selection => selection.QuestionId == questionId, cancellationToken);

    public Task<bool> QuestionIsUsedInPublishedSessionAsync(Guid questionId, CancellationToken cancellationToken = default) =>
        db.ExamSessionQuestions.AnyAsync(selection =>
            selection.QuestionId == questionId && db.ExamSessions.Any(session =>
                session.Id == selection.ExamSessionId &&
                (session.Status == ExamSession.Statuses.Open || session.Status == ExamSession.Statuses.Closed)),
            cancellationToken);
}
