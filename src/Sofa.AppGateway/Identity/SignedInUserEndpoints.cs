using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Sofa.Contracts.Identity;

namespace Sofa.AppGateway.Identity;

public static class SignedInUserEndpoints
{
    public static RouteGroupBuilder MapSignedInUser(this RouteGroupBuilder api)
    {
        api.MapGet("/me", async (ClaimsPrincipal principal, IdentityUsers users, CancellationToken cancellation) =>
                SignedInUser.IdOf(principal) is { } userId
                && await users.ProfileOfAsync(userId, cancellation) is { } profile
                    ? Results.Ok(profile)
                    : Results.NotFound())
            .RequireAuthorization()
            .WithName("GetSignedInUser")
            .WithSummary("The signed-in User, with their Workspaces and Teams.")
            .Produces<UserProfile>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        api.MapPost("/me/github-link", StartGitHubLink)
            .RequireAuthorization()
            .WithName("StartGitHubLink")
            .WithSummary("Where to send the browser to link the signed-in User's GitHub account, through GitHub.")
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }

    private static async Task<Results<Ok<GitHubLink>, ProblemHttpResult, NotFound>> StartGitHubLink(
        ClaimsPrincipal principal, IdentityUsers users, HttpContext http, CancellationToken cancellation)
    {
        if (SignedInUser.IdOf(principal) is not { } userId)
        {
            return TypedResults.NotFound();
        }

        var started = await users.StartGitHubLinkAsync(userId, cancellation);
        if (started.Value is not { } link)
        {
            return TypedResults.Problem(statusCode: started.Status, title: started.Problem);
        }

        http.Response.Cookies.Append(GitHubSignInState.Cookie, link.State, new CookieOptions
        {
            Path = GitHubSignInState.CookiePath,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            MaxAge = GitHubSignInState.Lifetime,
        });
        return TypedResults.Ok(new GitHubLink(link.AuthorizeUrl));
    }
}

public sealed record GitHubLink(string AuthorizeUrl);
