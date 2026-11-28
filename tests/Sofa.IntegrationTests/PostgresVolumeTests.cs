using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;

namespace Sofa.IntegrationTests;

public class PostgresVolumeTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_integration_tests_keep_their_Postgres_off_every_volume()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sofa_AppHost>(
            ["--Sofa:FreshDatabaseEachStart=true"], Cancellation);

        Assert.Empty(PostgresMounts(builder));
    }

    [Fact]
    public async Task Postgres_keeps_its_data_on_the_configured_volume()
    {
        var volume = $"sofa-test-volume-{Guid.NewGuid():N}";

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sofa_AppHost>(
            [$"--Sofa:PostgresVolume={volume}"], Cancellation);

        var mount = Assert.Single(PostgresMounts(builder));
        Assert.Equal(volume, mount.Source);
        Assert.Equal(ContainerMountType.Volume, mount.Type);
    }

    [Fact]
    public async Task An_app_host_refuses_to_start_on_a_volume_another_container_uses()
    {
        var volume = $"sofa-test-volume-{Guid.NewGuid():N}";
        var holder = $"sofa-test-volume-holder-{Guid.NewGuid():N}";
        await DockerAsync(["run", "--detach", "--rm", "--name", holder, "--volume", $"{volume}:/held", "alpine", "sleep", "300"]);
        try
        {
            var refusal = await Record.ExceptionAsync(() => DistributedApplicationTestingBuilder.CreateAsync<Projects.Sofa_AppHost>(
                [$"--Sofa:PostgresVolume={volume}"], Cancellation));

            var message = MessagesOf(refusal);
            Assert.Contains(volume, message);
            Assert.Contains(holder, message);
        }
        finally
        {
            await DockerAsync(["rm", "--force", holder]);
            await DockerAsync(["volume", "rm", "--force", volume]);
        }
    }

    private static List<ContainerMountAnnotation> PostgresMounts(IDistributedApplicationTestingBuilder builder) =>
        builder.Resources.Single(resource => resource.Name == "postgres")
            .Annotations.OfType<ContainerMountAnnotation>()
            .ToList();

    private static string MessagesOf(Exception? exception)
    {
        Assert.NotNull(exception);
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(Environment.NewLine, messages);
    }

    private static async Task DockerAsync(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var docker = Process.Start(start)!;
        var output = docker.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var errors = docker.StandardError.ReadToEndAsync(CancellationToken.None);
        await docker.WaitForExitAsync(CancellationToken.None);
        if (docker.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker {string.Join(' ', arguments)} failed: {await output}{await errors}");
        }
    }
}
