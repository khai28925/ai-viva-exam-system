using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiViva.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWf1AiPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bloom_level",
                table: "questions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "difficulty_level",
                table: "questions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "subject_id",
                table: "question_banks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rubric_set_id",
                table: "exam_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subjects", x => x.id);
                    table.ForeignKey(
                        name: "FK_subjects_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve existing banks by creating one deterministic legacy subject per bank.
            migrationBuilder.Sql(
                """
                INSERT INTO subjects (id, name, description, created_by, created_at, updated_at)
                SELECT id, name, description, created_by, created_at, updated_at
                FROM question_banks;

                UPDATE question_banks
                SET subject_id = id
                WHERE subject_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "subject_id",
                table: "question_banks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ai_generation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_question_count = table.Column<int>(type: "integer", nullable: false),
                    model = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_generation_runs", x => x.id);
                    table.CheckConstraint("ck_ai_generation_runs_status", "status IN ('Running','Completed','Failed')");
                    table.ForeignKey(
                        name: "FK_ai_generation_runs_question_banks_question_bank_id",
                        column: x => x.question_bank_id,
                        principalTable: "question_banks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_generation_runs_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_generation_runs_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rubric_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_sets", x => x.id);
                    table.CheckConstraint("ck_rubric_sets_status", "status IN ('Draft','Approved','Archived')");
                    table.ForeignKey(
                        name: "FK_rubric_sets_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_study_documents_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_study_documents_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_question_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    bloom_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    difficulty_level = table.Column<int>(type: "integer", nullable: false),
                    rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    expected_key_points_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    approved_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_question_candidates", x => x.id);
                    table.CheckConstraint("ck_ai_question_candidates_bloom", "bloom_level IN ('Remember','Understand','Apply','Analyze')");
                    table.CheckConstraint("ck_ai_question_candidates_difficulty", "difficulty_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_ai_question_candidates_status", "status IN ('Pending','Approved','Rejected')");
                    table.ForeignKey(
                        name: "FK_ai_question_candidates_ai_generation_runs_generation_run_id",
                        column: x => x.generation_run_id,
                        principalTable: "ai_generation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_question_candidates_questions_approved_question_id",
                        column: x => x.approved_question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subject_rubrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    weight_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subject_rubrics", x => x.id);
                    table.CheckConstraint("ck_subject_rubrics_weight", "CAST(weight_percent AS NUMERIC) > 0 AND CAST(weight_percent AS NUMERIC) <= 100");
                    table.ForeignKey(
                        name: "FK_subject_rubrics_rubric_sets_rubric_set_id",
                        column: x => x.rubric_set_id,
                        principalTable: "rubric_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subject_rubric_criteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subject_rubric_criteria", x => x.id);
                    table.CheckConstraint("ck_subject_rubric_criteria_score", "CAST(max_score AS NUMERIC) > 0");
                    table.ForeignKey(
                        name: "FK_subject_rubric_criteria_subject_rubrics_subject_rubric_id",
                        column: x => x.subject_rubric_id,
                        principalTable: "subject_rubrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Convert the former single-bank rubric to a versioned subject rubric set.
            // Criteria weights are intentionally not copied because the new contract
            // applies percentages at rubric level and scores at criterion level.
            migrationBuilder.Sql(
                """
                INSERT INTO rubric_sets
                    (id, subject_id, version, status, source, created_by, created_at, approved_at)
                SELECT
                    r.id,
                    qb.subject_id,
                    1,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM rubric_criteria rc WHERE rc.rubric_id = r.id
                    ) THEN 'Approved' ELSE 'Draft' END,
                    'Legacy',
                    qb.created_by,
                    r.created_at,
                    CASE WHEN EXISTS (
                        SELECT 1 FROM rubric_criteria rc WHERE rc.rubric_id = r.id
                    ) THEN r.created_at ELSE NULL END
                FROM rubrics r
                INNER JOIN question_banks qb ON qb.id = r.question_bank_id;

                INSERT INTO subject_rubrics
                    (id, rubric_set_id, name, description, weight_percent, display_order)
                SELECT id, id, name, description, 100.00, 0
                FROM rubrics;

                INSERT INTO subject_rubric_criteria
                    (id, subject_rubric_id, name, description, max_score, display_order)
                SELECT
                    rc.id,
                    rc.rubric_id,
                    rc.name,
                    rc.description,
                    rc.max_score,
                    (ROW_NUMBER() OVER (
                        PARTITION BY rc.rubric_id ORDER BY rc.created_at, rc.id
                    ) - 1)::integer
                FROM rubric_criteria rc;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_bloom",
                table: "questions",
                sql: "bloom_level IS NULL OR bloom_level IN ('Remember','Understand','Apply','Analyze')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_difficulty",
                table: "questions",
                sql: "difficulty_level IS NULL OR difficulty_level BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "ix_question_banks_subject_id",
                table: "question_banks",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_sessions_rubric_set_id",
                table: "exam_sessions",
                column: "rubric_set_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_generation_runs_created_by",
                table: "ai_generation_runs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_ai_generation_runs_question_bank_id",
                table: "ai_generation_runs",
                column: "question_bank_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_generation_runs_subject_id",
                table: "ai_generation_runs",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_question_candidates_approved_question_id",
                table: "ai_question_candidates",
                column: "approved_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_question_candidates_generation_run_id",
                table: "ai_question_candidates",
                column: "generation_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_question_candidates_question_bank_id_status",
                table: "ai_question_candidates",
                columns: new[] { "question_bank_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_rubric_sets_subject_id",
                table: "rubric_sets",
                column: "subject_id",
                unique: true,
                filter: "status = 'Approved'");

            migrationBuilder.CreateIndex(
                name: "IX_rubric_sets_subject_id_version",
                table: "rubric_sets",
                columns: new[] { "subject_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_documents_subject_id",
                table: "study_documents",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_study_documents_uploaded_by",
                table: "study_documents",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_subject_rubric_criteria_subject_rubric_id",
                table: "subject_rubric_criteria",
                column: "subject_rubric_id");

            migrationBuilder.CreateIndex(
                name: "IX_subject_rubrics_rubric_set_id_name",
                table: "subject_rubrics",
                columns: new[] { "rubric_set_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subjects_created_by",
                table: "subjects",
                column: "created_by");

            migrationBuilder.AddForeignKey(
                name: "FK_exam_sessions_rubric_sets_rubric_set_id",
                table: "exam_sessions",
                column: "rubric_set_id",
                principalTable: "rubric_sets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_question_banks_subjects_subject_id",
                table: "question_banks",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exam_sessions_rubric_sets_rubric_set_id",
                table: "exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_question_banks_subjects_subject_id",
                table: "question_banks");

            migrationBuilder.DropTable(
                name: "ai_question_candidates");

            migrationBuilder.DropTable(
                name: "study_documents");

            migrationBuilder.DropTable(
                name: "subject_rubric_criteria");

            migrationBuilder.DropTable(
                name: "ai_generation_runs");

            migrationBuilder.DropTable(
                name: "subject_rubrics");

            migrationBuilder.DropTable(
                name: "rubric_sets");

            migrationBuilder.DropTable(
                name: "subjects");

            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_bloom",
                table: "questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_difficulty",
                table: "questions");

            migrationBuilder.DropIndex(
                name: "ix_question_banks_subject_id",
                table: "question_banks");

            migrationBuilder.DropIndex(
                name: "IX_exam_sessions_rubric_set_id",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "bloom_level",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "difficulty_level",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "subject_id",
                table: "question_banks");

            migrationBuilder.DropColumn(
                name: "rubric_set_id",
                table: "exam_sessions");
        }
    }
}
