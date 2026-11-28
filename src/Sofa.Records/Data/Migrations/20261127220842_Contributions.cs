using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Records.Data.Migrations;

/// <inheritdoc />
public partial class _20261127220842_Contributions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "contributions",
            schema: "records",
            columns: table => new
            {
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                markdown = table.Column<string>(type: "character varying(200000)", maxLength: 200000, nullable: false),
                made_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_contributions", x => x.run_id);
            });

        migrationBuilder.CreateTable(
            name: "contribution_attachments",
            schema: "records",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                size = table.Column<long>(type: "bigint", nullable: false),
                file_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_contribution_attachments", x => x.id);
                table.ForeignKey(
                    name: "fk_contribution_attachments_contributions_run_id",
                    column: x => x.run_id,
                    principalSchema: "records",
                    principalTable: "contributions",
                    principalColumn: "run_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_contribution_attachments_run_id_position",
            schema: "records",
            table: "contribution_attachments",
            columns: new[] { "run_id", "position" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_contributions_workspace_id_work_item_id_made_at",
            schema: "records",
            table: "contributions",
            columns: new[] { "workspace_id", "work_item_id", "made_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "contribution_attachments",
            schema: "records");

        migrationBuilder.DropTable(
            name: "contributions",
            schema: "records");
    }
}
