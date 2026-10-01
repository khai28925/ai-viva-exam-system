namespace AiViva.Application.Exams;

public interface IExamService
{
    Task<IReadOnlyCollection<ExamDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
