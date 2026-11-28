using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Notices.Data.Migrations;

/// <inheritdoc />
public partial class _20261128122324_WarnBeforeASignInEnds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "work_item_key",
            schema: "notices",
            table: "inbox_items",
            type: "character varying(21)",
            maxLength: 21,
            nullable: true);

        migrationBuilder.Sql("""
            update notices.inbox_items set work_item_key = split_part(title, ':', 1)
            where kind <> 'RequestAwaitingDelegation' and position(':' in title) > 0
            """);

        migrationBuilder.CreateTable(
            name: "runner_sign_ins",
            schema: "notices",
            columns: table => new
            {
                runner_id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                runner_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                signed_in = table.Column<bool>(type: "boolean", nullable: false),
                ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                refused_a_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_runner_sign_ins", x => x.runner_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_runner_sign_ins_workspace_id_user_id",
            schema: "notices",
            table: "runner_sign_ins",
            columns: new[] { "workspace_id", "user_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "runner_sign_ins",
            schema: "notices");

        migrationBuilder.DropColumn(
            name: "work_item_key",
            schema: "notices",
            table: "inbox_items");
    }
}
