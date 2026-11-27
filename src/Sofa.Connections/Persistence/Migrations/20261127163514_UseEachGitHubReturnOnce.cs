using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Connections.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261127163514_UseEachGitHubReturnOnce : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "github_app_creations",
            schema: "connections");

        migrationBuilder.CreateTable(
            name: "used_github_returns",
            schema: "connections",
            columns: table => new
            {
                nonce = table.Column<Guid>(type: "uuid", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_used_github_returns", x => x.nonce);
            });

        migrationBuilder.CreateIndex(
            name: "ix_used_github_returns_expires_at",
            schema: "connections",
            table: "used_github_returns",
            column: "expires_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "used_github_returns",
            schema: "connections");

        migrationBuilder.CreateTable(
            name: "github_app_creations",
            schema: "connections",
            columns: table => new
            {
                state = table.Column<string>(type: "text", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_github_app_creations", x => x.state);
            });
    }
}
