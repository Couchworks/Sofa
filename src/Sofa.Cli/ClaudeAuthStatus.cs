using System.Text.Json;
using Sofa.Contracts.Execution;

namespace Sofa.Cli;

public static class ClaudeAuthStatus
{
    public const string SubscriptionSignIn = "claude.ai";

    public static SignInSeen? Read(string statusJson)
    {
        JsonElement status;
        try
        {
            status = JsonDocument.Parse(statusJson).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }

        if (status.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var loggedIn = status.TryGetProperty("loggedIn", out var logged) && logged.ValueKind == JsonValueKind.True;
        var onASubscription = status.TryGetProperty("authMethod", out var method)
            && method.ValueKind == JsonValueKind.String
            && method.GetString() == SubscriptionSignIn;
        var endsAt = status.TryGetProperty("loginExpiresAt", out var expires)
            && expires.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(expires.GetString(), out var parsed)
                ? parsed
                : (DateTimeOffset?)null;

        return loggedIn && onASubscription ? new SignInSeen(true, endsAt) : new SignInSeen(false, null);
    }
}
