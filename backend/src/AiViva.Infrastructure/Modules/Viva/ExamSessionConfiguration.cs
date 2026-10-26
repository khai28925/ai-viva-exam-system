using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiViva.Infrastructure.Persistence.Configurations;

public sealed class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
{
    public void Configure(EntityTypeBuilder<ExamSession> builder)
    {
        builder.ToTable("exam_sessions", table => table.HasCheckConstraint(
            "ck_exam_sessions_status", "status IN ('Draft', 'Open', 'Closed')"));
        builder.HasKey(session => session.Id);
        builder.HasAlternateKey(session => new { session.Id, session.QuestionBankId });
        builder.Property(session => session.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(session => session.Title).HasColumnName("title").HasMaxLength(120).IsRequired();
        builder.Property(session => session.QuestionBankId).HasColumnName("question_bank_id").IsRequired();
        builder.Property(session => session.ScheduleId).HasColumnName("schedule_id");
        builder.Property(session => session.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(session => session.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(session => session.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(session => session.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<QuestionBank>().WithMany().HasForeignKey(session => session.QuestionBankId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(session => session.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExamSchedule>().WithMany().HasForeignKey(session => session.ScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(session => session.ScheduleId).HasDatabaseName("ix_exam_sessions_schedule_id");
        builder.HasIndex(session => session.QuestionBankId).HasDatabaseName("ix_exam_sessions_question_bank_id");
        builder.HasIndex(session => session.CreatedBy).HasDatabaseName("ix_exam_sessions_created_by");
        builder.HasIndex(session => session.Status).HasDatabaseName("ix_exam_sessions_status");
    }
}
