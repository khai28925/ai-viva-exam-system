using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence.Configurations;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Persistence;

public sealed class QuestionBankDbContext(DbContextOptions<QuestionBankDbContext> options) : DbContext(options)
{
    public DbSet<QuestionBank> QuestionBanks => Set<QuestionBank>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();

    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();

    public DbSet<ExamSchedule> ExamSchedules => Set<ExamSchedule>();
    public DbSet<ExamScheduleStudent> ExamScheduleStudents => Set<ExamScheduleStudent>();
    public DbSet<Rubric> Rubrics => Set<Rubric>();
    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();
    public DbSet<ExpectedKeyPoint> ExpectedKeyPoints => Set<ExpectedKeyPoint>();
    public DbSet<ExamSessionQuestion> ExamSessionQuestions => Set<ExamSessionQuestion>();
    public DbSet<AttemptQuestion> AttemptQuestions => Set<AttemptQuestion>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<Transcript> Transcripts => Set<Transcript>();
    public DbSet<AiEvaluation> AiEvaluations => Set<AiEvaluation>();
    public DbSet<LecturerEvaluation> LecturerEvaluations => Set<LecturerEvaluation>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ExamActivityLog> ExamActivityLogs => Set<ExamActivityLog>();
    public DbSet<ServiceInteractionLog> ServiceInteractionLogs => Set<ServiceInteractionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuestionBankDbContext).Assembly);
        modelBuilder.ConfigureWorkflowModel();
    }
}
