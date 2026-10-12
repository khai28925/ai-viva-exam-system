using AiViva.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiViva.Infrastructure.Persistence.Configurations;

public sealed class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> builder)
    {
        builder.ToTable("exam_attempts", table =>
        {
            table.HasCheckConstraint("ck_exam_attempts_status", "status IN ('InProgress', 'Completed', 'Cancelled')");
            table.HasCheckConstraint("ck_exam_attempts_completion", "completed_at IS NULL OR completed_at >= started_at");
        });
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(attempt => attempt.ExamSessionId).HasColumnName("exam_session_id").IsRequired();
        builder.Property(attempt => attempt.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(attempt => attempt.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(attempt => attempt.Status).HasColumnName("status").HasMaxLength(16)
            .HasDefaultValue("InProgress").IsRequired();
        builder.Property(attempt => attempt.CompletedAt).HasColumnName("completed_at");
        builder.Property(attempt => attempt.CreatedAt).HasColumnName("created_at")
            .HasDefaultValueSql("now()").IsRequired();
        builder.HasOne<ExamSession>().WithMany().HasForeignKey(attempt => attempt.ExamSessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(attempt => attempt.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(attempt => new { attempt.ExamSessionId, attempt.StudentId })
            .IsUnique().HasDatabaseName("ux_exam_attempts_session_student");
        builder.HasIndex(attempt => attempt.StudentId).HasDatabaseName("ix_exam_attempts_student_id");
    }
}
