using OpenIddict.Abstractions;
using Sofa.Contracts.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.Identity.Tokens;

public sealed class ClientOptions
{
    public const string Section = "Identity";

    public Uri? Issuer { get; set; }

    public List<Uri> WebClientOrigins { get; set; } = [];

    public string? ExecutionClientSecret { get; set; }
}

public static class Clients
{
    public const string WebClientId = "sofa-web";
    public const string WebClientSignedInPath = "/signed-in";

    public static async Task RegisterAsync(
        IOpenIddictApplicationManager applications, ClientOptions options, CancellationToken cancellation)
    {
        await RegisterOrUpdateAsync(applications, WebClient(options.WebClientOrigins), cancellation);

        if (!string.IsNullOrEmpty(options.ExecutionClientSecret))
        {
            await RegisterOrUpdateAsync(applications, Execution(options.ExecutionClientSecret), cancellation);
        }
    }

    public static OpenIddictApplicationDescriptor WebClient(IEnumerable<Uri> origins)
    {
        var client = new OpenIddictApplicationDescriptor
        {
            ClientId = WebClientId,
            DisplayName = "Sofa's web client",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Email,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        foreach (var origin in origins)
        {
            client.RedirectUris.Add(new Uri(origin, WebClientSignedInPath));
            client.PostLogoutRedirectUris.Add(new Uri(origin, "/"));
        }

        return client;
    }

    public static OpenIddictApplicationDescriptor Execution(string secret) => new()
    {
        ClientId = RunTokens.ClientId,
        ClientSecret = secret,
        DisplayName = "Execution, asking for Run tokens",
        ClientType = ClientTypes.Confidential,
        Permissions =
        {
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.ClientCredentials,
            Permissions.Prefixes.Scope + RunTokens.Scope,
        },
    };

    private static async Task RegisterOrUpdateAsync(
        IOpenIddictApplicationManager applications, OpenIddictApplicationDescriptor descriptor, CancellationToken cancellation)
    {
        var existing = await applications.FindByClientIdAsync(descriptor.ClientId!, cancellation);
        if (existing is null)
        {
            await applications.CreateAsync(descriptor, cancellation);
        }
        else
        {
            await applications.UpdateAsync(existing, descriptor, cancellation);
        }
    }
}
