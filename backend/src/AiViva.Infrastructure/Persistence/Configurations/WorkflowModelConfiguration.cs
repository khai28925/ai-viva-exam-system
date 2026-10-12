using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Persistence.Configurations;

internal static class WorkflowModelConfiguration
{
    public static void ConfigureWorkflowModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExamSchedule>(b =>
        {
            b.ToTable("exam_schedules", t =>
            {
                t.HasCheckConstraint("ck_exam_schedules_time", "end_time > start_time");
                t.HasCheckConstraint("ck_exam_schedules_status", "status IN ('Draft', 'Scheduled', 'Open', 'Closed', 'Cancelled')");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.Title).HasColumnName("title").HasMaxLength(120).IsRequired();
            b.Property(x => x.StartTime).HasColumnName("start_time");
            b.Property(x => x.EndTime).HasColumnName("end_time");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).HasDefaultValue("Draft");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.CreatedBy);
            b.HasIndex(x => new { x.StartTime, x.EndTime });
        });

        modelBuilder.Entity<ExamScheduleStudent>(b =>
        {
            b.ToTable("exam_schedule_students");
            b.HasKey(x => new { x.ExamScheduleId, x.StudentId });
            b.Property(x => x.ExamScheduleId).HasColumnName("exam_schedule_id");
            b.Property(x => x.StudentId).HasColumnName("student_id");
            b.Property(x => x.AssignedAt).HasColumnName("assigned_at").HasDefaultValueSql("now()");
            b.HasOne<ExamSchedule>().WithMany().HasForeignKey(x => x.ExamScheduleId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.StudentId);
        });

        modelBuilder.Entity<Rubric>(b =>
        {
            b.ToTable("rubrics");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.QuestionBankId).HasColumnName("question_bank_id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.HasOne<QuestionBank>().WithMany().HasForeignKey(x => x.QuestionBankId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.QuestionBankId).IsUnique();
        });

        modelBuilder.Entity<RubricCriterion>(b =>
        {
            b.ToTable("rubric_criteria", t =>
            {
                t.HasCheckConstraint("ck_rubric_criteria_max_score", "max_score > 0");
                t.HasCheckConstraint("ck_rubric_criteria_weight", "weight > 0 AND weight <= 1");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.RubricId).HasColumnName("rubric_id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            b.Property(x => x.MaxScore).HasColumnName("max_score").HasPrecision(10, 2);
            b.Property(x => x.Weight).HasColumnName("weight").HasPrecision(5, 4);
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.HasOne<Rubric>().WithMany().HasForeignKey(x => x.RubricId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RubricId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<ExpectedKeyPoint>(b =>
        {
            b.ToTable("expected_key_points", t =>
                t.HasCheckConstraint("ck_expected_key_points_order", "display_order >= 0"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.QuestionId).HasColumnName("question_id");
            b.Property(x => x.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
            b.Property(x => x.DisplayOrder).HasColumnName("display_order");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.QuestionId, x.DisplayOrder }).IsUnique();
        });

        modelBuilder.Entity<ExamSessionQuestion>(b =>
        {
            b.ToTable("exam_session_questions", t =>
                t.HasCheckConstraint("ck_exam_session_questions_order", "display_order >= 0"));
            b.HasKey(x => x.Id);
            b.HasAlternateKey(x => new { x.Id, x.ExamSessionId });
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.ExamSessionId).HasColumnName("exam_session_id");
            b.Property(x => x.QuestionBankId).HasColumnName("question_bank_id");
            b.Property(x => x.QuestionId).HasColumnName("question_id");
            b.Property(x => x.ContentSnapshot).HasColumnName("content_snapshot").HasMaxLength(2000).IsRequired();
            b.Property(x => x.DisplayOrder).HasColumnName("display_order");
            b.Property(x => x.IsRequired).HasColumnName("is_required").HasDefaultValue(true);
            b.HasOne<ExamSession>().WithMany()
                .HasForeignKey(x => new { x.ExamSessionId, x.QuestionBankId })
                .HasPrincipalKey(x => new { x.Id, x.QuestionBankId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Question>().WithMany()
                .HasForeignKey(x => new { x.QuestionId, x.QuestionBankId })
                .HasPrincipalKey(x => new { x.Id, x.QuestionBankId }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.ExamSessionId, x.QuestionId }).IsUnique();
            b.HasIndex(x => new { x.ExamSessionId, x.DisplayOrder }).IsUnique();
            b.HasIndex(x => x.QuestionId);
        });

        modelBuilder.Entity<AttemptQuestion>(b =>
        {
            b.ToTable("attempt_questions", t =>
            {
                t.HasCheckConstraint("ck_attempt_questions_order", "display_order >= 0");
                t.HasCheckConstraint("ck_attempt_questions_source",
                    "(source = 'Bank' AND exam_session_question_id IS NOT NULL) OR source = 'AiFollowUp'");
            });
            b.HasKey(x => x.Id);
            b.HasAlternateKey(x => new { x.ExamAttemptId, x.Id });
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.ExamAttemptId).HasColumnName("exam_attempt_id");
            b.Property(x => x.ExamSessionId).HasColumnName("exam_session_id");
            b.Property(x => x.ExamSessionQuestionId).HasColumnName("exam_session_question_id");
            b.Property(x => x.ParentAttemptQuestionId).HasColumnName("parent_attempt_question_id");
            b.Property(x => x.ContentSnapshot).HasColumnName("content_snapshot").HasMaxLength(2000).IsRequired();
            b.Property(x => x.Source).HasColumnName("source").HasMaxLength(16);
            b.Property(x => x.DisplayOrder).HasColumnName("display_order");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.HasOne<ExamAttempt>().WithMany()
                .HasForeignKey(x => new { x.ExamAttemptId, x.ExamSessionId })
                .HasPrincipalKey(x => new { x.Id, x.ExamSessionId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ExamSessionQuestion>().WithMany()
                .HasForeignKey(x => new { x.ExamSessionQuestionId, x.ExamSessionId })
                .HasPrincipalKey(x => new { x.Id, x.ExamSessionId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AttemptQuestion>().WithMany()
                .HasForeignKey(x => new { x.ExamAttemptId, x.ParentAttemptQuestionId })
                .HasPrincipalKey(x => new { x.ExamAttemptId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.ExamAttemptId, x.DisplayOrder }).IsUnique();
            b.HasIndex(x => new { x.ExamSessionQuestionId, x.ExamSessionId });
        });

        modelBuilder.Entity<Answer>(b =>
        {
            b.ToTable("answers", t =>
                t.HasCheckConstraint("ck_answers_payload", "audio_object_key IS NOT NULL OR text_content IS NOT NULL"));
            b.HasKey(x => x.Id);
            b.HasAlternateKey(x => new { x.ExamAttemptId, x.Id });
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.ExamAttemptId).HasColumnName("exam_attempt_id");
            b.Property(x => x.AttemptQuestionId).HasColumnName("attempt_question_id");
            b.Property(x => x.AudioObjectKey).HasColumnName("audio_object_key").HasMaxLength(1024);
            b.Property(x => x.TextContent).HasColumnName("text_content").HasColumnType("text");
            b.Property(x => x.AnsweredAt).HasColumnName("answered_at");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.HasOne<AttemptQuestion>().WithMany()
                .HasForeignKey(x => new { x.ExamAttemptId, x.AttemptQuestionId })
                .HasPrincipalKey(x => new { x.ExamAttemptId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.AttemptQuestionId).IsUnique();
        });

        modelBuilder.Entity<Transcript>(b =>
        {
            b.ToTable("transcripts", t =>
                t.HasCheckConstraint("ck_transcripts_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.AnswerId).HasColumnName("answer_id");
            b.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
            b.Property(x => x.Confidence).HasColumnName("confidence").HasPrecision(5, 4);
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.HasOne<Answer>().WithMany().HasForeignKey(x => x.AnswerId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.AnswerId).IsUnique();
        });

        modelBuilder.Entity<AiEvaluation>(b =>
        {
            b.ToTable("ai_evaluations", t =>
                t.HasCheckConstraint("ck_ai_evaluations_score", "score >= 0"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.AnswerId).HasColumnName("answer_id");
            b.Property(x => x.Score).HasColumnName("score").HasPrecision(10, 2);
            b.Property(x => x.Analysis).HasColumnName("analysis").HasColumnType("text");
            b.Property(x => x.Decision).HasColumnName("decision").HasMaxLength(32);
            b.Property(x => x.MatchedKeyPoints).HasColumnName("matched_key_points").HasColumnType("text");
            b.Property(x => x.MissingKeyPoints).HasColumnName("missing_key_points").HasColumnType("text");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.HasOne<Answer>().WithMany().HasForeignKey(x => x.AnswerId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.AnswerId).IsUnique();
        });

        modelBuilder.Entity<LecturerEvaluation>(b =>
        {
            b.ToTable("lecturer_evaluations", t =>
                t.HasCheckConstraint("ck_lecturer_evaluations_score", "score >= 0"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.AnswerId).HasColumnName("answer_id");
            b.Property(x => x.LecturerId).HasColumnName("lecturer_id");
            b.Property(x => x.Score).HasColumnName("score").HasPrecision(10, 2);
            b.Property(x => x.Comments).HasColumnName("comments").HasColumnType("text");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.HasOne<Answer>().WithMany().HasForeignKey(x => x.AnswerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.AnswerId).IsUnique();
            b.HasIndex(x => x.LecturerId);
        });

        modelBuilder.Entity<Report>(b =>
        {
            b.ToTable("reports", t =>
                t.HasCheckConstraint("ck_reports_scores",
                    "ai_total_score >= 0 AND lecturer_total_score >= 0 AND final_score >= 0"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.ExamAttemptId).HasColumnName("exam_attempt_id");
            b.Property(x => x.AiTotalScore).HasColumnName("ai_total_score").HasPrecision(10, 2);
            b.Property(x => x.LecturerTotalScore).HasColumnName("lecturer_total_score").HasPrecision(10, 2);
            b.Property(x => x.FinalScore).HasColumnName("final_score").HasPrecision(10, 2);
            b.Property(x => x.Feedback).HasColumnName("feedback").HasColumnType("text");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.HasOne<ExamAttempt>().WithMany().HasForeignKey(x => x.ExamAttemptId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.ExamAttemptId).IsUnique();
        });

        modelBuilder.Entity<ExamActivityLog>(b =>
        {
            b.ToTable("exam_activity_logs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.ExamAttemptId).HasColumnName("exam_attempt_id");
            b.Property(x => x.UserId).HasColumnName("user_id");
            b.Property(x => x.ActivityType).HasColumnName("activity_type").HasMaxLength(64).IsRequired();
            b.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            b.HasOne<ExamAttempt>().WithMany().HasForeignKey(x => x.ExamAttemptId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.ExamAttemptId, x.CreatedAt });
            b.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<ServiceInteractionLog>(b =>
        {
            b.ToTable("service_interaction_logs", t =>
            {
                t.HasCheckConstraint("ck_service_interaction_logs_latency", "latency_ms IS NULL OR latency_ms >= 0");
                t.HasCheckConstraint("ck_service_interaction_logs_time",
                    "response_time IS NULL OR response_time >= request_time");
                t.HasCheckConstraint("ck_service_interaction_logs_type", "service_type IN ('STT', 'TTS', 'LLM')");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            b.Property(x => x.ExamAttemptId).HasColumnName("exam_attempt_id");
            b.Property(x => x.AnswerId).HasColumnName("answer_id");
            b.Property(x => x.ServiceType).HasColumnName("service_type").HasMaxLength(8).IsRequired();
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            b.Property(x => x.RequestTime).HasColumnName("request_time");
            b.Property(x => x.ResponseTime).HasColumnName("response_time");
            b.Property(x => x.LatencyMs).HasColumnName("latency_ms");
            b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
            b.HasOne<ExamAttempt>().WithMany().HasForeignKey(x => x.ExamAttemptId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Answer>().WithMany()
                .HasForeignKey(x => new { x.ExamAttemptId, x.AnswerId })
                .HasPrincipalKey(x => new { x.ExamAttemptId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.ExamAttemptId, x.RequestTime });
            b.HasIndex(x => new { x.ExamAttemptId, x.AnswerId });
        });
    }
}
