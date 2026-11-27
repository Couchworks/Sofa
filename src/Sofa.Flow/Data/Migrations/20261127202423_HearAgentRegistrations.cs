using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Flow.Data.Migrations;

/// <inheritdoc />
public partial class _20261127202423_HearAgentRegistrations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "registration",
            schema: "flow",
            table: "agents",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Pending");

        migrationBuilder.AddColumn<string>(
            name: "registration_refused_because",
            schema: "flow",
            table: "agents",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "registration",
            schema: "flow",
            table: "agents");

        migrationBuilder.DropColumn(
            name: "registration_refused_because",
            schema: "flow",
            table: "agents");
    }
}
