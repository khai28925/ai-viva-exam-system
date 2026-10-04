using AiViva.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Persistence;

public sealed class QuestionBankDbContext(DbContextOptions<QuestionBankDbContext> options) : DbContext(options)
{
    public DbSet<QuestionBank> QuestionBanks => Set<QuestionBank>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuestionBankDbContext).Assembly);
    }
}
