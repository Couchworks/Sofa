using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Sofa.Execution.Workers;

public sealed class DockerWorkerNetwork(IOptions<WorkerOptions> options, ILogger<DockerWorkerNetwork> logger) : IWorkerNetwork
{
    public const string RelayLabel = "sofa.egress-relay";

    private readonly SemaphoreSlim _placing = new(1, 1);

    private string Workers => options.Value.Network;

    private string Egress => $"{Workers}-egress";

    private string Relay => $"{Workers}-relay";

    public async Task<WorkerPlacement> PlaceAsync(CancellationToken cancellation)
    {
        var agentGateway = new Uri(options.Value.AgentGatewayAddress
            ?? throw new InvalidOperationException($"Execution needs {WorkerOptions.Section}:AgentGatewayAddress, where the egress relay reaches the Agent Gateway."));
        var hosts = WhatWorkersReach.Hosts;
        var configuration = EgressRelay.Configuration(hosts, agentGateway);
        var fingerprint = EgressRelay.FingerprintOf(configuration);

        await _placing.WaitAsync(cancellation);
        try
        {
            await EnsureNetworkAsync(Workers, internalOnly: true, cancellation);
            await EnsureNetworkAsync(Egress, internalOnly: false, cancellation);
            await EnsureRelayAsync(configuration, fingerprint, cancellation);
            var address = (await DockerAsync(
                ["inspect", "--format", $"{{{{(index .NetworkSettings.Networks \"{Workers}\").IPAddress}}}}", Relay], cancellation)).Output.Trim();
            if (string.IsNullOrEmpty(address))
            {
                throw new InvalidOperationException($"The egress relay {Relay} has no address on {Workers}.");
            }

            return EgressRelay.PlacementBehind(Workers, address, hosts);
        }
        finally
        {
            _placing.Release();
        }
    }

    public async Task RemoveAsync(CancellationToken cancellation)
    {
        await DockerAsync(["rm", "--force", Relay], cancellation);
        await DockerAsync(["network", "rm", Egress], cancellation);
        await DockerAsync(["network", "rm", Workers], cancellation);
    }

    private async Task EnsureNetworkAsync(string name, bool internalOnly, CancellationToken cancellation)
    {
        if ((await DockerAsync(["network", "inspect", name], cancellation)).ExitCode == 0)
        {
            return;
        }

        List<string> create = ["network", "create", "--label", $"{RelayLabel}={Workers}"];
        if (internalOnly)
        {
            create.Add("--internal");
        }

        create.Add(name);
        var created = await DockerAsync(create, cancellation);
        if (created.ExitCode != 0 && (await DockerAsync(["network", "inspect", name], cancellation)).ExitCode != 0)
        {
            throw new InvalidOperationException($"Docker couldn't create the network {name}: {created.Output}");
        }
    }

    private async Task EnsureRelayAsync(string configuration, string fingerprint, CancellationToken cancellation)
    {
        var seen = await DockerAsync(
            ["inspect", "--format", $"{{{{index .Config.Labels \"{RelayLabel}\"}}}} {{{{.State.Running}}}}", Relay], cancellation);
        if (seen.ExitCode == 0 && seen.Output.Trim() == $"{fingerprint} true")
        {
            return;
        }

        await DockerAsync(["rm", "--force", Relay], cancellation);
        var started = await DockerAsync(
        [
            "run", "--detach", "--name", Relay, "--restart", "unless-stopped",
            "--network", Egress,
            $"--add-host={EgressRelay.BoxAsTheRelaySeesIt}:host-gateway",
            "--label", $"{RelayLabel}={fingerprint}",
            "--env", "SOFA_RELAY_CONFIGURATION",
            "--entrypoint", "sh",
            options.Value.RelayImage,
            "-c", "printf '%s\\n' \"$SOFA_RELAY_CONFIGURATION\" > /etc/nginx/nginx.conf && exec nginx -g 'daemon off;'",
        ], cancellation, new Dictionary<string, string> { ["SOFA_RELAY_CONFIGURATION"] = configuration });
        if (started.ExitCode != 0)
        {
            throw new InvalidOperationException($"Docker couldn't start the egress relay {Relay}: {started.Output}");
        }

        var connected = await DockerAsync(["network", "connect", Workers, Relay], cancellation);
        if (connected.ExitCode != 0)
        {
            throw new InvalidOperationException($"Docker couldn't put the egress relay on {Workers}: {connected.Output}");
        }

        logger.LogInformation("Started the egress relay {Relay} between {Workers} and {Egress} ({Fingerprint})", Relay, Workers, Egress, fingerprint);
    }

    private static async Task<(int ExitCode, string Output)> DockerAsync(
        IReadOnlyList<string> arguments, CancellationToken cancellation, IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        using var docker = Process.Start(start) ?? throw new InvalidOperationException("Docker didn't start.");
        var output = docker.StandardOutput.ReadToEndAsync(cancellation);
        var errors = docker.StandardError.ReadToEndAsync(cancellation);
        await docker.WaitForExitAsync(cancellation);
        return (docker.ExitCode, await output + await errors);
    }
}
