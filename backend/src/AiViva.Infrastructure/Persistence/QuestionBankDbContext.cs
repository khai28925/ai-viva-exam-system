using AiViva.Domain.Entities;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuestionBankDbContext).Assembly);
    }
}
