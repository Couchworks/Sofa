using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Sofa.Contracts.Identity;
using Sofa.Identity.Data;
using Sofa.Identity.SignIn;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.Identity.Tokens;

public static class ConnectEndpoints
{
    public const string AuthorizationPath = "/connect/authorize";
    public const string TokenPath = "/connect/token";
    public const string EndSessionPath = "/connect/endsession";
    public const string RevocationPath = "/connect/revoke";
    public const string WebClientSignInPage = "/sign-in";

    private const string ServerScheme = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;

    public static IEndpointRouteBuilder MapConnect(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(AuthorizationPath, Authorize);
        endpoints.MapPost(TokenPath, IssueToken);
        endpoints.MapMethods(EndSessionPath, [HttpMethods.Get, HttpMethods.Post], (Delegate)EndSession);
        return endpoints;
    }

    private static async Task<IResult> Authorize(HttpContext http, IdentityDb db, CancellationToken cancellation)
    {
        var request = http.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict did not pass an authorization request on.");

        var signedIn = await http.AuthenticateAsync(SignInCookie.Scheme);
        var user = SignInCookie.UserIdIn(signedIn.Principal) is { } userId
            ? await db.Users.FindAsync([userId], cancellation)
            : null;

        if (user is null)
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Refuse(Errors.LoginRequired, "No User is signed in.");
            }

            var returnUrl = http.Request.PathBase + http.Request.Path + http.Request.QueryString;
            return Results.Redirect($"{WebClientSignInPage}?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        return Results.SignIn(UserPrincipals.For(user, request.GetScopes()), authenticationScheme: ServerScheme);
    }

    private static async Task<IResult> IssueToken(HttpContext http, IdentityDb db, CancellationToken cancellation)
    {
        var request = http.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict did not pass a token request on.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var granted = await http.AuthenticateAsync(ServerScheme);
            var user = SignInCookie.UserIdIn(granted.Principal) is { } userId
                ? await db.Users.FindAsync([userId], cancellation)
                : null;
            return user is null
                ? Refuse(Errors.InvalidGrant, "That User no longer exists.")
                : Results.SignIn(UserPrincipals.For(user, granted.Principal!.GetScopes()), authenticationScheme: ServerScheme);
        }

        if (request.IsClientCredentialsGrantType())
        {
            if (request.ClientId != RunTokens.ClientId)
            {
                return Refuse(Errors.UnauthorizedClient, "Only Execution is issued Run tokens.");
            }

            if (RunTokenGrant.Read(request) is not { } runToken)
            {
                return Refuse(Errors.InvalidRequest,
                    $"A Run token needs the Run's id as {RunTokens.RunIdParameter} and its Agent's as {RunTokens.AgentIdParameter}.");
            }

            var agent = await db.Agents.FindAsync([runToken.AgentId], cancellation);
            return agent is null
                ? Refuse(Errors.InvalidGrant, $"Identity knows no Agent {runToken.AgentId}.")
                : Results.SignIn(RunTokenGrant.For(runToken.RunId, agent), authenticationScheme: ServerScheme);
        }

        return Refuse(Errors.UnsupportedGrantType, "That grant type is not supported.");
    }

    private static async Task<IResult> EndSession(HttpContext http)
    {
        await http.SignOutAsync(SignInCookie.Scheme);
        return Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, [ServerScheme]);
    }

    private static IResult Refuse(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [ServerScheme]);
}
