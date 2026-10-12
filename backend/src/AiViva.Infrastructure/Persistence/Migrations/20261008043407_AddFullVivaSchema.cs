using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiViva.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFullVivaSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "users",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "roles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "roles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_question_id",
                table: "questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "question_type",
                table: "questions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Bank");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                table: "question_banks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "question_banks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "schedule_id",
                table: "exam_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "exam_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                table: "exam_attempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "exam_attempts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "exam_attempts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "InProgress");

            // Preserve the original timeline of rows created before this migration.
            migrationBuilder.Sql("UPDATE users SET updated_at = created_at WHERE updated_at IS NULL");
            migrationBuilder.Sql("UPDATE question_banks SET updated_at = created_at WHERE updated_at IS NULL");
            migrationBuilder.Sql("UPDATE questions SET updated_at = created_at WHERE updated_at IS NULL");
            migrationBuilder.Sql("UPDATE exam_sessions SET updated_at = created_at WHERE updated_at IS NULL");
            migrationBuilder.Sql("UPDATE exam_attempts SET created_at = started_at");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_questions_id_question_bank_id",
                table: "questions",
                columns: new[] { "id", "question_bank_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_exam_sessions_id_question_bank_id",
                table: "exam_sessions",
                columns: new[] { "id", "question_bank_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_exam_attempts_id_exam_session_id",
                table: "exam_attempts",
                columns: new[] { "id", "exam_session_id" });

            migrationBuilder.CreateTable(
                name: "exam_activity_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_activity_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_exam_activity_logs_exam_attempts_exam_attempt_id",
                        column: x => x.exam_attempt_id,
                        principalTable: "exam_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_activity_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    start_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Draft"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_schedules", x => x.id);
                    table.CheckConstraint("ck_exam_schedules_status", "status IN ('Draft', 'Scheduled', 'Open', 'Closed', 'Cancelled')");
                    table.CheckConstraint("ck_exam_schedules_time", "end_time > start_time");
                    table.ForeignKey(
                        name: "FK_exam_schedules_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_session_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_snapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_session_questions", x => x.id);
                    table.UniqueConstraint("AK_exam_session_questions_id_exam_session_id", x => new { x.id, x.exam_session_id });
                    table.CheckConstraint("ck_exam_session_questions_order", "display_order >= 0");
                    table.ForeignKey(
                        name: "FK_exam_session_questions_exam_sessions_exam_session_id_questi~",
                        columns: x => new { x.exam_session_id, x.question_bank_id },
                        principalTable: "exam_sessions",
                        principalColumns: new[] { "id", "question_bank_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_session_questions_questions_question_id_question_bank_~",
                        columns: x => new { x.question_id, x.question_bank_id },
                        principalTable: "questions",
                        principalColumns: new[] { "id", "question_bank_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expected_key_points",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expected_key_points", x => x.id);
                    table.CheckConstraint("ck_expected_key_points_order", "display_order >= 0");
                    table.ForeignKey(
                        name: "FK_expected_key_points_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ai_total_score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    lecturer_total_score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    final_score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.id);
                    table.CheckConstraint("ck_reports_scores", "ai_total_score >= 0 AND lecturer_total_score >= 0 AND final_score >= 0");
                    table.ForeignKey(
                        name: "FK_reports_exam_attempts_exam_attempt_id",
                        column: x => x.exam_attempt_id,
                        principalTable: "exam_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rubrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_rubrics_question_banks_question_bank_id",
                        column: x => x.question_bank_id,
                        principalTable: "question_banks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_schedule_students",
                columns: table => new
                {
                    exam_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_schedule_students", x => new { x.exam_schedule_id, x.student_id });
                    table.ForeignKey(
                        name: "FK_exam_schedule_students_exam_schedules_exam_schedule_id",
                        column: x => x.exam_schedule_id,
                        principalTable: "exam_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_schedule_students_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attempt_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_attempt_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content_snapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attempt_questions", x => x.id);
                    table.UniqueConstraint("AK_attempt_questions_exam_attempt_id_id", x => new { x.exam_attempt_id, x.id });
                    table.CheckConstraint("ck_attempt_questions_order", "display_order >= 0");
                    table.CheckConstraint("ck_attempt_questions_source", "(source = 'Bank' AND exam_session_question_id IS NOT NULL) OR source = 'AiFollowUp'");
                    table.ForeignKey(
                        name: "FK_attempt_questions_attempt_questions_exam_attempt_id_parent_~",
                        columns: x => new { x.exam_attempt_id, x.parent_attempt_question_id },
                        principalTable: "attempt_questions",
                        principalColumns: new[] { "exam_attempt_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attempt_questions_exam_attempts_exam_attempt_id_exam_sessio~",
                        columns: x => new { x.exam_attempt_id, x.exam_session_id },
                        principalTable: "exam_attempts",
                        principalColumns: new[] { "id", "exam_session_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attempt_questions_exam_session_questions_exam_session_quest~",
                        columns: x => new { x.exam_session_question_id, x.exam_session_id },
                        principalTable: "exam_session_questions",
                        principalColumns: new[] { "id", "exam_session_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rubric_criteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    rubric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_criteria", x => x.id);
                    table.CheckConstraint("ck_rubric_criteria_max_score", "max_score > 0");
                    table.CheckConstraint("ck_rubric_criteria_weight", "weight > 0 AND weight <= 1");
                    table.ForeignKey(
                        name: "FK_rubric_criteria_rubrics_rubric_id",
                        column: x => x.rubric_id,
                        principalTable: "rubrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audio_object_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    text_content = table.Column<string>(type: "text", nullable: true),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answers", x => x.id);
                    table.UniqueConstraint("AK_answers_exam_attempt_id_id", x => new { x.exam_attempt_id, x.id });
                    table.CheckConstraint("ck_answers_payload", "audio_object_key IS NOT NULL OR text_content IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_answers_attempt_questions_exam_attempt_id_attempt_question_~",
                        columns: x => new { x.exam_attempt_id, x.attempt_question_id },
                        principalTable: "attempt_questions",
                        principalColumns: new[] { "exam_attempt_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    analysis = table.Column<string>(type: "text", nullable: true),
                    decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    matched_key_points = table.Column<string>(type: "text", nullable: true),
                    missing_key_points = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_evaluations", x => x.id);
                    table.CheckConstraint("ck_ai_evaluations_score", "score >= 0");
                    table.ForeignKey(
                        name: "FK_ai_evaluations_answers_answer_id",
                        column: x => x.answer_id,
                        principalTable: "answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lecturer_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    comments = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecturer_evaluations", x => x.id);
                    table.CheckConstraint("ck_lecturer_evaluations_score", "score >= 0");
                    table.ForeignKey(
                        name: "FK_lecturer_evaluations_answers_answer_id",
                        column: x => x.answer_id,
                        principalTable: "answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lecturer_evaluations_users_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_interaction_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    request_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    response_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_interaction_logs", x => x.id);
                    table.CheckConstraint("ck_service_interaction_logs_latency", "latency_ms IS NULL OR latency_ms >= 0");
                    table.CheckConstraint("ck_service_interaction_logs_time", "response_time IS NULL OR response_time >= request_time");
                    table.CheckConstraint("ck_service_interaction_logs_type", "service_type IN ('STT', 'TTS', 'LLM')");
                    table.ForeignKey(
                        name: "FK_service_interaction_logs_answers_exam_attempt_id_answer_id",
                        columns: x => new { x.exam_attempt_id, x.answer_id },
                        principalTable: "answers",
                        principalColumns: new[] { "exam_attempt_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_interaction_logs_exam_attempts_exam_attempt_id",
                        column: x => x.exam_attempt_id,
                        principalTable: "exam_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transcripts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transcripts", x => x.id);
                    table.CheckConstraint("ck_transcripts_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
                    table.ForeignKey(
                        name: "FK_transcripts_answers_answer_id",
                        column: x => x.answer_id,
                        principalTable: "answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("a8697244-6c13-4de1-9c13-bc6a840179c1"),
                columns: new[] { "description", "name" },
                values: new object[] { "Manages users and all exam content.", "Administrator" });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("a8697244-6c13-4de1-9c13-bc6a840179c2"),
                columns: new[] { "description", "name" },
                values: new object[] { "Creates question banks and conducts exams.", "Lecturer" });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("a8697244-6c13-4de1-9c13-bc6a840179c3"),
                columns: new[] { "description", "name" },
                values: new object[] { "Participates in assigned exams.", "Student" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_parent_question_id",
                table: "questions",
                column: "parent_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_questions_parent_question_id_question_bank_id",
                table: "questions",
                columns: new[] { "parent_question_id", "question_bank_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_parent",
                table: "questions",
                sql: "parent_question_id IS NULL OR parent_question_id <> id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_questions_type",
                table: "questions",
                sql: "question_type IN ('Bank', 'FollowUp')");

            migrationBuilder.CreateIndex(
                name: "ix_question_banks_created_by",
                table: "question_banks",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sessions_schedule_id",
                table: "exam_sessions",
                column: "schedule_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_attempts_completion",
                table: "exam_attempts",
                sql: "completed_at IS NULL OR completed_at >= started_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_exam_attempts_status",
                table: "exam_attempts",
                sql: "status IN ('InProgress', 'Completed', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_ai_evaluations_answer_id",
                table: "ai_evaluations",
                column: "answer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_answers_attempt_question_id",
                table: "answers",
                column: "attempt_question_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_answers_exam_attempt_id_attempt_question_id",
                table: "answers",
                columns: new[] { "exam_attempt_id", "attempt_question_id" });

            migrationBuilder.CreateIndex(
                name: "IX_attempt_questions_exam_attempt_id_display_order",
                table: "attempt_questions",
                columns: new[] { "exam_attempt_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attempt_questions_exam_attempt_id_exam_session_id",
                table: "attempt_questions",
                columns: new[] { "exam_attempt_id", "exam_session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_attempt_questions_exam_attempt_id_parent_attempt_question_id",
                table: "attempt_questions",
                columns: new[] { "exam_attempt_id", "parent_attempt_question_id" });

            migrationBuilder.CreateIndex(
                name: "IX_attempt_questions_exam_session_question_id_exam_session_id",
                table: "attempt_questions",
                columns: new[] { "exam_session_question_id", "exam_session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_exam_activity_logs_exam_attempt_id_created_at",
                table: "exam_activity_logs",
                columns: new[] { "exam_attempt_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_exam_activity_logs_user_id",
                table: "exam_activity_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_schedule_students_student_id",
                table: "exam_schedule_students",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_schedules_created_by",
                table: "exam_schedules",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_exam_schedules_start_time_end_time",
                table: "exam_schedules",
                columns: new[] { "start_time", "end_time" });

            migrationBuilder.CreateIndex(
                name: "IX_exam_session_questions_exam_session_id_display_order",
                table: "exam_session_questions",
                columns: new[] { "exam_session_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_session_questions_exam_session_id_question_bank_id",
                table: "exam_session_questions",
                columns: new[] { "exam_session_id", "question_bank_id" });

            migrationBuilder.CreateIndex(
                name: "IX_exam_session_questions_exam_session_id_question_id",
                table: "exam_session_questions",
                columns: new[] { "exam_session_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_session_questions_question_id",
                table: "exam_session_questions",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_session_questions_question_id_question_bank_id",
                table: "exam_session_questions",
                columns: new[] { "question_id", "question_bank_id" });

            migrationBuilder.CreateIndex(
                name: "IX_expected_key_points_question_id_display_order",
                table: "expected_key_points",
                columns: new[] { "question_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_evaluations_answer_id",
                table: "lecturer_evaluations",
                column: "answer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_evaluations_lecturer_id",
                table: "lecturer_evaluations",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "IX_reports_exam_attempt_id",
                table: "reports",
                column: "exam_attempt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rubric_criteria_rubric_id_name",
                table: "rubric_criteria",
                columns: new[] { "rubric_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_question_bank_id",
                table: "rubrics",
                column: "question_bank_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_interaction_logs_exam_attempt_id_answer_id",
                table: "service_interaction_logs",
                columns: new[] { "exam_attempt_id", "answer_id" });

            migrationBuilder.CreateIndex(
                name: "IX_service_interaction_logs_exam_attempt_id_request_time",
                table: "service_interaction_logs",
                columns: new[] { "exam_attempt_id", "request_time" });

            migrationBuilder.CreateIndex(
                name: "IX_transcripts_answer_id",
                table: "transcripts",
                column: "answer_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_exam_sessions_exam_schedules_schedule_id",
                table: "exam_sessions",
                column: "schedule_id",
                principalTable: "exam_schedules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_question_banks_users_created_by",
                table: "question_banks",
                column: "created_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_questions_questions_parent_question_id_question_bank_id",
                table: "questions",
                columns: new[] { "parent_question_id", "question_bank_id" },
                principalTable: "questions",
                principalColumns: new[] { "id", "question_bank_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exam_sessions_exam_schedules_schedule_id",
                table: "exam_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_question_banks_users_created_by",
                table: "question_banks");

            migrationBuilder.DropForeignKey(
                name: "FK_questions_questions_parent_question_id_question_bank_id",
                table: "questions");

            migrationBuilder.DropTable(
                name: "ai_evaluations");

            migrationBuilder.DropTable(
                name: "exam_activity_logs");

            migrationBuilder.DropTable(
                name: "exam_schedule_students");

            migrationBuilder.DropTable(
                name: "expected_key_points");

            migrationBuilder.DropTable(
                name: "lecturer_evaluations");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "rubric_criteria");

            migrationBuilder.DropTable(
                name: "service_interaction_logs");

            migrationBuilder.DropTable(
                name: "transcripts");

            migrationBuilder.DropTable(
                name: "exam_schedules");

            migrationBuilder.DropTable(
                name: "rubrics");

            migrationBuilder.DropTable(
                name: "answers");

            migrationBuilder.DropTable(
                name: "attempt_questions");

            migrationBuilder.DropTable(
                name: "exam_session_questions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_questions_id_question_bank_id",
                table: "questions");

            migrationBuilder.DropIndex(
                name: "ix_questions_parent_question_id",
                table: "questions");

            migrationBuilder.DropIndex(
                name: "IX_questions_parent_question_id_question_bank_id",
                table: "questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_parent",
                table: "questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_questions_type",
                table: "questions");

            migrationBuilder.DropIndex(
                name: "ix_question_banks_created_by",
                table: "question_banks");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_exam_sessions_id_question_bank_id",
                table: "exam_sessions");

            migrationBuilder.DropIndex(
                name: "ix_exam_sessions_schedule_id",
                table: "exam_sessions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_exam_attempts_id_exam_session_id",
                table: "exam_attempts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_attempts_completion",
                table: "exam_attempts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_exam_attempts_status",
                table: "exam_attempts");

            migrationBuilder.DropColumn(
                name: "full_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "description",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "name",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "parent_question_id",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "question_type",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "question_banks");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "question_banks");

            migrationBuilder.DropColumn(
                name: "schedule_id",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "exam_attempts");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "exam_attempts");

            migrationBuilder.DropColumn(
                name: "status",
                table: "exam_attempts");
        }
    }
}
