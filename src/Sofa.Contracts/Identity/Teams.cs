namespace Sofa.Contracts.Identity;

public sealed record WorkspaceTeam(Guid Id, string Name, IReadOnlyList<TeamMember> Members);

public sealed record TeamMember(Guid UserId, string Name, string? GitHubLogin);

public sealed record WorkspaceMember(Guid UserId, string Name, string? Email, string? GitHubLogin, bool IsAdmin);

public sealed record TeamMemberToAdd(Guid UserId);
