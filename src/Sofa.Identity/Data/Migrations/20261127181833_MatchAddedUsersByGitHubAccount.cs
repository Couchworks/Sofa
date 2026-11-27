using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Identity.Data.Migrations;

/// <inheritdoc />
public partial class _20261127181833_MatchAddedUsersByGitHubAccount : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_users_normalized_github_login",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "normalized_github_login",
            schema: "identity",
            table: "users");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "first_signed_in_at",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(CountEveryoneWithAPasswordOrALinkedAccountAsSignedIn);
        migrationBuilder.Sql(RemoveUsersAddedByLoginWithoutAnAccountIdSoAnAdminAddsThemAgain);
    }

    private const string CountEveryoneWithAPasswordOrALinkedAccountAsSignedIn = """
        update identity.users set first_signed_in_at = created_at
        where password_hash <> '' or github_account_id is not null;
        """;

    private const string RemoveUsersAddedByLoginWithoutAnAccountIdSoAnAdminAddsThemAgain = """
        create temporary table added_by_login_without_an_account_id on commit drop as
            select id from identity.users
            where first_signed_in_at is null and github_account_id is null and email is null and password_hash = '';
        delete from identity.team_users where user_id in (select id from added_by_login_without_an_account_id);
        delete from identity.workspace_users where user_id in (select id from added_by_login_without_an_account_id);
        delete from identity.users where id in (select id from added_by_login_without_an_account_id);
        """;

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "first_signed_in_at",
            schema: "identity",
            table: "users");

        migrationBuilder.AddColumn<string>(
            name: "normalized_github_login",
            schema: "identity",
            table: "users",
            type: "character varying(39)",
            maxLength: 39,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_normalized_github_login",
            schema: "identity",
            table: "users",
            column: "normalized_github_login");
    }
}
