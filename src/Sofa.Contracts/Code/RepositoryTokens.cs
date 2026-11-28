using System.Text.Json.Serialization;
using Sofa.Contracts.Flow;

namespace Sofa.Contracts.Code;

public sealed record RepositoryTokenRequest(Guid WorkspaceId, Guid ProjectId, Sandbox Sandbox, string OwnBranch);

public sealed record RepositoryToken(
    string Token, DateTimeOffset ExpiresAt, IReadOnlyList<string> Repositories, RepositoryAccess Access, string? PushesTo);

[JsonConverter(typeof(JsonStringEnumConverter<RepositoryAccess>))]
public enum RepositoryAccess
{
    Read,
    PushItsOwnBranch,
}

public static class OwnBranches
{
    public const string Prefix = "sofa/";

    public static string Of(string? workItemKey, Guid runId) =>
        workItemKey is { Length: > 0 } key ? $"{Prefix}{key}" : $"{Prefix}test-run-{runId:N}";
}
