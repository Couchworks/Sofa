using Sofa.Code.GitHub;
using Sofa.Code.Pushes;
using Sofa.Code.Repositories;
using Sofa.Contracts.Code;

namespace Sofa.UnitTests.Code;

public class PushRulesTests
{
    private const string Before = "1111111111111111111111111111111111111111";
    private const string After = "2222222222222222222222222222222222222222";
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private static Pushed PushTo(string gitRef, bool created = false, bool deleted = false) =>
        new("acme/demo", gitRef, created ? GitHubRepositories.NoCommit : Before, deleted ? GitHubRepositories.NoCommit : After,
            created, deleted, "sofa-acme[bot]");

    private static RepositoryGrant Grant(RepositoryAccess access, string branch = "sofa/DEMO-1", DateTimeOffset? endedAt = null,
        string repository = "acme/demo", int expiresInMinutes = 60) => new()
    {
        RunId = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        Repositories = [repository],
        Access = access,
        OwnBranch = branch,
        IssuedAt = Now.AddMinutes(-5),
        ExpiresAt = Now.AddMinutes(expiresInMinutes),
        RunEndedAt = endedAt,
    };

    [Fact]
    public void A_push_to_the_own_branch_of_a_running_Run_that_may_push_is_allowed()
    {
        Assert.True(PushRules.Judge(PushTo("refs/heads/sofa/DEMO-1"), [Grant(RepositoryAccess.PushItsOwnBranch)], Now).Allowed);
        Assert.True(PushRules.Judge(PushTo("refs/heads/sofa/DEMO-1", created: true), [Grant(RepositoryAccess.PushItsOwnBranch)], Now).Allowed);
    }

    [Fact]
    public void A_push_to_another_branch_is_caught_naming_every_running_Run_that_could_push_there()
    {
        var pusher = Grant(RepositoryAccess.PushItsOwnBranch);
        var reader = Grant(RepositoryAccess.Read);
        var elsewhere = Grant(RepositoryAccess.PushItsOwnBranch, repository: "acme/other");

        var verdict = PushRules.Judge(PushTo("refs/heads/main"), [pusher, reader, elsewhere], Now);

        Assert.False(verdict.Allowed);
        Assert.Equal([pusher.RunId], verdict.RunsThatCouldHavePushed);
        Assert.Contains("pushed to refs/heads/main in acme/demo", verdict.Why);
    }

    [Fact]
    public void A_branch_is_no_longer_a_Runs_own_once_the_Run_ended_or_its_token_expired()
    {
        Assert.False(PushRules.Judge(PushTo("refs/heads/sofa/DEMO-1"), [Grant(RepositoryAccess.PushItsOwnBranch, endedAt: Now)], Now).Allowed);
        Assert.False(PushRules.Judge(PushTo("refs/heads/sofa/DEMO-1"), [Grant(RepositoryAccess.PushItsOwnBranch, expiresInMinutes: -1)], Now).Allowed);
    }

    [Fact]
    public void A_tag_is_never_a_Runs_own_branch()
    {
        Assert.False(PushRules.Judge(PushTo("refs/tags/sofa/DEMO-1"), [Grant(RepositoryAccess.PushItsOwnBranch)], Now).Allowed);
    }

    [Fact]
    public void Undoing_deletes_a_new_ref_creates_a_deleted_one_again_and_puts_a_moved_one_back()
    {
        Assert.Equal((Undoing.DeleteTheRef, GitHubRepositories.NoCommit),
            (PushRules.UndoingOf(PushTo("refs/heads/x", created: true)), PushRules.PutBackTo(PushTo("refs/heads/x", created: true))));
        Assert.Equal((Undoing.CreateTheRefAgain, Before),
            (PushRules.UndoingOf(PushTo("refs/heads/x", deleted: true)), PushRules.PutBackTo(PushTo("refs/heads/x", deleted: true))));
        Assert.Equal((Undoing.PutTheRefBack, Before), (PushRules.UndoingOf(PushTo("refs/heads/main")), PushRules.PutBackTo(PushTo("refs/heads/main"))));
    }

    [Fact]
    public void The_push_GitHub_sends_for_Codes_own_undoing_is_known_as_such()
    {
        var undone = new UndonePush { Id = Guid.NewGuid(), Repository = "acme/demo", Ref = "refs/heads/main", PutBackTo = Before, UndoneAt = Now };
        var theUndoing = new Pushed("ACME/demo", "refs/heads/main", After, Before, false, false, "sofa-acme[bot]");

        Assert.True(PushRules.IsTheUndoingOf(undone, theUndoing));
        Assert.False(PushRules.IsTheUndoingOf(undone, PushTo("refs/heads/main")));
    }

    [Fact]
    public void Only_pushes_by_Sofas_own_GitHub_App_are_judged()
    {
        Assert.True(PushRules.IsByTheApp(PushTo("refs/heads/main"), "sofa-acme[bot]"));
        Assert.False(PushRules.IsByTheApp(PushTo("refs/heads/main") with { By = "ada-gh" }, "sofa-acme[bot]"));
        Assert.False(PushRules.IsByTheApp(PushTo("refs/heads/main"), null));
    }
}
