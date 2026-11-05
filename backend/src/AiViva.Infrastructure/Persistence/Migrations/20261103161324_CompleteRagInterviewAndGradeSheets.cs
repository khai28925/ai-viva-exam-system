using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiViva.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteRagInterviewAndGradeSheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "student_code",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                table: "study_documents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "index_error",
                table: "study_documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "index_status",
                table: "study_documents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "indexed_at",
                table: "study_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "page_count",
                table: "study_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "answer_mode",
                table: "exam_sessions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "VoiceOrText");

            migrationBuilder.AddColumn<string>(
                name: "source_refs_json",
                table: "ai_question_candidates",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "retrieved_chunk_count",
                table: "ai_generation_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "topic",
                table: "ai_generation_runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "study_document_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    page_start = table.Column<int>(type: "integer", nullable: false),
                    page_end = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    embedding = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_document_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_document_chunks_study_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "study_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_study_document_chunks_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_study_document_chunks_document_id_chunk_index",
                table: "study_document_chunks",
                columns: new[] { "document_id", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_document_chunks_subject_id",
                table: "study_document_chunks",
                column: "subject_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "study_document_chunks");

            migrationBuilder.DropColumn(
                name: "student_code",
                table: "users");

            migrationBuilder.DropColumn(
                name: "embedding_model",
                table: "study_documents");

            migrationBuilder.DropColumn(
                name: "index_error",
                table: "study_documents");

            migrationBuilder.DropColumn(
                name: "index_status",
                table: "study_documents");

            migrationBuilder.DropColumn(
                name: "indexed_at",
                table: "study_documents");

            migrationBuilder.DropColumn(
                name: "page_count",
                table: "study_documents");

            migrationBuilder.DropColumn(
                name: "answer_mode",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "source_refs_json",
                table: "ai_question_candidates");

            migrationBuilder.DropColumn(
                name: "retrieved_chunk_count",
                table: "ai_generation_runs");

            migrationBuilder.DropColumn(
                name: "topic",
                table: "ai_generation_runs");
        }
    }
}
