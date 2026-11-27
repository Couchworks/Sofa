using System.Security.Cryptography;
using System.Text;
using Sofa.Connections.Credentials;
using Sofa.Contracts.Flow;

namespace Sofa.Connections.ApiKeys;

public static class ApiKeyRules
{
    public const string AnthropicKeyPrefix = "sk-ant-";
    public const int Longest = 512;

    public static string? ProblemWith(Vendor vendor, string? apiKey)
    {
        var key = apiKey?.Trim() ?? "";
        if (key.Length == 0)
        {
            return "Enter the API key.";
        }

        if (key.Length > Longest || key.Any(char.IsWhiteSpace))
        {
            return "That doesn't look like an API key.";
        }

        return vendor switch
        {
            Vendor.Anthropic when !key.StartsWith(AnthropicKeyPrefix, StringComparison.Ordinal) =>
                $"An Anthropic API key starts with {AnthropicKeyPrefix}.",
            Vendor.Anthropic => null,
            _ => "Sofa doesn't know that vendor.",
        };
    }

    public static CredentialPurpose PurposeOf(Guid apiKeyId) => new($"api-key:{apiKeyId}");

    public static bool IsExecution(string? secretSent, string? executionsSecret) =>
        !string.IsNullOrEmpty(executionsSecret)
        && secretSent is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(secretSent), Encoding.UTF8.GetBytes(executionsSecret));
}
