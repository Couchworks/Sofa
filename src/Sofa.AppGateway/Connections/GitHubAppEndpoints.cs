using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Sofa.AppGateway.Admins;
using Sofa.AppGateway.GitHubReturns;
using Sofa.Contracts.Connections;
using Wolverine;

namespace Sofa.AppGateway.Connections;

public static class GitHubAppEndpoints
{
    public static readonly TimeSpan ConnectionsAnswersWithin = TimeSpan.FromSeconds(30);

    public const string GitHubAppQuery = "github-app";
    public const string ConnectionQuery = "connection";
    public const string ProblemQuery = "problem";
    public const string UpdatedOnGitHub = "updated";
    public const string GitHubChangedAnInstallationOnItsOwn = "update";

    public const string ReturnSofaDidNotStart =
        "GitHub came back to Sofa with a return Sofa didn't start in this browser, one already used, or one that has expired. " +
        "Start again from Sofa.";

    public const string NoLongerAnAdmin = "Only an Admin of the Workspace may finish this, and whoever started it no longer is one.";

    public static RouteGroupBuilder MapGitHubAppApi(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/github-app", (ConnectionsClient connections, CancellationToken cancellation) =>
                connections.GitHubApp(cancellation))
            .WithName("GetGitHubApp")
            .WithSummary("Whether Sofa's GitHub App has been created.");

        workspace.MapPost("/github-app/creations", StartCreation)
            .WithName("StartGitHubAppCreation")
            .WithSummary("The form that sends the GitHub App's manifest to GitHub, to create the App there.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        workspace.MapPost("/github-app/installations", StartInstallation)
            .WithName("StartGitHubAppInstallation")
            .WithSummary("Where to send the browser to install Sofa's GitHub App on an organisation or account.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        workspace.MapGet("/connections", (SignedInAdmin admin, ConnectionsClient connections, CancellationToken cancellation) =>
                connections.ConnectionsOf(admin.WorkspaceId, cancellation))
            .WithName("GetConnections")
            .WithSummary("The Workspace's Connections to outside systems.");

        return workspace;
    }

    public static IEndpointRouteBuilder MapGitHubAppReturns(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(GitHubAppPaths.Created, FinishCreation).ExcludeFromDescription();
        endpoints.MapGet(GitHubAppPaths.Installed, AddInstallation).ExcludeFromDescription();
        return endpoints;
    }

    private static async Task<Results<Ok<GitHubAppCreationForm>, ProblemHttpResult>> StartCreation(
        NewGitHubApp app,
        SignedInAdmin admin,
        GitHubReturnStates states,
        HttpContext http,
        IMessageBus bus,
        CancellationToken cancellation)
    {
        var (state, started) = states.Mint(GitHubReturnTo.Created, admin);
        var answer = await bus.InvokeAsync<GitHubAppCreationStarted>(
            new StartGitHubAppCreation(admin.WorkspaceId, app.Organisation, app.Name, state), cancellation, ConnectionsAnswersWithin);
        if (answer is not { FormAction: { } formAction, Manifest: { } manifest })
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: answer.Problem);
        }

        GitHubReturnCookie.Remember(http, started, states.Lifetime);
        return TypedResults.Ok(new GitHubAppCreationForm(formAction, manifest));
    }

    private static async Task<Results<Ok<GitHubAppInstallation>, ProblemHttpResult>> StartInstallation(
        SignedInAdmin admin,
        GitHubReturnStates states,
        ConnectionsClient connections,
        HttpContext http,
        CancellationToken cancellation)
    {
        var app = await connections.GitHubApp(cancellation);
        if (app.InstallUrl is not { } installUrl)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Sofa's GitHub App hasn't been created yet.");
        }

        var (state, started) = states.Mint(GitHubReturnTo.Installed, admin);
        GitHubReturnCookie.Remember(http, started, states.Lifetime);
        return TypedResults.Ok(new GitHubAppInstallation($"{installUrl}?state={Uri.EscapeDataString(state)}"));
    }

    private static async Task<IResult> FinishCreation(
        string? code,
        string? state,
        HttpContext http,
        GitHubReturnStates states,
        WorkspaceAdmins admins,
        IMessageBus bus,
        CancellationToken cancellation)
    {
        var checkedReturn = await RefuseUnlessStartedHere(http, states, admins, state, GitHubReturnTo.Created, cancellation);
        if (checkedReturn.Problem is { } problem)
        {
            return BackToTheWebClient(GitHubAppQuery, problem);
        }

        if (code is null)
        {
            return BackToTheWebClient(GitHubAppQuery, "GitHub came back without the new App's code.");
        }

        var finished = await bus.InvokeAsync<GitHubAppCreationFinished>(
            new FinishGitHubAppCreation(code, checkedReturn.Started!), cancellation, ConnectionsAnswersWithin);
        return BackToTheWebClient(GitHubAppQuery, finished.Problem);
    }

    private static async Task<IResult> AddInstallation(
        [FromQuery(Name = "installation_id")] long? installationId,
        [FromQuery(Name = "setup_action")] string? setupAction,
        string? state,
        HttpContext http,
        GitHubReturnStates states,
        WorkspaceAdmins admins,
        IMessageBus bus,
        CancellationToken cancellation)
    {
        if (state is null && setupAction == GitHubChangedAnInstallationOnItsOwn)
        {
            return Results.Redirect($"/?{ConnectionQuery}={UpdatedOnGitHub}");
        }

        var checkedReturn = await RefuseUnlessStartedHere(http, states, admins, state, GitHubReturnTo.Installed, cancellation);
        if (checkedReturn.Problem is { } problem)
        {
            return BackToTheWebClient(ConnectionQuery, problem);
        }

        if (installationId is null)
        {
            return BackToTheWebClient(ConnectionQuery,
                "GitHub came back without an installation; an organisation's owner may still need to approve it.");
        }

        var added = await bus.InvokeAsync<GitHubInstallationAdded>(
            new AddGitHubInstallation(installationId.Value, checkedReturn.Started!), cancellation, ConnectionsAnswersWithin);
        return BackToTheWebClient(ConnectionQuery, added.Problem);
    }

    private static async Task<(GitHubReturn? Started, string? Problem)> RefuseUnlessStartedHere(
        HttpContext http,
        GitHubReturnStates states,
        WorkspaceAdmins admins,
        string? state,
        GitHubReturnTo to,
        CancellationToken cancellation)
    {
        var started = states.Read(state, to);
        if (started is null || !GitHubReturnCookie.CameFromTheSameBrowser(http, started))
        {
            return (null, ReturnSofaDidNotStart);
        }

        GitHubReturnCookie.Forget(http);
        return await admins.IsAdminAsync(started.AdminUserId, started.WorkspaceId, cancellation)
            ? (started, null)
            : (null, NoLongerAnAdmin);
    }

    private static IResult BackToTheWebClient(string what, string? problem) =>
        Results.Redirect(problem is null
            ? $"/?{what}=done"
            : $"/?{what}=failed&{ProblemQuery}={Uri.EscapeDataString(problem)}");
}

public sealed record NewGitHubApp(string? Organisation, string? Name);

public sealed record GitHubAppCreationForm(string FormAction, string Manifest);

public sealed record GitHubAppInstallation(string InstallUrl);
