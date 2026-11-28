using Sofa.Contracts.Code;

namespace Sofa.Code.Repositories;

public sealed class RepositoryGrant
{
    public required Guid RunId { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid ProjectId { get; init; }

    public required List<string> Repositories { get; init; }

    public required RepositoryAccess Access { get; init; }

    public required string OwnBranch { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RunEndedAt { get; set; }
}
