using AiViva.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiViva.Infrastructure.Persistence.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");
        builder.HasKey(question => question.Id);

        builder.Property(question => question.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(question => question.QuestionBankId)
            .HasColumnName("question_bank_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(question => question.Content)
            .HasColumnName("content")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(question => question.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<QuestionBank>()
            .WithMany()
            .HasForeignKey(question => question.QuestionBankId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(question => question.QuestionBankId)
            .HasDatabaseName("ix_questions_question_bank_id");
    }
}
