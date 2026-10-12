using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiViva.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBasicExamSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exam_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    question_bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_sessions", x => x.id);
                    table.CheckConstraint("ck_exam_sessions_status", "status IN ('Draft', 'Open', 'Closed')");
                    table.ForeignKey(
                        name: "FK_exam_sessions_question_banks_question_bank_id",
                        column: x => x.question_bank_id,
                        principalTable: "question_banks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_sessions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_exam_attempts_exam_sessions_exam_session_id",
                        column: x => x.exam_session_id,
                        principalTable: "exam_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exam_attempts_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exam_attempts_student_id",
                table: "exam_attempts",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ux_exam_attempts_session_student",
                table: "exam_attempts",
                columns: new[] { "exam_session_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_sessions_created_by",
                table: "exam_sessions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sessions_question_bank_id",
                table: "exam_sessions",
                column: "question_bank_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_sessions_status",
                table: "exam_sessions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_attempts");

            migrationBuilder.DropTable(
                name: "exam_sessions");
        }
    }
}
