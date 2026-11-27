using Sofa.Code.GitHub;
using Sofa.Code.Repositories;
using Sofa.Contracts.Code;

namespace Sofa.Code.Pushes;

public enum Undoing
{
    DeleteTheRef,
    CreateTheRefAgain,
    PutTheRefBack,
}

public sealed record PushVerdict(bool Allowed, IReadOnlyList<Guid> RunsThatCouldHavePushed, string? Why)
{
    public static PushVerdict Allow { get; } = new(true, [], null);
}

public static class PushRules
{
    public const string Branches = "refs/heads/";

    public static bool IsByTheApp(Pushed push, string? appBot) =>
        appBot is not null && string.Equals(push.By, appBot, StringComparison.OrdinalIgnoreCase);

    public static bool IsLive(RepositoryGrant grant, DateTimeOffset now) => grant.RunEndedAt is null && grant.ExpiresAt > now;

    public static PushVerdict Judge(Pushed push, IEnumerable<RepositoryGrant> grants, DateTimeOffset now)
    {
        var inTheRepository = grants
            .Where(grant => IsLive(grant, now) && grant.Repositories.Contains(push.Repository, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var pushers = inTheRepository.Where(grant => grant.Access == RepositoryAccess.PushItsOwnBranch).ToList();
        if (pushers.Any(grant => push.Ref == Branches + grant.OwnBranch))
        {
            return PushVerdict.Allow;
        }

        var what = push.Deleted ? "deleted" : push.Created ? "created" : "pushed to";
        return new PushVerdict(false, pushers.Select(grant => grant.RunId).ToList(),
            $"Sofa's GitHub App {what} {push.Ref} in {push.Repository}, which is no running Run's own branch.");
    }

    public static Undoing UndoingOf(Pushed push) =>
        push.Created || push.Before == GitHubRepositories.NoCommit ? Undoing.DeleteTheRef
        : push.Deleted || push.After == GitHubRepositories.NoCommit ? Undoing.CreateTheRefAgain
        : Undoing.PutTheRefBack;

    public static string PutBackTo(Pushed push) =>
        UndoingOf(push) == Undoing.DeleteTheRef ? GitHubRepositories.NoCommit : push.Before;

    public static bool IsTheUndoingOf(UndonePush undone, Pushed push) =>
        string.Equals(undone.Repository, push.Repository, StringComparison.OrdinalIgnoreCase)
        && undone.Ref == push.Ref
        && undone.PutBackTo == push.After;
}
