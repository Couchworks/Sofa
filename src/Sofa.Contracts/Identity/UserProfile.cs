namespace Sofa.Contracts.Identity;

public sealed record UserProfile(Guid Id, string Name, string Email, IReadOnlyList<UserWorkspace> Workspaces);

public sealed record UserWorkspace(Guid Id, string Name, bool IsAdmin, IReadOnlyList<UserTeam> Teams);

public sealed record UserTeam(Guid Id, string Name);
