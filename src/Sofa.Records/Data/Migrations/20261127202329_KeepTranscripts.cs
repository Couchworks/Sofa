using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Records.Data.Migrations;

/// <inheritdoc />
public partial class _20261127202329_KeepTranscripts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "records");

        migrationBuilder.CreateTable(
            name: "transcript_lines",
            schema: "records",
            columns: table => new
            {
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                number = table.Column<int>(type: "integer", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                text = table.Column<string>(type: "text", nullable: false),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "transcript_lines",
            schema: "records");
    }
}
