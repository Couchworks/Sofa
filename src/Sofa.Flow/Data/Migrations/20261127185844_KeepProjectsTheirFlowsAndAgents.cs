using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Flow.Data.Migrations;

/// <inheritdoc />
public partial class _20261127185844_KeepProjectsTheirFlowsAndAgents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "flow");

        migrationBuilder.CreateTable(
            name: "projects",
            schema: "flow",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                request_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                request_repository = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                request_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                code_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                code_repositories = table.Column<List<string>>(type: "text[]", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_projects", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "agents",
            schema: "flow",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                owner_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                instructions = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                vendor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_agents", x => x.id);
                table.UniqueConstraint("ak_agents_project_id_id", x => new { x.project_id, x.id });
                table.ForeignKey(
                    name: "fk_agents_projects_project_id",
                    column: x => x.project_id,
                    principalSchema: "flow",
                    principalTable: "projects",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "phases",
            schema: "flow",
            columns: table => new
            {
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                pattern = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                cast_agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                gate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                sandbox = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_phases", x => new { x.project_id, x.name });
                table.ForeignKey(
                    name: "fk_phases_agents_project_id_cast_agent_id",
                    columns: x => new { x.project_id, x.cast_agent_id },
                    principalSchema: "flow",
                    principalTable: "agents",
                    principalColumns: new[] { "project_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_phases_projects_project_id",
                    column: x => x.project_id,
                    principalSchema: "flow",
                    principalTable: "projects",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_phases_project_id_cast_agent_id",
            schema: "flow",
            table: "phases",
            columns: new[] { "project_id", "cast_agent_id" });

        migrationBuilder.CreateIndex(
            name: "ix_projects_workspace_id_key",
            schema: "flow",
            table: "projects",
            columns: new[] { "workspace_id", "key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "phases",
            schema: "flow");

        migrationBuilder.DropTable(
            name: "agents",
            schema: "flow");

        migrationBuilder.DropTable(
            name: "projects",
            schema: "flow");
    }
}
