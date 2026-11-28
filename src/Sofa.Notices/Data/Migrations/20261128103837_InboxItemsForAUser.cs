using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Notices.Data.Migrations;

/// <inheritdoc />
public partial class _20261128103837_InboxItemsForAUser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "user_id",
            schema: "notices",
            table: "inbox_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_inbox_items_workspace_id_user_id",
            schema: "notices",
            table: "inbox_items",
            columns: new[] { "workspace_id", "user_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_inbox_items_workspace_id_user_id",
            schema: "notices",
            table: "inbox_items");

        migrationBuilder.DropColumn(
            name: "user_id",
            schema: "notices",
            table: "inbox_items");
    }
}
