using AiViva.Application.Exams;
using AiViva.Application.QuestionBanks;
using AiViva.Application.Questions;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IQuestionBankService, QuestionBankService>();
        services.AddScoped<IQuestionService, QuestionService>();
        return services;
    }
}
