using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Identity.Data.Migrations;

/// <inheritdoc />
public partial class _20261127185530_AddedUsersJoinTheWorkspacesTeam : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(UsersInNoTeamOfTheirWorkspaceJoinItsOldestTeam);
    }

    private const string UsersInNoTeamOfTheirWorkspaceJoinItsOldestTeam = """
        insert into identity.team_users (workspace_id, team_id, user_id, joined_at)
        select workspace_user.workspace_id, oldest_team.id, workspace_user.user_id, now()
        from identity.workspace_users workspace_user
        cross join lateral (
            select team.id from identity.teams team
            where team.workspace_id = workspace_user.workspace_id
            order by team.created_at, team.id
            limit 1) oldest_team
        where not exists (
            select 1 from identity.team_users team_user
            where team_user.workspace_id = workspace_user.workspace_id and team_user.user_id = workspace_user.user_id);
        """;

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
