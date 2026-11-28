using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sofa.Contracts.Flow;
using Sofa.Flow.Projects;

#nullable disable

namespace Sofa.Flow.Data.Migrations;

/// <inheritdoc />
public partial class _20261128103741_MoveWorkItemsThroughTheFlow : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "current_run_id",
            schema: "flow",
            table: "work_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "done_at",
            schema: "flow",
            table: "work_items",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "phase",
            schema: "flow",
            table: "work_items",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "pull_request_number",
            schema: "flow",
            table: "work_items",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "pull_request_repository",
            schema: "flow",
            table: "work_items",
            type: "character varying(140)",
            maxLength: 140,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "pull_request_state",
            schema: "flow",
            table: "work_items",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "pull_request_url",
            schema: "flow",
            table: "work_items",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "state",
            schema: "flow",
            table: "work_items",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "AwaitsDelegation");

        migrationBuilder.AddColumn<string>(
            name: "track",
            schema: "flow",
            table: "work_items",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "wait_id",
            schema: "flow",
            table: "work_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "waiting_because",
            schema: "flow",
            table: "work_items",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "waiting_for",
            schema: "flow",
            table: "work_items",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "waiting_since",
            schema: "flow",
            table: "work_items",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<uint>(
            name: "xmin",
            schema: "flow",
            table: "work_items",
            type: "xid",
            rowVersion: true,
            nullable: false,
            defaultValue: 0u);

        migrationBuilder.AddColumn<string>(
            name: "instructions",
            schema: "flow",
            table: "phases",
            type: "character varying(20000)",
            maxLength: 20000,
            nullable: false,
            defaultValue: "");

        foreach (var phase in Enum.GetValues<PhaseName>())
        {
            migrationBuilder.Sql(
                $"update flow.phases set instructions = '{DefaultFlow.InstructionsFor(phase).Replace("'", "''")}' where name = '{phase}'");
        }

        migrationBuilder.CreateTable(
            name: "phase_ends",
            schema: "flow",
            columns: table => new
            {
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                gate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                standing = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                comment = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_phase_ends", x => x.run_id);
                table.ForeignKey(
                    name: "fk_phase_ends_work_items_work_item_id",
                    column: x => x.work_item_id,
                    principalSchema: "flow",
                    principalTable: "work_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "work_item_runs",
            schema: "flow",
            columns: table => new
            {
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                retry_of = table.Column<Guid>(type: "uuid", nullable: true),
                ended_as = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_work_item_runs", x => x.run_id);
                table.ForeignKey(
                    name: "fk_work_item_runs_work_items_work_item_id",
                    column: x => x.work_item_id,
                    principalSchema: "flow",
                    principalTable: "work_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_phase_ends_work_item_id_ended_at",
            schema: "flow",
            table: "phase_ends",
            columns: new[] { "work_item_id", "ended_at" });

        migrationBuilder.CreateIndex(
            name: "ix_work_item_runs_work_item_id_started_at",
            schema: "flow",
            table: "work_item_runs",
            columns: new[] { "work_item_id", "started_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "phase_ends",
            schema: "flow");

        migrationBuilder.DropTable(
            name: "work_item_runs",
            schema: "flow");

        migrationBuilder.DropColumn(
            name: "current_run_id",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "done_at",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "phase",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "pull_request_number",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "pull_request_repository",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "pull_request_state",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "pull_request_url",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "state",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "track",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "wait_id",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "waiting_because",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "waiting_for",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "waiting_since",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "xmin",
            schema: "flow",
            table: "work_items");

        migrationBuilder.DropColumn(
            name: "instructions",
            schema: "flow",
            table: "phases");
    }
}
