using Sofa.Contracts.Identity;
using Sofa.Identity.Tokens;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.UnitTests.Identity;

public class ClientsTests
{
    [Fact]
    public void The_web_client_is_public_and_must_use_PKCE()
    {
        var client = Clients.WebClient([]);

        Assert.Equal(ClientTypes.Public, client.ClientType);
        Assert.Contains(Requirements.Features.ProofKeyForCodeExchange, client.Requirements);
        Assert.Contains(Permissions.GrantTypes.AuthorizationCode, client.Permissions);
        Assert.DoesNotContain(Permissions.GrantTypes.ClientCredentials, client.Permissions);
    }

    [Fact]
    public void The_web_client_comes_back_to_each_of_Sofas_public_origins()
    {
        var client = Clients.WebClient([new Uri("http://localhost:5176"), new Uri("https://sofa.example.com")]);

        Assert.Equal(
            [new Uri("http://localhost:5176/signed-in"), new Uri("https://sofa.example.com/signed-in")],
            client.RedirectUris.OrderBy(uri => uri.ToString()));
        Assert.Contains(new Uri("https://sofa.example.com/"), client.PostLogoutRedirectUris);
    }

    [Fact]
    public void Execution_may_only_ask_for_Run_tokens()
    {
        var client = Clients.Execution("secret");

        Assert.Equal(ClientTypes.Confidential, client.ClientType);
        Assert.Equal(
            [Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials, Permissions.Prefixes.Scope + RunTokens.Scope],
            client.Permissions.Order());
    }
}
