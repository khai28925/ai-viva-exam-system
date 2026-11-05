using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiViva.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiInterviewAndReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "finalized_at",
                table: "reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "finalized_by",
                table: "reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_finalized",
                table: "reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "answer_time_seconds",
                table: "exam_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 120);

            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "exam_sessions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "vi-VN");

            migrationBuilder.AddColumn<int>(
                name: "max_follow_ups",
                table: "exam_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "assessment_context_json",
                table: "attempt_questions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "presented_at",
                table: "attempt_questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "submitted_at",
                table: "attempt_questions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "submitted_transcript",
                table: "attempt_questions",
                type: "character varying(12000)",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_finalized_by",
                table: "reports",
                column: "finalized_by");

            migrationBuilder.AddForeignKey(
                name: "FK_reports_users_finalized_by",
                table: "reports",
                column: "finalized_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reports_users_finalized_by",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "IX_reports_finalized_by",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "finalized_at",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "finalized_by",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "is_finalized",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "answer_time_seconds",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "language",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "max_follow_ups",
                table: "exam_sessions");

            migrationBuilder.DropColumn(
                name: "assessment_context_json",
                table: "attempt_questions");

            migrationBuilder.DropColumn(
                name: "presented_at",
                table: "attempt_questions");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                table: "attempt_questions");

            migrationBuilder.DropColumn(
                name: "submitted_transcript",
                table: "attempt_questions");
        }
    }
}
