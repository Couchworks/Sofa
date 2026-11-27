using Sofa.Code.Repositories;
using Sofa.Contracts.Code;
using Sofa.Contracts.Flow;

namespace Sofa.UnitTests.Code;

public class SandboxAccessTests
{
    [Fact]
    public void A_read_only_Run_gets_a_token_that_reads_contents_and_pull_requests_and_writes_nothing()
    {
        var access = SandboxAccess.For(Sandbox.ReadOnly)!;

        Assert.Equal(RepositoryAccess.Read, access.Access);
        Assert.All(access.Permissions.Values, level => Assert.Equal("read", level));
        Assert.Equal(["contents", "metadata", "pull_requests"], access.Permissions.Keys.Order());
    }

    [Fact]
    public void A_Run_on_its_own_branch_gets_a_token_that_writes_contents_and_pull_requests_and_nothing_else()
    {
        var access = SandboxAccess.For(Sandbox.OwnBranch)!;

        Assert.Equal(RepositoryAccess.PushItsOwnBranch, access.Access);
        Assert.Equal(("write", "write", "read"),
            (access.Permissions["contents"], access.Permissions["pull_requests"], access.Permissions["metadata"]));
        Assert.Equal(3, access.Permissions.Count);
    }

    [Fact]
    public void There_is_no_token_for_the_Integration_branch_before_there_are_Integration_branches()
    {
        Assert.Null(SandboxAccess.For(Sandbox.IntegrationBranch));
    }

    [Fact]
    public void A_token_is_asked_for_by_the_Repositories_names_in_their_account()
    {
        Assert.Equal(["web", "api"], SandboxAccess.NamesInTheirAccount(["acme/web", "acme/api"]));
    }

    [Theory]
    [InlineData("sofa/DEMO-1", true)]
    [InlineData("sofa/test-run-0193", true)]
    [InlineData("main", false)]
    [InlineData("sofa/", false)]
    [InlineData("sofa/../main", false)]
    public void A_Runs_own_branch_is_a_branch_under_sofa(string branch, bool fine)
    {
        Assert.Equal(fine, SandboxAccess.ProblemWithTheBranch(branch) is null);
    }

    [Fact]
    public void A_Runs_own_branch_is_named_for_its_Work_Item_so_its_retries_push_the_same_branch()
    {
        var runId = Guid.Parse("01938a5e-0000-7000-8000-000000000001");

        Assert.Equal("sofa/DEMO-1", OwnBranches.Of("DEMO-1", runId));
        Assert.Equal("sofa/test-run-01938a5e000070008000000000000001", OwnBranches.Of(null, runId));
    }
}
