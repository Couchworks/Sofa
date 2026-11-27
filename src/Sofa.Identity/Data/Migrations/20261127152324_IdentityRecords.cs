using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sofa.Identity.Data.Migrations;

/// <inheritdoc />
public partial class _20261127152324_IdentityRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "identity");

        migrationBuilder.CreateTable(
            name: "data_protection_keys",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                friendly_name = table.Column<string>(type: "text", nullable: true),
                xml = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_data_protection_keys", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "installation",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false),
                setup_code_hash = table.Column<string>(type: "text", nullable: true),
                set_up_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_installation", x => x.id);
                table.CheckConstraint("ck_installation_only_one", "id = 1");
            });

        migrationBuilder.CreateTable(
            name: "openiddict_applications",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                application_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                client_secret = table.Column<string>(type: "text", nullable: true),
                client_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                concurrency_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                consent_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                display_name = table.Column<string>(type: "text", nullable: true),
                display_names = table.Column<string>(type: "text", nullable: true),
                json_web_key_set = table.Column<string>(type: "text", nullable: true),
                permissions = table.Column<string>(type: "text", nullable: true),
                post_logout_redirect_uris = table.Column<string>(type: "text", nullable: true),
                properties = table.Column<string>(type: "text", nullable: true),
                redirect_uris = table.Column<string>(type: "text", nullable: true),
                requirements = table.Column<string>(type: "text", nullable: true),
                settings = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_openiddict_applications", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "openiddict_scopes",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                concurrency_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                description = table.Column<string>(type: "text", nullable: true),
                descriptions = table.Column<string>(type: "text", nullable: true),
                display_name = table.Column<string>(type: "text", nullable: true),
                display_names = table.Column<string>(type: "text", nullable: true),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                properties = table.Column<string>(type: "text", nullable: true),
                resources = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_openiddict_scopes", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "token_keys",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                use = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                rsa_private_key_pem = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_token_keys", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "workspaces",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_workspaces", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "openiddict_authorizations",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                application_id = table.Column<Guid>(type: "uuid", nullable: true),
                concurrency_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                creation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                properties = table.Column<string>(type: "text", nullable: true),
                scopes = table.Column<string>(type: "text", nullable: true),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_openiddict_authorizations", x => x.id);
                table.ForeignKey(
                    name: "fk_openiddict_authorizations_openiddict_applications_applicati",
                    column: x => x.application_id,
                    principalSchema: "identity",
                    principalTable: "openiddict_applications",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "teams",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_teams", x => x.id);
                table.UniqueConstraint("ak_teams_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "fk_teams_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalSchema: "identity",
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "workspace_users",
            schema: "identity",
            columns: table => new
            {
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_admin = table.Column<bool>(type: "boolean", nullable: false),
                joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_workspace_users", x => new { x.workspace_id, x.user_id });
                table.ForeignKey(
                    name: "fk_workspace_users_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_workspace_users_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalSchema: "identity",
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "openiddict_tokens",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                application_id = table.Column<Guid>(type: "uuid", nullable: true),
                authorization_id = table.Column<Guid>(type: "uuid", nullable: true),
                concurrency_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                creation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                payload = table.Column<string>(type: "text", nullable: true),
                properties = table.Column<string>(type: "text", nullable: true),
                redemption_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                reference_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_openiddict_tokens", x => x.id);
                table.ForeignKey(
                    name: "fk_openiddict_tokens_openiddict_applications_application_id",
                    column: x => x.application_id,
                    principalSchema: "identity",
                    principalTable: "openiddict_applications",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "fk_openiddict_tokens_openiddict_authorizations_authorization_id",
                    column: x => x.authorization_id,
                    principalSchema: "identity",
                    principalTable: "openiddict_authorizations",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "agents",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                owner_team_id = table.Column<Guid>(type: "uuid", nullable: true),
                registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_agents", x => x.id);
                table.CheckConstraint("ck_agents_owned_by_a_user_or_a_team", "(owner_user_id is null) <> (owner_team_id is null)");
                table.ForeignKey(
                    name: "fk_agents_teams_workspace_id_owner_team_id",
                    columns: x => new { x.workspace_id, x.owner_team_id },
                    principalSchema: "identity",
                    principalTable: "teams",
                    principalColumns: new[] { "workspace_id", "id" });
                table.ForeignKey(
                    name: "fk_agents_workspace_users_workspace_id_owner_user_id",
                    columns: x => new { x.workspace_id, x.owner_user_id },
                    principalSchema: "identity",
                    principalTable: "workspace_users",
                    principalColumns: new[] { "workspace_id", "user_id" });
                table.ForeignKey(
                    name: "fk_agents_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalSchema: "identity",
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "team_users",
            schema: "identity",
            columns: table => new
            {
                team_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_team_users", x => new { x.team_id, x.user_id });
                table.ForeignKey(
                    name: "fk_team_users_teams_workspace_id_team_id",
                    columns: x => new { x.workspace_id, x.team_id },
                    principalSchema: "identity",
                    principalTable: "teams",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_team_users_workspace_users_workspace_id_user_id",
                    columns: x => new { x.workspace_id, x.user_id },
                    principalSchema: "identity",
                    principalTable: "workspace_users",
                    principalColumns: new[] { "workspace_id", "user_id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_agents_workspace_id_owner_team_id",
            schema: "identity",
            table: "agents",
            columns: new[] { "workspace_id", "owner_team_id" });

        migrationBuilder.CreateIndex(
            name: "ix_agents_workspace_id_owner_user_id",
            schema: "identity",
            table: "agents",
            columns: new[] { "workspace_id", "owner_user_id" });

        migrationBuilder.CreateIndex(
            name: "ix_openiddict_applications_client_id",
            schema: "identity",
            table: "openiddict_applications",
            column: "client_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_openiddict_authorizations_application_id_status_subject_type",
            schema: "identity",
            table: "openiddict_authorizations",
            columns: new[] { "application_id", "status", "subject", "type" });

        migrationBuilder.CreateIndex(
            name: "ix_openiddict_scopes_name",
            schema: "identity",
            table: "openiddict_scopes",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_openiddict_tokens_application_id_status_subject_type",
            schema: "identity",
            table: "openiddict_tokens",
            columns: new[] { "application_id", "status", "subject", "type" });

        migrationBuilder.CreateIndex(
            name: "ix_openiddict_tokens_authorization_id",
            schema: "identity",
            table: "openiddict_tokens",
            column: "authorization_id");

        migrationBuilder.CreateIndex(
            name: "ix_openiddict_tokens_reference_id",
            schema: "identity",
            table: "openiddict_tokens",
            column: "reference_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_team_users_workspace_id_team_id",
            schema: "identity",
            table: "team_users",
            columns: new[] { "workspace_id", "team_id" });

        migrationBuilder.CreateIndex(
            name: "ix_team_users_workspace_id_user_id",
            schema: "identity",
            table: "team_users",
            columns: new[] { "workspace_id", "user_id" });

        migrationBuilder.CreateIndex(
            name: "ix_users_normalized_email",
            schema: "identity",
            table: "users",
            column: "normalized_email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_workspace_users_user_id",
            schema: "identity",
            table: "workspace_users",
            column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "agents",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "data_protection_keys",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "installation",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "openiddict_scopes",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "openiddict_tokens",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "team_users",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "token_keys",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "openiddict_authorizations",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "teams",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "workspace_users",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "openiddict_applications",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "users",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "workspaces",
            schema: "identity");
    }
}
