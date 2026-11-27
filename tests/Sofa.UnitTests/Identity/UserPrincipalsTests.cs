using System.Collections.Immutable;
using OpenIddict.Abstractions;
using Sofa.Contracts;
using Sofa.Identity.Tokens;
using Sofa.Identity.Users;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.UnitTests.Identity;

public class UserPrincipalsTests
{
    private static readonly User Ada = new() { Name = "Ada Lovelace", Email = "ada@example.com", NormalizedEmail = "ADA@EXAMPLE.COM" };

    [Fact]
    public void A_Users_token_is_for_the_App_Gateway()
    {
        var principal = UserPrincipals.For(Ada, [Scopes.OpenId]);

        Assert.Equal(Ada.Id.ToString(), principal.GetClaim(Claims.Subject));
        Assert.Equal([SofaServices.AppGateway], principal.GetResources());
    }

    [Fact]
    public void Scopes_Sofa_does_not_grant_are_dropped()
    {
        var principal = UserPrincipals.For(Ada, [Scopes.OpenId, "run", "admin"]);

        Assert.Equal([Scopes.OpenId], principal.GetScopes());
    }

    [Fact]
    public void The_name_and_email_reach_the_identity_token_only_when_their_scopes_are_asked_for()
    {
        var withProfile = UserPrincipals.For(Ada, ImmutableArray.Create(Scopes.OpenId, Scopes.Profile));

        Assert.Contains(Destinations.IdentityToken, withProfile.FindFirst(Claims.Name)!.GetDestinations());
        Assert.DoesNotContain(Destinations.IdentityToken, withProfile.FindFirst(Claims.Email)!.GetDestinations());
        Assert.Contains(Destinations.AccessToken, withProfile.FindFirst(Claims.Email)!.GetDestinations());
    }
}
