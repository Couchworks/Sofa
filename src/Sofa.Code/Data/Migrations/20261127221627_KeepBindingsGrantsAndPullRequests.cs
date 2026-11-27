using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Code.Data.Migrations;

/// <inheritdoc />
public partial class _20261127221627_KeepBindingsGrantsAndPullRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "code");

        migrationBuilder.CreateTable(
            name: "bindings",
            schema: "code",
            columns: table => new
            {
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                repositories = table.Column<List<string>>(type: "text[]", nullable: false),
                removed = table.Column<bool>(type: "boolean", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bindings", x => x.project_id);
            });

        migrationBuilder.CreateTable(
            name: "pull_requests",
            schema: "code",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                repository = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                number = table.Column<long>(type: "bigint", nullable: false),
                work_item_key = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                head_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                base_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pull_requests", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "repository_grants",
            schema: "code",
            columns: table => new
            {
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                repositories = table.Column<List<string>>(type: "text[]", nullable: false),
                access = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                own_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                run_ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_repository_grants", x => x.run_id);
            });

        migrationBuilder.CreateTable(
            name: "undone_pushes",
            schema: "code",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                repository = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                @ref = table.Column<string>(name: "ref", type: "character varying(255)", maxLength: 255, nullable: false),
                put_back_to = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                undone_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_undone_pushes", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_bindings_connection_id",
            schema: "code",
            table: "bindings",
            column: "connection_id");

        migrationBuilder.CreateIndex(
            name: "ix_bindings_workspace_id_project_key",
            schema: "code",
            table: "bindings",
            columns: new[] { "workspace_id", "project_key" });

        migrationBuilder.CreateIndex(
            name: "ix_pull_requests_project_id_repository_number",
            schema: "code",
            table: "pull_requests",
            columns: new[] { "project_id", "repository", "number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_pull_requests_project_id_work_item_key",
            schema: "code",
            table: "pull_requests",
            columns: new[] { "project_id", "work_item_key" });

        migrationBuilder.CreateIndex(
            name: "ix_repository_grants_workspace_id_expires_at",
            schema: "code",
            table: "repository_grants",
            columns: new[] { "workspace_id", "expires_at" });

        migrationBuilder.CreateIndex(
            name: "ix_undone_pushes_ref",
            schema: "code",
            table: "undone_pushes",
            column: "ref");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "bindings",
            schema: "code");

        migrationBuilder.DropTable(
            name: "pull_requests",
            schema: "code");

        migrationBuilder.DropTable(
            name: "repository_grants",
            schema: "code");

        migrationBuilder.DropTable(
            name: "undone_pushes",
            schema: "code");
    }
}
