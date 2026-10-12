using AiViva.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiViva.Infrastructure.Persistence.Configurations;

public sealed class QuestionBankConfiguration : IEntityTypeConfiguration<QuestionBank>
{
    public void Configure(EntityTypeBuilder<QuestionBank> builder)
    {
        builder.ToTable("question_banks");
        builder.HasKey(bank => bank.Id);

        builder.Property(bank => bank.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(bank => bank.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(bank => bank.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(bank => bank.CreatedBy).HasColumnName("created_by");
        builder.Property(bank => bank.UpdatedAt).HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(bank => bank.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(bank => bank.CreatedBy).HasDatabaseName("ix_question_banks_created_by");

        builder.Property(bank => bank.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
