using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Execution.Data.Migrations;

/// <inheritdoc />
public partial class _20261128100641_KeepTheWorkItemKeyOfARun : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "work_item_key",
            schema: "execution",
            table: "runs",
            type: "character varying(21)",
            maxLength: 21,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "work_item_key",
            schema: "execution",
            table: "runs");
    }
}
