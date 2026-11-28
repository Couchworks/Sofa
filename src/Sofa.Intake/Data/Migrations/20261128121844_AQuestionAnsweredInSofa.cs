using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Intake.Data.Migrations;

/// <inheritdoc />
public partial class _20261128121844_AQuestionAnsweredInSofa : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "answered_in_sofa",
            schema: "intake",
            table: "question_comments",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "answered_in_sofa",
            schema: "intake",
            table: "question_comments");
    }
}
