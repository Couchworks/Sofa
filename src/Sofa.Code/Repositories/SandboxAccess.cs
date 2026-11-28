using Sofa.Contracts.Code;
using Sofa.Contracts.Flow;

namespace Sofa.Code.Repositories;

public sealed record AccessForASandbox(RepositoryAccess Access, IReadOnlyDictionary<string, string> Permissions);

public static class SandboxAccess
{
    public static AccessForASandbox? For(Sandbox sandbox) => sandbox switch
    {
        Sandbox.ReadOnly => new(RepositoryAccess.Read, new Dictionary<string, string>
        {
            ["metadata"] = "read",
            ["contents"] = "read",
            ["pull_requests"] = "read",
        }),
        Sandbox.OwnBranch => new(RepositoryAccess.PushItsOwnBranch, new Dictionary<string, string>
        {
            ["metadata"] = "read",
            ["contents"] = "write",
            ["pull_requests"] = "write",
        }),
        _ => null,
    };

    public static IReadOnlyList<string> NamesInTheirAccount(IEnumerable<string> repositories) =>
        repositories.Select(repository => repository[(repository.IndexOf('/') + 1)..]).ToList();

    public static string? ProblemWithTheBranch(string ownBranch) =>
        ownBranch.StartsWith(OwnBranches.Prefix, StringComparison.Ordinal) && ownBranch.Length > OwnBranches.Prefix.Length
            && !ownBranch.Contains("..", StringComparison.Ordinal) && !ownBranch.Any(char.IsWhiteSpace)
            ? null
            : $"A Run's own branch is a branch under {OwnBranches.Prefix}, not '{ownBranch}'.";
}
