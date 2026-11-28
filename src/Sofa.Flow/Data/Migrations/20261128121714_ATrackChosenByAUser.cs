using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Flow.Data.Migrations;

/// <inheritdoc />
public partial class _20261128121714_ATrackChosenByAUser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "track_chosen_by_a_user",
            schema: "flow",
            table: "work_items",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "track_chosen_by_a_user",
            schema: "flow",
            table: "work_items");
    }
}
