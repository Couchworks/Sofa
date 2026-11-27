using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Flow.Data.Migrations;

/// <inheritdoc />
public partial class _20261127202050_KeepWorkItems : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "binding_version",
            schema: "flow",
            table: "projects",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<bool>(
            name: "keeps_issue_state",
            schema: "flow",
            table: "projects",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "last_work_item_number",
            schema: "flow",
            table: "projects",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "work_items",
            schema: "flow",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                number = table.Column<int>(type: "integer", nullable: false),
                key = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                request_id = table.Column<Guid>(type: "uuid", nullable: false),
                request_repository = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                request_number = table.Column<long>(type: "bigint", nullable: false),
                request_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                delegating_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_work_items", x => x.id);
                table.ForeignKey(
                    name: "fk_work_items_projects_project_id",
                    column: x => x.project_id,
                    principalSchema: "flow",
                    principalTable: "projects",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_work_items_project_id_number",
            schema: "flow",
            table: "work_items",
            columns: new[] { "project_id", "number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_work_items_request_id",
            schema: "flow",
            table: "work_items",
            column: "request_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "work_items",
            schema: "flow");

        migrationBuilder.DropColumn(
            name: "binding_version",
            schema: "flow",
            table: "projects");

        migrationBuilder.DropColumn(
            name: "keeps_issue_state",
            schema: "flow",
            table: "projects");

        migrationBuilder.DropColumn(
            name: "last_work_item_number",
            schema: "flow",
            table: "projects");
    }
}
