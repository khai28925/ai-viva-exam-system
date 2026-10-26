using AiViva.Application.Abstractions;

namespace AiViva.Application.Exams;

public sealed class ExamService(IExamRepository examRepository) : IExamService
{
    public async Task<IReadOnlyCollection<ExamDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var exams = await examRepository.GetAllAsync(cancellationToken);

        return exams
            .Select(exam => new ExamDto(
                exam.Id,
                exam.Title,
                exam.Status.ToString(),
                exam.CreatedAt))
            .ToArray();
    }
}
