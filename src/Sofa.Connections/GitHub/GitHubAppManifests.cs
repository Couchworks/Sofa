using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.GitHub;

public sealed partial class GitHubAppManifests(IOptions<GitHubOptions> github, PublicAddress publicAddress)
{
    public const int LongestAppName = 34;

    private static readonly string[] EventsSofaReads = ["issues", "issue_comment", "pull_request", "push"];

    public static bool IsAccountLogin(string login) => AccountLogin().IsMatch(login);

    public Uri FormAction(string state, string? organisation)
    {
        var newAppPage = organisation is null
            ? "settings/apps/new"
            : $"organizations/{organisation}/settings/apps/new";
        return new Uri(github.Value.WebAddress, $"{newAppPage}?state={Uri.EscapeDataString(state)}");
    }

    public string Manifest(string? organisation, string? name)
    {
        var manifest = new JsonObject
        {
            ["name"] = AppName(organisation, name),
            ["url"] = publicAddress.Address.ToString(),
            ["hook_attributes"] = new JsonObject
            {
                ["url"] = publicAddress.Resolve(GitHubAppPaths.Webhooks).ToString(),
                ["active"] = publicAddress.GitHubCanReachIt,
            },
            ["redirect_url"] = publicAddress.Resolve(GitHubAppPaths.Created).ToString(),
            ["setup_url"] = publicAddress.Resolve(GitHubAppPaths.Installed).ToString(),
            ["setup_on_update"] = true,
            ["public"] = false,
            ["default_permissions"] = new JsonObject
            {
                ["metadata"] = "read",
                ["issues"] = "write",
                ["contents"] = "write",
                ["pull_requests"] = "write",
            },
            ["default_events"] = new JsonArray(EventsSofaReads.Select(e => (JsonNode)e).ToArray()),
        };
        return manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private string AppName(string? organisation, string? name)
    {
        var chosen = string.IsNullOrWhiteSpace(name)
            ? $"Sofa {organisation ?? publicAddress.Address.Host}"
            : name.Trim();
        return chosen.Length <= LongestAppName ? chosen : chosen[..LongestAppName];
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$")]
    private static partial Regex AccountLogin();
}
