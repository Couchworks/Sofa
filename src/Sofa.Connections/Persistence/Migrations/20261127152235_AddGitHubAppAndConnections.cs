using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Connections.Persistence.Migrations;

public partial class AddGitHubAppAndConnections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "connections");

        migrationBuilder.CreateTable(
            name: "connections",
            schema: "connections",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                system = table.Column<string>(type: "text", nullable: false),
                github_installation_id = table.Column<long>(type: "bigint", nullable: false),
                account = table.Column<string>(type: "text", nullable: false),
                account_type = table.Column<string>(type: "text", nullable: false),
                added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                suspended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_connections", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "github_app_creations",
            schema: "connections",
            columns: table => new
            {
                state = table.Column<string>(type: "text", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_github_app_creations", x => x.state);
            });

        migrationBuilder.CreateTable(
            name: "github_apps",
            schema: "connections",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false),
                slug = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                owner_login = table.Column<string>(type: "text", nullable: false),
                html_url = table.Column<string>(type: "text", nullable: false),
                client_id = table.Column<string>(type: "text", nullable: false),
                sealed_private_key = table.Column<string>(type: "text", nullable: false),
                sealed_webhook_secret = table.Column<string>(type: "text", nullable: false),
                sealed_client_secret = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                deliveries_checked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_github_apps", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "received_deliveries",
            schema: "connections",
            columns: table => new
            {
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_received_deliveries", x => x.delivery_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_connections_github_installation_id",
            schema: "connections",
            table: "connections",
            column: "github_installation_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_connections_workspace_id",
            schema: "connections",
            table: "connections",
            column: "workspace_id");

        migrationBuilder.CreateIndex(
            name: "ix_received_deliveries_received_at",
            schema: "connections",
            table: "received_deliveries",
            column: "received_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "connections",
            schema: "connections");

        migrationBuilder.DropTable(
            name: "github_app_creations",
            schema: "connections");

        migrationBuilder.DropTable(
            name: "github_apps",
            schema: "connections");

        migrationBuilder.DropTable(
            name: "received_deliveries",
            schema: "connections");
    }
}
