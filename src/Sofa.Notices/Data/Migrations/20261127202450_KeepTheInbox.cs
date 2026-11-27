using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Notices.Data.Migrations;

/// <inheritdoc />
public partial class _20261127202450_KeepTheInbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "notices");

        migrationBuilder.CreateTable(
            name: "inbox_items",
            schema: "notices",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                @for = table.Column<string>(name: "for", type: "character varying(40)", maxLength: 40, nullable: false),
                subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_inbox_items", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_inbox_items_kind_subject_id",
            schema: "notices",
            table: "inbox_items",
            columns: new[] { "kind", "subject_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_inbox_items_workspace_id_for",
            schema: "notices",
            table: "inbox_items",
            columns: new[] { "workspace_id", "for" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "inbox_items",
            schema: "notices");
    }
}
