using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sofa.Identity.Data;
using Sofa.Identity.Users;
using Sofa.Identity.Workspaces;

namespace Sofa.Identity.Setup;

public sealed record SetupRequest(
    string SetupCode, string Name, string Email, string Password, string WorkspaceName, string TeamName);

public sealed record SetupDone(SetUpUser User, SetUpWorkspace Workspace, SetUpTeam Team);

public sealed record SetUpUser(Guid Id, string Name, string Email);

public sealed record SetUpWorkspace(Guid Id, string Name);

public sealed record SetUpTeam(Guid Id, string Name);

public enum SetupRefusal
{
    AlreadySetUp,
    WrongSetupCode,
}

public sealed class FirstUserSetup(IdentityDb db, IPasswordHasher<User> passwords, TimeProvider time)
{
    public async Task<bool> IsNeededAsync(CancellationToken cancellation) =>
        !await db.Installations.AnyAsync(i => i.SetUpAt != null, cancellation);

    public async Task<(SetupDone? Done, User? FirstUser, SetupRefusal? Refusal)> RunAsync(
        SetupRequest request, CancellationToken cancellation)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        var installation = await db.Installations
            .FromSql($"select * from identity.installation for update")
            .SingleOrDefaultAsync(cancellation);

        if (installation is { IsSetUp: true })
        {
            return (null, null, SetupRefusal.AlreadySetUp);
        }

        if (installation?.SetupCodeHash is not { } hash || !SetupCodes.Matches(request.SetupCode, hash))
        {
            return (null, null, SetupRefusal.WrongSetupCode);
        }

        var now = time.GetUtcNow();
        var user = new User
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            NormalizedEmail = User.Normalize(request.Email),
            CreatedAt = now,
            FirstSignedInAt = now,
        };
        user.PasswordHash = passwords.HashPassword(user, request.Password);
        var workspace = new Workspace { Name = request.WorkspaceName.Trim(), CreatedAt = now };
        var team = new Team { WorkspaceId = workspace.Id, Name = request.TeamName.Trim(), CreatedAt = now };

        db.Users.Add(user);
        db.Workspaces.Add(workspace);
        db.Teams.Add(team);
        db.WorkspaceUsers.Add(new WorkspaceUser { WorkspaceId = workspace.Id, UserId = user.Id, IsAdmin = true, JoinedAt = now });
        db.TeamUsers.Add(new TeamUser { WorkspaceId = workspace.Id, TeamId = team.Id, UserId = user.Id, JoinedAt = now });

        installation.SetUpAt = now;
        installation.SetupCodeHash = null;

        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);

        var done = new SetupDone(
            new SetUpUser(user.Id, user.Name, user.Email),
            new SetUpWorkspace(workspace.Id, workspace.Name),
            new SetUpTeam(team.Id, team.Name));
        return (done, user, null);
    }
}
