using System.Text.RegularExpressions;
using Sofa.Contracts.Flow;

namespace Sofa.Flow.Projects;

public static partial class ProjectRules
{
    public const int LongestKey = 10;
    public const int LongestName = 100;
    public const int LongestLabel = 50;
    public const int LongestRepository = 140;
    public const int LongestInstructions = 20_000;

    public static string NormalizeKey(string? key) => (key ?? "").Trim().ToUpperInvariant();

    public static string? KeyProblem(string normalizedKey) =>
        Key().IsMatch(normalizedKey)
            ? null
            : $"A Project's key is 2 to {LongestKey} letters and digits, starting with a letter, such as DEMO.";

    public static string? NameProblem(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "A Project needs a name."
        : name.Trim().Length > LongestName ? $"A Project's name has at most {LongestName} characters."
        : null;

    public static string? BindingShapeProblem(RequestProviderBinding? requests, CodeProviderBinding? code)
    {
        if (requests is null || code is null)
        {
            return "A Project is bound to a Request Provider and a Code Provider.";
        }

        if (string.IsNullOrWhiteSpace(requests.Label) || requests.Label.Trim().Length > LongestLabel)
        {
            return $"The label that marks issues for Sofa has 1 to {LongestLabel} characters.";
        }

        if (!IsRepository(requests.Repository))
        {
            return "Requests come from one Repository, named as owner/name.";
        }

        if (code.Repositories is not { Count: > 0 } || !code.Repositories.All(IsRepository))
        {
            return "Code lives in one or more Repositories, each named as owner/name.";
        }

        return code.Repositories.Distinct(StringComparer.OrdinalIgnoreCase).Count() == code.Repositories.Count
            ? null
            : "Each Repository of the Code Provider is named once.";
    }

    public static (RequestProviderBinding? Requests, CodeProviderBinding? Code, string? Problem) AsTheConnectionsNameThem(
        RequestProviderBinding requests,
        CodeProviderBinding code,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> repositoriesEachConnectionReaches)
    {
        string? Reached(Guid connectionId, string repository) =>
            repositoriesEachConnectionReaches.TryGetValue(connectionId, out var reached)
                ? reached.FirstOrDefault(name => name.Equals(repository.Trim(), StringComparison.OrdinalIgnoreCase))
                : null;

        if (Reached(requests.ConnectionId, requests.Repository) is not { } requestRepository)
        {
            return (null, null, $"The Connection doesn't reach {requests.Repository.Trim()}.");
        }

        var codeRepositories = new List<string>();
        foreach (var repository in code.Repositories)
        {
            if (Reached(code.ConnectionId, repository) is not { } reached)
            {
                return (null, null, $"The Connection doesn't reach {repository.Trim()}.");
            }

            codeRepositories.Add(reached);
        }

        return (requests with { Repository = requestRepository, Label = requests.Label.Trim() },
            code with { Repositories = codeRepositories },
            null);
    }

    public static string? AgentProblem(string? instructions, string? model) =>
        instructions is null || instructions.Length > LongestInstructions
            ? $"An Agent's instructions have at most {LongestInstructions} characters."
            : AgentModels.Problem(model);

    public static string? InstructionsProblem(string instructions) =>
        instructions.Length > LongestInstructions ? $"A Phase's instructions have at most {LongestInstructions} characters." : null;

    private static bool IsRepository(string? repository) =>
        repository is not null && repository.Trim().Length <= LongestRepository && Repository().IsMatch(repository.Trim());

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$")]
    private static partial Regex Key();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9._-]+$")]
    private static partial Regex Repository();
}

public static partial class AgentModels
{
    public const string Opus = "opus";
    public const string Sonnet = "sonnet";
    public const string Haiku = "haiku";

    public static string? Problem(string? model) =>
        model is not null && Model().IsMatch(model)
            ? null
            : "A model is opus, sonnet or haiku, or a full model name such as claude-opus-5-5.";

    [GeneratedRegex("^(opus|sonnet|haiku|claude-[a-z0-9]+(-[a-z0-9]+)*)$")]
    private static partial Regex Model();
}
