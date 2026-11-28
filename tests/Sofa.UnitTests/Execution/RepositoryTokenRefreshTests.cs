using Sofa.Execution.Runs;
using Sofa.Execution.Workers;

namespace Sofa.UnitTests.Execution;

public class RepositoryTokenRefreshTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_Runs_token_is_refreshed_the_set_time_before_it_expires()
    {
        Assert.Equal(TimeSpan.FromMinutes(50), RepositoryTokenRefresh.DueIn(Now.AddHours(1), Now, TimeSpan.FromMinutes(10)));
        Assert.Equal(TimeSpan.Zero, RepositoryTokenRefresh.DueIn(Now.AddMinutes(5), Now, TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void A_refresh_that_failed_is_tried_again_shortly() =>
        Assert.Equal(RepositoryTokenRefresh.TryingAgainAfter,
            RepositoryTokenRefresh.DueIn(RepositoryTokenRefresh.TryAgainAt(Now, TimeSpan.FromMinutes(10)), Now, TimeSpan.FromMinutes(10)));

    [Fact]
    public void The_fresh_token_reaches_the_container_by_name_through_docker_exec_never_on_its_command_line()
    {
        var arguments = DockerWorkerContainers.ExecArguments("sofa-run-1", WorkerCommand.ReplacingTheRepositoryToken(),
            new Dictionary<string, string> { [WorkerCommand.FreshRepositoryToken] = "ghs_fresh" });

        Assert.Equal(["exec", "--env", WorkerCommand.FreshRepositoryToken, "sofa-run-1", "sh", "-c"], arguments.Take(6));
        Assert.DoesNotContain(arguments, argument => argument.Contains("ghs_fresh", StringComparison.Ordinal));
        Assert.Contains($"> {WorkerCommand.RepositoryTokenFile}.next && mv -f", arguments[^1]);
        Assert.Contains($"\"${WorkerCommand.FreshRepositoryToken}\"", arguments[^1]);
    }
}
