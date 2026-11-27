using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Connections.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261127185621_KeepApiKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "api_keys",
            schema: "connections",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                vendor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                sealed_key = table.Column<string>(type: "text", nullable: false),
                saved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_api_keys", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_api_keys_workspace_id",
            schema: "connections",
            table: "api_keys",
            column: "workspace_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "api_keys",
            schema: "connections");
    }
}
