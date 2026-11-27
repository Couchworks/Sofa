using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Execution.Data.Migrations;

/// <inheritdoc />
public partial class _20261127185947_KeepRunners : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "execution");

        migrationBuilder.CreateTable(
            name: "runners",
            schema: "execution",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                vendor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                owner_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                runs_at_once = table.Column<int>(type: "integer", nullable: false),
                api_key_id = table.Column<Guid>(type: "uuid", nullable: true),
                worker_volume = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                signed_in = table.Column<bool>(type: "boolean", nullable: false),
                sign_in_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                sign_in_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_runners", x => x.id);
                table.CheckConstraint("ck_runners_api_key_runners_have_a_key", "kind <> 'ApiKey' or api_key_id is not null");
                table.CheckConstraint("ck_runners_owned_by_a_user_or_a_team", "(owner_user_id is null) <> (owner_team_id is null)");
                table.CheckConstraint("ck_runners_run_at_least_one_at_once", "runs_at_once >= 1");
                table.CheckConstraint("ck_runners_subscriptions_are_personal", "kind <> 'Subscription' or (owner_user_id is not null and worker_volume is not null and api_key_id is null)");
            });

        migrationBuilder.CreateIndex(
            name: "ix_runners_workspace_id_owner_team_id",
            schema: "execution",
            table: "runners",
            columns: new[] { "workspace_id", "owner_team_id" });

        migrationBuilder.CreateIndex(
            name: "ix_runners_workspace_id_owner_user_id",
            schema: "execution",
            table: "runners",
            columns: new[] { "workspace_id", "owner_user_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "runners",
            schema: "execution");
    }
}
