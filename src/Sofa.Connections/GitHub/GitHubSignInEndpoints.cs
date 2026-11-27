using Microsoft.Extensions.Options;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.GitHub;

public static class GitHubSignInEndpoints
{
    public static IEndpointRouteBuilder MapGitHubSignIn(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/github-app/sign-in", SignInApp);
        endpoints.MapPost("/github-app/sign-in/accounts", AccountOfCode);
        endpoints.MapGet("/github-accounts/{login}", AccountOfLogin);
        return endpoints;
    }

    private static async Task<IResult> SignInApp(
        GitHubAppAccess access, GitHubAppManifests manifests, CancellationToken cancellation) =>
        await access.FindApp(cancellation) is { } app
            ? Results.Ok(new GitHubSignInApp(
                app.ClientId, manifests.AuthorizeAddress.ToString(), manifests.SignInCallback.ToString()))
            : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Sofa's GitHub App hasn't been created yet.");

    private static async Task<IResult> AccountOfLogin(
        string login, GitHubApi gitHub, ILogger<GitHubAccountOfLogin> logger, CancellationToken cancellation)
    {
        try
        {
            return await gitHub.AccountOfLogin(login, cancellation) is { IsPersonal: true } account
                ? Results.Ok(new GitHubAccountOfLogin(account.Id, account.Login))
                : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"GitHub has no person with the login {login}.");
        }
        catch (GitHubRefused refused)
        {
            logger.LogWarning(refused, "GitHub didn't say whose login {Login} is", login);
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "GitHub didn't say whose that login is. Try again in a while.");
        }
    }

    private static async Task<IResult> AccountOfCode(
        GitHubUserCode userCode,
        GitHubAppAccess access,
        GitHubAppManifests manifests,
        GitHubApi gitHub,
        IOptions<GitHubOptions> options,
        ILogger<GitHubUserCode> logger,
        CancellationToken cancellation)
    {
        if (await access.FindApp(cancellation) is not { } app)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Sofa's GitHub App hasn't been created yet.");
        }

        try
        {
            var token = await gitHub.UserAccessToken(
                options.Value.WebAddress, app.ClientId, await access.ClientSecret(app, cancellation),
                userCode.Code, manifests.SignInCallback, cancellation);
            if (token is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "GitHub didn't accept the sign-in. Try again.");
            }

            var user = await gitHub.User(token, cancellation);
            var email = await gitHub.VerifiedPrimaryEmail(token, cancellation);
            return Results.Ok(new GitHubUserAccount(user.Id, user.Login, user.Name, email));
        }
        catch (GitHubRefused refused)
        {
            logger.LogWarning(refused, "GitHub refused to say whose sign-in code it handed back");
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "GitHub didn't accept the sign-in. Try again.");
        }
    }
}
