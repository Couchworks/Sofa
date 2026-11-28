using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Sofa.Contracts.Records;

public static partial class SecretScrubbing
{
    public const string Scrubbed = "[scrubbed]";

    private static readonly Regex[] KnownSecrets =
    [
        SofasOwnSecrets(),
        SignedJsonWebTokens(),
        GitHubTokens(),
        GitHubFineGrainedTokens(),
        AnthropicKeysAndOAuthTokens(),
        AwsAccessKeyIds(),
        AwsSecretAccessKeysNamedAsSuch(),
        SlackTokens(),
        PrivateKeyBlocks(),
    ];

    public static string ScrubText(string text)
    {
        var scrubbed = text;
        foreach (var secret in KnownSecrets)
        {
            scrubbed = secret.Replace(scrubbed, Scrubbed);
        }

        return BearerValues().Replace(scrubbed, match => match.Groups["scheme"].Value + Scrubbed);
    }

    public static JsonNode? ScrubJson(JsonNode? node) => node switch
    {
        JsonObject properties => new JsonObject(properties.Select(property =>
            KeyValuePair.Create(ScrubText(property.Key), ScrubJson(property.Value)))),
        JsonArray items => new JsonArray(items.Select(ScrubJson).ToArray()),
        JsonValue value when value.GetValueKind() == JsonValueKind.String => JsonValue.Create(ScrubText(value.GetValue<string>())),
        null => null,
        _ => node.DeepClone(),
    };

    public static string ScrubLine(string line)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(line);
        }
        catch (JsonException)
        {
            return ScrubText(line);
        }

        return parsed is null ? ScrubText(line) : ScrubJson(parsed)!.ToJsonString();
    }

    [GeneratedRegex(@"sofa_[a-z]+(?:_[a-z]+)*_[A-Za-z0-9_-]{20,}")]
    private static partial Regex SofasOwnSecrets();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{8,}\.eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}(?:\.[A-Za-z0-9_-]+){0,2}")]
    private static partial Regex SignedJsonWebTokens();

    [GeneratedRegex(@"gh[pousr]_[A-Za-z0-9]{36,}")]
    private static partial Regex GitHubTokens();

    [GeneratedRegex(@"github_pat_[A-Za-z0-9_]{22,}")]
    private static partial Regex GitHubFineGrainedTokens();

    [GeneratedRegex(@"sk-ant-[A-Za-z0-9_-]{20,}")]
    private static partial Regex AnthropicKeysAndOAuthTokens();

    [GeneratedRegex(@"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b")]
    private static partial Regex AwsAccessKeyIds();

    [GeneratedRegex(@"(?<=aws_secret_access_key[""']?\s*[:=]\s*[""']?)[A-Za-z0-9/+=]{40}", RegexOptions.IgnoreCase)]
    private static partial Regex AwsSecretAccessKeysNamedAsSuch();

    [GeneratedRegex(@"xox[abeoprs]-[A-Za-z0-9-]{10,}")]
    private static partial Regex SlackTokens();

    [GeneratedRegex(@"-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----(?:[\s\S]*?-----END [A-Z0-9 ]*PRIVATE KEY-----|[\s\S]*$)")]
    private static partial Regex PrivateKeyBlocks();

    [GeneratedRegex(@"\b(?<scheme>Bearer\s+)(?=[A-Za-z0-9._~+/-]*\d)[A-Za-z0-9._~+/-]{8,}=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerValues();
}
