using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public sealed partial record FreshInstallation(
    string SetupCode,
    HttpStatusCode WithAMadeUpCode,
    HttpStatusCode WithTheLoggedCode,
    HttpStatusCode WithTheLoggedCodeAgain,
    FirstUser FirstUser)
{
    private static readonly TimeSpan SetupCodeLoggedWithin = TimeSpan.FromMinutes(1);

    public static async Task<FreshInstallation> SetUpAsync(SofaApp sofa)
    {
        var setupCode = await ReadSetupCodeFromIdentitysLogAsync(sofa);
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        var firstUser = new FirstUser("Ada Lovelace", "ada@example.com", "a long enough password", "Ada's Workspace", "Engines");

        using var withAMadeUpCode = await gateway.PostAsJsonAsync("/account/setup", firstUser.SetupWith("sofa_setup_madeup"));
        using var withTheLoggedCode = await gateway.PostAsJsonAsync("/account/setup", firstUser.SetupWith(setupCode));
        var done = await withTheLoggedCode.Content.ReadFromJsonAsync<SetupDone>();
        using var again = await gateway.PostAsJsonAsync("/account/setup", firstUser.SetupWith(setupCode));

        return new FreshInstallation(
            setupCode,
            withAMadeUpCode.StatusCode,
            withTheLoggedCode.StatusCode,
            again.StatusCode,
            firstUser with { Id = done?.User.Id ?? Guid.Empty, WorkspaceId = done?.Workspace.Id ?? Guid.Empty, TeamId = done?.Team.Id ?? Guid.Empty });
    }

    private static async Task<string> ReadSetupCodeFromIdentitysLogAsync(SofaApp sofa)
    {
        using var timeout = new CancellationTokenSource(SetupCodeLoggedWithin);
        var logs = sofa.App.Services.GetRequiredService<ResourceLoggerService>();
        var identity = sofa.App.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.Single(resource => resource.Name == SofaServices.Identity);

        await foreach (var batch in logs.WatchAsync(identity).WithCancellation(timeout.Token))
        {
            foreach (var line in batch)
            {
                if (SetupCodeInLog().Match(line.Content) is { Success: true } match)
                {
                    return match.Value;
                }
            }
        }

        throw new InvalidOperationException("Identity never logged a setup code.");
    }

    [GeneratedRegex("sofa_setup_[A-Za-z0-9_-]+")]
    private static partial Regex SetupCodeInLog();

    private sealed record SetupDone(Created User, Created Workspace, Created Team);

    private sealed record Created(Guid Id);
}

public sealed record FirstUser(string Name, string Email, string Password, string WorkspaceName, string TeamName)
{
    public Guid Id { get; init; }

    public Guid WorkspaceId { get; init; }

    public Guid TeamId { get; init; }

    public object SetupWith(string setupCode) =>
        new { setupCode, Name, Email, Password, WorkspaceName, TeamName };
}
