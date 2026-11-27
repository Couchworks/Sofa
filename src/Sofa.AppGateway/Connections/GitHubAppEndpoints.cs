using Microsoft.AspNetCore.Http.HttpResults;
using Sofa.AppGateway.Admins;
using Sofa.Contracts.Connections;
using Wolverine;

namespace Sofa.AppGateway.Connections;

public static class GitHubAppEndpoints
{
    public static readonly TimeSpan ConnectionsAnswersWithin = TimeSpan.FromSeconds(30);

    public const string GitHubAppQuery = "github-app";
    public const string ConnectionQuery = "connection";
    public const string ProblemQuery = "problem";

    public static RouteGroupBuilder MapGitHubAppApi(this RouteGroupBuilder api)
    {
        api.MapGet("/github-app", (ConnectionsClient connections, CancellationToken cancellation) =>
                connections.GitHubApp(cancellation))
            .WithName("GetGitHubApp")
            .WithSummary("Whether Sofa's GitHub App has been created, and where to install it.");

        api.MapPost("/github-app/creations", StartCreation)
            .WithName("StartGitHubAppCreation")
            .WithSummary("The form that sends the GitHub App's manifest to GitHub, to create the App there.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapGet("/connections", (SignedInAdmin admin, ConnectionsClient connections, CancellationToken cancellation) =>
                connections.ConnectionsOf(admin.WorkspaceId, cancellation))
            .WithName("GetConnections")
            .WithSummary("The Workspace's Connections to outside systems.");

        return api;
    }

    public static IEndpointRouteBuilder MapGitHubAppReturns(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(GitHubAppPaths.Created, FinishCreation).ExcludeFromDescription();
        endpoints.MapGet(GitHubAppPaths.Installed, AddInstallation).ExcludeFromDescription();
        return endpoints;
    }

    private static async Task<Results<Ok<GitHubAppCreationForm>, ProblemHttpResult>> StartCreation(
        NewGitHubApp app, SignedInAdmin admin, IMessageBus bus, CancellationToken cancellation)
    {
        var started = await bus.InvokeAsync<GitHubAppCreationStarted>(
            new StartGitHubAppCreation(admin.WorkspaceId, app.Organisation, app.Name), cancellation, ConnectionsAnswersWithin);
        return started is { FormAction: { } formAction, Manifest: { } manifest }
            ? TypedResults.Ok(new GitHubAppCreationForm(formAction, manifest))
            : TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: started.Problem);
    }

    private static async Task<IResult> FinishCreation(
        string? code, string? state, IMessageBus bus, CancellationToken cancellation)
    {
        if (code is null || state is null)
        {
            return BackToTheWebClient(GitHubAppQuery, "GitHub came back without the new App's code.");
        }

        var finished = await bus.InvokeAsync<GitHubAppCreationFinished>(
            new FinishGitHubAppCreation(code, state), cancellation, ConnectionsAnswersWithin);
        return BackToTheWebClient(GitHubAppQuery, finished.Problem);
    }

    private static async Task<IResult> AddInstallation(
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "installation_id")] long? installationId,
        SignedInAdmin admin,
        IMessageBus bus,
        CancellationToken cancellation)
    {
        if (installationId is null)
        {
            return BackToTheWebClient(ConnectionQuery,
                "GitHub came back without an installation; an organisation's owner may still need to approve it.");
        }

        var added = await bus.InvokeAsync<GitHubInstallationAdded>(
            new AddGitHubInstallation(admin.WorkspaceId, installationId.Value), cancellation, ConnectionsAnswersWithin);
        return BackToTheWebClient(ConnectionQuery, added.Problem);
    }

    private static IResult BackToTheWebClient(string what, string? problem) =>
        Results.Redirect(problem is null
            ? $"/?{what}=done"
            : $"/?{what}=failed&{ProblemQuery}={Uri.EscapeDataString(problem)}");
}

public sealed record NewGitHubApp(string? Organisation, string? Name);

public sealed record GitHubAppCreationForm(string FormAction, string Manifest);
