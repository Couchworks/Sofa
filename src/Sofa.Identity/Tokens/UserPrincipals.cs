using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Sofa.Contracts;
using Sofa.Identity.Users;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.Identity.Tokens;

public static class UserPrincipals
{
    public const string Audience = SofaServices.AppGateway;

    public static readonly ImmutableArray<string> Scopes =
        [OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.OfflineAccess];

    public static ClaimsPrincipal For(User user, ImmutableArray<string> requestedScopes)
    {
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Name, user.Name)
            .SetClaim(Claims.Email, user.Email);

        var scopes = requestedScopes.Intersect(Scopes).ToImmutableArray();
        identity.SetScopes(scopes);
        identity.SetResources(Audience);
        identity.SetDestinations(claim => DestinationsOf(claim, scopes));

        return new ClaimsPrincipal(identity);
    }

    private static IEnumerable<string> DestinationsOf(Claim claim, ImmutableArray<string> scopes)
    {
        yield return Destinations.AccessToken;

        var shownInIdentityToken = claim.Type switch
        {
            Claims.Name => scopes.Contains(OpenIddictConstants.Scopes.Profile),
            Claims.Email => scopes.Contains(OpenIddictConstants.Scopes.Email),
            _ => false,
        };
        if (shownInIdentityToken)
        {
            yield return Destinations.IdentityToken;
        }
    }
}
