using AiViva.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiViva.Infrastructure.Persistence.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions", table =>
        {
            table.HasCheckConstraint("ck_questions_type", "question_type IN ('Bank', 'FollowUp')");
            table.HasCheckConstraint("ck_questions_parent", "parent_question_id IS NULL OR parent_question_id <> id");
        });
        builder.HasKey(question => question.Id);
        builder.HasAlternateKey(question => new { question.Id, question.QuestionBankId });

        builder.Property(question => question.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(question => question.QuestionBankId)
            .HasColumnName("question_bank_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(question => question.ParentQuestionId).HasColumnName("parent_question_id");
        builder.Property(question => question.QuestionType).HasColumnName("question_type")
            .HasMaxLength(16).HasDefaultValue("Bank").IsRequired();
        builder.Property(question => question.UpdatedAt).HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
        builder.HasOne<Question>().WithMany()
            .HasForeignKey(question => new { question.ParentQuestionId, question.QuestionBankId })
            .HasPrincipalKey(question => new { question.Id, question.QuestionBankId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(question => question.ParentQuestionId)
            .HasDatabaseName("ix_questions_parent_question_id");

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
