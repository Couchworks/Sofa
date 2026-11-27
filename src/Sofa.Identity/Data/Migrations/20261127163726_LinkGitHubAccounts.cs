using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Identity.Data.Migrations;

/// <inheritdoc />
public partial class _20261127163726_LinkGitHubAccounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "normalized_email",
            schema: "identity",
            table: "users",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(320)",
            oldMaxLength: 320);

        migrationBuilder.AlterColumn<string>(
            name: "email",
            schema: "identity",
            table: "users",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(320)",
            oldMaxLength: 320);

        migrationBuilder.AddColumn<long>(
            name: "github_account_id",
            schema: "identity",
            table: "users",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "github_login",
            schema: "identity",
            table: "users",
            type: "character varying(39)",
            maxLength: 39,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "normalized_github_login",
            schema: "identity",
            table: "users",
            type: "character varying(39)",
            maxLength: 39,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "github_authorizations",
            schema: "identity",
            columns: table => new
            {
                state_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                linking_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                return_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_github_authorizations", x => x.state_hash);
            });

        migrationBuilder.CreateIndex(
            name: "ix_users_github_account_id",
            schema: "identity",
            table: "users",
            column: "github_account_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_normalized_github_login",
            schema: "identity",
            table: "users",
            column: "normalized_github_login");

        migrationBuilder.CreateIndex(
            name: "ix_github_authorizations_expires_at",
            schema: "identity",
            table: "github_authorizations",
            column: "expires_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "github_authorizations",
            schema: "identity");

        migrationBuilder.DropIndex(
            name: "ix_users_github_account_id",
            schema: "identity",
            table: "users");

        migrationBuilder.DropIndex(
            name: "ix_users_normalized_github_login",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "github_account_id",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "github_login",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "normalized_github_login",
            schema: "identity",
            table: "users");

        migrationBuilder.AlterColumn<string>(
            name: "normalized_email",
            schema: "identity",
            table: "users",
            type: "character varying(320)",
            maxLength: 320,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "character varying(320)",
            oldMaxLength: 320,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "email",
            schema: "identity",
            table: "users",
            type: "character varying(320)",
            maxLength: 320,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "character varying(320)",
            oldMaxLength: 320,
            oldNullable: true);
    }
}
