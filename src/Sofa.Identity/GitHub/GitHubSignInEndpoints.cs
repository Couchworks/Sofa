using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Sofa.Contracts.Identity;
using Sofa.Identity.Data;
using Sofa.Identity.SignIn;
using Sofa.Identity.Tokens;

namespace Sofa.Identity.GitHub;

public static class GitHubSignInEndpoints
{
    public const string StartPath = "/account/github/sign-in";
    public const string CallbackPath = "/account/github/callback";
    public const string ProblemQuery = "problem";
    public const string LinkQuery = "github-link";

    public const string DidNotStartHere =
        "That sign-in with GitHub didn't start in this browser, was already used, or took too long. Try again.";

    public static IEndpointRouteBuilder MapGitHubSignIn(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/account/github", async (ConnectionsGitHub connections, CancellationToken cancellation) =>
            new GitHubSignInAvailable(await connections.SignInAppAsync(cancellation) is not null));
        endpoints.MapGet(StartPath, StartSignIn);
        endpoints.MapGet(CallbackPath, Callback);
        endpoints.MapPost("/users/{userId:guid}/github-links", StartLink);
        endpoints.MapPost("/workspaces/{workspaceId:guid}/users", AddByGitHubLogin);
        return endpoints;
    }

    private static async Task<IResult> StartSignIn(
        string? returnUrl, GitHubAuthorizations authorizations, HttpContext http, CancellationToken cancellation)
    {
        var safeReturnUrl = SafeReturnUrl(returnUrl);
        var started = await authorizations.StartAsync(linkingUserId: null, safeReturnUrl, cancellation);
        if (started is null)
        {
            return SignInRefused("Sofa's GitHub App hasn't been created yet, so no one can sign in with GitHub.", safeReturnUrl);
        }

        RememberState(http, started.State);
        return Results.Redirect(started.AuthorizeUrl);
    }

    private static async Task<IResult> StartLink(
        Guid userId, IdentityDb db, GitHubAuthorizations authorizations, CancellationToken cancellation)
    {
        var user = await db.Users.FindAsync([userId], cancellation);
        if (user is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No such User.");
        }

        if (user.GitHubLogin is { } login && user.GitHubAccountId is not null)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"Your GitHub account {login} is already linked.");
        }

        var started = await authorizations.StartAsync(userId, returnUrl: null, cancellation);
        return started is null
            ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Sofa's GitHub App hasn't been created yet.")
            : Results.Ok(new GitHubLinkStarted(started.AuthorizeUrl, started.State));
    }

    private static async Task<IResult> Callback(
        string? code,
        string? state,
        string? error,
        HttpContext http,
        GitHubAuthorizations authorizations,
        ConnectionsGitHub connections,
        GitHubAccountLinks links,
        ILogger<GitHubAuthorization> logger,
        CancellationToken cancellation)
    {
        var remembered = http.Request.Cookies[GitHubSignInState.Cookie];
        http.Response.Cookies.Delete(GitHubSignInState.Cookie, new CookieOptions { Path = GitHubSignInState.CookiePath });
        var started = state is not null && SameState(remembered, state)
            ? await authorizations.TakeOnceAsync(state, cancellation)
            : null;
        if (started is null)
        {
            return SignInRefused(DidNotStartHere, returnUrl: null);
        }

        var linking = started.LinkingUserId;
        var account = error is null && code is not null ? await connections.AccountOfAsync(code, cancellation) : null;
        if (account is null)
        {
            const string gitHubSaidNo = "GitHub didn't sign you in.";
            return linking is null ? SignInRefused(gitHubSaidNo, started.ReturnUrl) : LinkRefused(gitHubSaidNo);
        }

        if (linking is { } userId)
        {
            var refusal = await links.LinkAsync(userId, account, cancellation);
            logger.LogInformation("Linking GitHub account {GitHubLogin} to User {UserId}: {Outcome}",
                account.Login, userId, refusal?.ToString() ?? "linked");
            return refusal switch
            {
                null => Results.Redirect($"/?{LinkQuery}=done"),
                LinkRefusal.UserHasAnotherGitHubAccount => LinkRefused("You already have another GitHub account linked."),
                _ => LinkRefused($"The GitHub account {account.Login} belongs to another User."),
            };
        }

        var user = await links.SignInAsAsync(account, cancellation);
        if (user is null)
        {
            logger.LogInformation("Refused signing in GitHub account {GitHubLogin}, which no User has", account.Login);
            return SignInRefused(
                $"No User has the GitHub account {account.Login}. Ask an Admin of your Workspace to add you by your GitHub login.",
                started.ReturnUrl);
        }

        await http.SignInAsync(SignInCookie.Scheme, SignInCookie.For(user));
        return Results.Redirect(SafeReturnUrl(started.ReturnUrl) ?? "/");
    }

    private static async Task<IResult> AddByGitHubLogin(
        Guid workspaceId, AddUserByGitHubLogin added, GitHubAccountLinks links, CancellationToken cancellation)
    {
        var (user, refusal) = await links.AddByLoginAsync(workspaceId, added.GitHubLogin ?? "", cancellation);
        return refusal switch
        {
            null => Results.Created((string?)null, user),
            AddByLoginRefusal.NotAGitHubLogin => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: $"'{added.GitHubLogin}' is not a GitHub login."),
            AddByLoginRefusal.NoSuchWorkspace => Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "No such Workspace."),
            AddByLoginRefusal.NoSuchPersonOnGitHub => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: $"GitHub has no person with the login {added.GitHubLogin}."),
            AddByLoginRefusal.GitHubDidNotAnswer => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "GitHub didn't say whose that login is. Try again in a while."),
            _ => Results.Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"{added.GitHubLogin} already belongs to this Workspace."),
        };
    }

    private static void RememberState(HttpContext http, string state) =>
        http.Response.Cookies.Append(GitHubSignInState.Cookie, state, new CookieOptions
        {
            Path = GitHubSignInState.CookiePath,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            MaxAge = GitHubSignInState.Lifetime,
        });

    private static bool SameState(string? remembered, string state) =>
        remembered is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(remembered), Encoding.UTF8.GetBytes(state));

    private static string? SafeReturnUrl(string? returnUrl) =>
        returnUrl is not null && returnUrl.StartsWith(ConnectEndpoints.AuthorizationPath + "?", StringComparison.Ordinal)
            ? returnUrl
            : null;

    private static IResult SignInRefused(string problem, string? returnUrl) =>
        Results.Redirect($"{ConnectEndpoints.WebClientSignInPage}?{ProblemQuery}={Uri.EscapeDataString(problem)}"
            + (returnUrl is null ? "" : $"&returnUrl={Uri.EscapeDataString(returnUrl)}"));

    private static IResult LinkRefused(string problem) =>
        Results.Redirect($"/?{LinkQuery}=failed&{ProblemQuery}={Uri.EscapeDataString(problem)}");
}

public sealed record GitHubSignInAvailable(bool Available);
