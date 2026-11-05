using AiViva.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiViva.Infrastructure.Modules.QuestionPreparation.Wf1;

internal static class Wf1ModelConfiguration
{
    public static void ConfigureWf1Model(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>(b =>
        {
            b.ToTable("subjects");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.CreatedBy);
        });

        modelBuilder.Entity<StudyDocument>(b =>
        {
            b.ToTable("study_documents");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.SubjectId).HasColumnName("subject_id");
            b.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
            b.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(100).IsRequired();
            b.Property(x => x.SizeBytes).HasColumnName("size_bytes");
            b.Property(x => x.Content).HasColumnName("content").IsRequired();
            b.Property(x => x.UploadedBy).HasColumnName("uploaded_by");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.IndexStatus).HasColumnName("index_status").HasMaxLength(16).HasDefaultValue("Pending");
            b.Property(x => x.PageCount).HasColumnName("page_count");
            b.Property(x => x.EmbeddingModel).HasColumnName("embedding_model").HasMaxLength(80);
            b.Property(x => x.IndexError).HasColumnName("index_error").HasMaxLength(500);
            b.Property(x => x.IndexedAt).HasColumnName("indexed_at");
            b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.SubjectId);
        });

        modelBuilder.Entity<StudyDocumentChunk>(b =>
        {
            b.ToTable("study_document_chunks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.DocumentId).HasColumnName("document_id");
            b.Property(x => x.SubjectId).HasColumnName("subject_id");
            b.Property(x => x.ChunkIndex).HasColumnName("chunk_index");
            b.Property(x => x.PageStart).HasColumnName("page_start");
            b.Property(x => x.PageEnd).HasColumnName("page_end");
            b.Property(x => x.Content).HasColumnName("content").HasMaxLength(4000).IsRequired();
            b.Property(x => x.Embedding).HasColumnName("embedding");
            b.HasOne<StudyDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.DocumentId, x.ChunkIndex }).IsUnique();
            b.HasIndex(x => x.SubjectId);
        });

        modelBuilder.Entity<AiGenerationRun>(b =>
        {
            b.ToTable("ai_generation_runs", t => t.HasCheckConstraint("ck_ai_generation_runs_status", "status IN ('Running','Completed','Failed')"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.SubjectId).HasColumnName("subject_id");
            b.Property(x => x.QuestionBankId).HasColumnName("question_bank_id");
            b.Property(x => x.RequestedQuestionCount).HasColumnName("requested_question_count");
            b.Property(x => x.Topic).HasColumnName("topic").HasMaxLength(200);
            b.Property(x => x.RetrievedChunkCount).HasColumnName("retrieved_chunk_count");
            b.Property(x => x.Model).HasColumnName("model").HasMaxLength(80).IsRequired();
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.CompletedAt).HasColumnName("completed_at");
            b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<QuestionBank>().WithMany().HasForeignKey(x => x.QuestionBankId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiQuestionCandidate>(b =>
        {
            b.ToTable("ai_question_candidates", t =>
            {
                t.HasCheckConstraint("ck_ai_question_candidates_bloom", "bloom_level IN ('Remember','Understand','Apply','Analyze')");
                t.HasCheckConstraint("ck_ai_question_candidates_difficulty", "difficulty_level BETWEEN 1 AND 5");
                t.HasCheckConstraint("ck_ai_question_candidates_status", "status IN ('Pending','Approved','Rejected')");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.GenerationRunId).HasColumnName("generation_run_id");
            b.Property(x => x.SubjectId).HasColumnName("subject_id");
            b.Property(x => x.QuestionBankId).HasColumnName("question_bank_id");
            b.Property(x => x.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
            b.Property(x => x.BloomLevel).HasColumnName("bloom_level").HasMaxLength(16).IsRequired();
            b.Property(x => x.DifficultyLevel).HasColumnName("difficulty_level");
            b.Property(x => x.Rationale).HasColumnName("rationale").HasMaxLength(1000).IsRequired();
            b.Property(x => x.ExpectedKeyPointsJson).HasColumnName("expected_key_points_json").HasColumnType("jsonb");
            b.Property(x => x.SourceRefsJson).HasColumnName("source_refs_json").HasColumnType("jsonb").HasDefaultValue("[]");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            b.Property(x => x.ApprovedQuestionId).HasColumnName("approved_question_id");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            b.HasOne<AiGenerationRun>().WithMany().HasForeignKey(x => x.GenerationRunId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Question>().WithMany().HasForeignKey(x => x.ApprovedQuestionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.QuestionBankId, x.Status });
        });

        modelBuilder.Entity<RubricSet>(b =>
        {
            b.ToTable("rubric_sets", t => t.HasCheckConstraint("ck_rubric_sets_status", "status IN ('Draft','Approved','Archived')"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.SubjectId).HasColumnName("subject_id");
            b.Property(x => x.Version).HasColumnName("version");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            b.Property(x => x.Source).HasColumnName("source").HasMaxLength(16).IsRequired();
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.ApprovedAt).HasColumnName("approved_at");
            b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.SubjectId, x.Version }).IsUnique();
            b.HasIndex(x => x.SubjectId).IsUnique().HasFilter("status = 'Approved'");
        });

        modelBuilder.Entity<SubjectRubric>(b =>
        {
            b.ToTable("subject_rubrics", t => t.HasCheckConstraint("ck_subject_rubrics_weight", "CAST(weight_percent AS NUMERIC) > 0 AND CAST(weight_percent AS NUMERIC) <= 100"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.RubricSetId).HasColumnName("rubric_set_id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            b.Property(x => x.WeightPercent).HasColumnName("weight_percent").HasPrecision(5, 2);
            b.Property(x => x.DisplayOrder).HasColumnName("display_order");
            b.HasOne<RubricSet>().WithMany().HasForeignKey(x => x.RubricSetId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.RubricSetId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<SubjectRubricCriterion>(b =>
        {
            b.ToTable("subject_rubric_criteria", t => t.HasCheckConstraint("ck_subject_rubric_criteria_score", "CAST(max_score AS NUMERIC) > 0"));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.SubjectRubricId).HasColumnName("subject_rubric_id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            b.Property(x => x.MaxScore).HasColumnName("max_score").HasPrecision(10, 2);
            b.Property(x => x.DisplayOrder).HasColumnName("display_order");
            b.HasOne<SubjectRubric>().WithMany().HasForeignKey(x => x.SubjectRubricId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
