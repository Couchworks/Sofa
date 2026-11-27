using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Execution.Data.Migrations;

/// <inheritdoc />
public partial class _20261127203020_KeepRuns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "runs",
            schema: "execution",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                vendor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                sandbox = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                delegating_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                brief = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                retry_limit = table.Column<int>(type: "integer", nullable: false),
                retries_left = table.Column<int>(type: "integer", nullable: false),
                attempt = table.Column<int>(type: "integer", nullable: false),
                retry_of = table.Column<Guid>(type: "uuid", nullable: true),
                retried_by = table.Column<Guid>(type: "uuid", nullable: true),
                state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                why = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                refused_as = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                waits_for_retry = table.Column<bool>(type: "boolean", nullable: false),
                runner_id = table.Column<Guid>(type: "uuid", nullable: true),
                summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_runs", x => x.id);
                table.CheckConstraint("ck_runs_retries_left_within_the_limit", "retries_left >= 0 and retries_left <= retry_limit");
                table.CheckConstraint("ck_runs_running_on_a_runner", "state <> 'Running' or runner_id is not null");
                table.ForeignKey(
                    name: "fk_runs_runners_runner_id",
                    column: x => x.runner_id,
                    principalSchema: "execution",
                    principalTable: "runners",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_runs_runner_id",
            schema: "execution",
            table: "runs",
            column: "runner_id");

        migrationBuilder.CreateIndex(
            name: "ix_runs_state",
            schema: "execution",
            table: "runs",
            column: "state");

        migrationBuilder.CreateIndex(
            name: "ix_runs_work_item_id",
            schema: "execution",
            table: "runs",
            column: "work_item_id");

        migrationBuilder.CreateIndex(
            name: "ix_runs_workspace_id_queued_at",
            schema: "execution",
            table: "runs",
            columns: new[] { "workspace_id", "queued_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "runs",
            schema: "execution");
    }
}
