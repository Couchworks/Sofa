using System.Text.Json.Serialization;
using Sofa.Contracts;
using Sofa.Contracts.Identity;

namespace Sofa.Execution.Runs;

public sealed class RunTokenClientOptions
{
    public const string Section = "Execution";

    public string? ClientSecret { get; set; }

    public string? ConnectionsSecret { get; set; }
}

public abstract record RunTokenOutcome
{
    public sealed record Issued(string Token, TimeSpan ValidFor) : RunTokenOutcome;

    public sealed record Refused(string Reason) : RunTokenOutcome;
}

public sealed class RunTokenClient(HttpClient identity, Microsoft.Extensions.Options.IOptions<RunTokenClientOptions> options)
{
    public static readonly Uri IdentityAddress = new($"http://{SofaServices.Identity}");

    public async Task<RunTokenOutcome> RequestAsync(Guid runId, Guid agentId, CancellationToken cancellation)
    {
        var secret = options.Value.ClientSecret
            ?? throw new InvalidOperationException(
                $"Execution needs {RunTokenClientOptions.Section}:ClientSecret to ask Identity for Run tokens.");

        using var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = RunTokens.ClientId,
            ["client_secret"] = secret,
            ["scope"] = RunTokens.Scope,
            [RunTokens.RunIdParameter] = runId.ToString(),
            [RunTokens.AgentIdParameter] = agentId.ToString(),
        });
        using var response = await identity.PostAsync("/connect/token", request, cancellation);
        var answer = await response.Content.ReadFromJsonAsync<TokenEndpointAnswer>(cancellation)
            ?? throw new InvalidOperationException("Identity's token endpoint answered with nothing.");

        return answer.AccessToken is { } token
            ? new RunTokenOutcome.Issued(token, TimeSpan.FromSeconds(answer.ExpiresIn ?? 0))
            : new RunTokenOutcome.Refused(answer.ErrorDescription ?? answer.Error ?? $"Identity answered {response.StatusCode}.");
    }

    private sealed record TokenEndpointAnswer(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] long? ExpiresIn,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);
}
