using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Records.Data.Migrations;

/// <inheritdoc />
public partial class _20261128100643_ConvertTranscriptsIntoEntries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "transcript_lines",
            schema: "records");

        migrationBuilder.CreateTable(
            name: "transcript_entries",
            schema: "records",
            columns: table => new
            {
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                line = table.Column<int>(type: "integer", nullable: false),
                part = table.Column<int>(type: "integer", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                text = table.Column<string>(type: "text", nullable: true),
                tool_use_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                tool_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                tool_input = table.Column<string>(type: "jsonb", nullable: true),
                tool_input_cut = table.Column<bool>(type: "boolean", nullable: false),
                tool_result = table.Column<string>(type: "text", nullable: true),
                tool_result_cut = table.Column<bool>(type: "boolean", nullable: false),
                tool_result_line = table.Column<int>(type: "integer", nullable: true),
                at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_transcript_entries", x => new { x.run_id, x.line, x.part });
            });

        migrationBuilder.CreateIndex(
            name: "ix_transcript_entries_run_id_tool_use_id",
            schema: "records",
            table: "transcript_entries",
            columns: new[] { "run_id", "tool_use_id" });

        migrationBuilder.CreateIndex(
            name: "ix_transcript_entries_workspace_id_run_id",
            schema: "records",
            table: "transcript_entries",
            columns: new[] { "workspace_id", "run_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "transcript_entries",
            schema: "records");

        migrationBuilder.CreateTable(
            name: "transcript_lines",
            schema: "records",
            columns: table => new
            {
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                number = table.Column<int>(type: "integer", nullable: false),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                text = table.Column<string>(type: "text", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_transcript_lines", x => new { x.run_id, x.number });
            });

        migrationBuilder.CreateIndex(
            name: "ix_transcript_lines_workspace_id_run_id",
            schema: "records",
            table: "transcript_lines",
            columns: new[] { "workspace_id", "run_id" });
    }
}
