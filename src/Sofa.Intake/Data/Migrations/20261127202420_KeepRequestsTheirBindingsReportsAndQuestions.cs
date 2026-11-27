using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Intake.Data.Migrations;

/// <inheritdoc />
public partial class _20261127202420_KeepRequestsTheirBindingsReportsAndQuestions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "intake");

        migrationBuilder.CreateTable(
            name: "request_bindings",
            schema: "intake",
            columns: table => new
            {
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                repository = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                keeps_issue_state = table.Column<bool>(type: "boolean", nullable: false),
                removed = table.Column<bool>(type: "boolean", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_request_bindings", x => x.project_id);
            });

        migrationBuilder.CreateTable(
            name: "requests",
            schema: "intake",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                repository = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                number = table.Column<long>(type: "bigint", nullable: false),
                title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                marked_by_github_account_id = table.Column<long>(type: "bigint", nullable: false),
                marked_by_github_login = table.Column<string>(type: "character varying(39)", maxLength: 39, nullable: false),
                delegating_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                marked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                phase_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_requests", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "question_comments",
            schema: "intake",
            columns: table => new
            {
                question_id = table.Column<Guid>(type: "uuid", nullable: false),
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                work_item_key = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                comment_id = table.Column<long>(type: "bigint", nullable: false),
                asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                answered_by_comment_id = table.Column<long>(type: "bigint", nullable: true),
                answered_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                answer = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_question_comments", x => x.question_id);
                table.ForeignKey(
                    name: "fk_question_comments_requests_request_id",
                    column: x => x.request_id,
                    principalSchema: "intake",
                    principalTable: "requests",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "report_comments",
            schema: "intake",
            columns: table => new
            {
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                comment_id = table.Column<long>(type: "bigint", nullable: false),
                posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_report_comments", x => new { x.request_id, x.phase });
                table.ForeignKey(
                    name: "fk_report_comments_requests_request_id",
                    column: x => x.request_id,
                    principalSchema: "intake",
                    principalTable: "requests",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_question_comments_request_id",
            schema: "intake",
            table: "question_comments",
            column: "request_id");

        migrationBuilder.CreateIndex(
            name: "ix_request_bindings_connection_id",
            schema: "intake",
            table: "request_bindings",
            column: "connection_id");

        migrationBuilder.CreateIndex(
            name: "ix_requests_connection_id_repository_number",
            schema: "intake",
            table: "requests",
            columns: new[] { "connection_id", "repository", "number" });

        migrationBuilder.CreateIndex(
            name: "ix_requests_project_id_repository_number",
            schema: "intake",
            table: "requests",
            columns: new[] { "project_id", "repository", "number" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "question_comments",
            schema: "intake");

        migrationBuilder.DropTable(
            name: "report_comments",
            schema: "intake");

        migrationBuilder.DropTable(
            name: "request_bindings",
            schema: "intake");

        migrationBuilder.DropTable(
            name: "requests",
            schema: "intake");
    }
}
