namespace AiViva.Application.ExamSessions;

public interface IExamSessionService
{
    Task<IReadOnlyCollection<ExamSessionDto>> GetAllAsync(ExamActor actor, CancellationToken ct = default);
    Task<ExamSessionResult<ExamSessionDto>> GetByIdAsync(Guid id, ExamActor actor, CancellationToken ct = default);
    Task<ExamSessionResult<ExamSessionDto>> CreateAsync(SaveExamSessionRequest request, ExamActor actor, CancellationToken ct = default);
    Task<ExamSessionResult<ExamSessionDto>> UpdateAsync(Guid id, SaveExamSessionRequest request, ExamActor actor, CancellationToken ct = default);
    Task<ExamSessionResult<bool>> DeleteAsync(Guid id, ExamActor actor, CancellationToken ct = default);
    Task<ExamSessionResult<ExamAttemptDto>> JoinAsync(Guid id, ExamActor actor, CancellationToken ct = default);
}
