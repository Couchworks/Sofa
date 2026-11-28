using Sofa.Contracts.Intake;
using Sofa.Intake.Data;
using Sofa.Intake.GitHub;

namespace Sofa.Intake.Requests;

public static class RequestEndpoints
{
    public static IEndpointRouteBuilder MapRequests(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/requests/{requestId:guid}/now", RequestNowAsync);
        return endpoints;
    }

    private static async Task<IResult> RequestNowAsync(
        Guid workspaceId, Guid requestId, IntakeDb db, IRequestSource source, CancellationToken cancellation)
    {
        if (await db.Requests.FindAsync([requestId], cancellation) is not { } request || request.WorkspaceId != workspaceId)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Request.");
        }

        if (await source.ReadAsync(request.Issue, cancellation) is not { } now)
        {
            return Results.Problem(statusCode: StatusCodes.Status410Gone,
                title: $"{request.Repository}#{request.Number} is no longer on GitHub, or Sofa may no longer read it.");
        }

        return Results.Ok(new RequestNow(request.Id, request.Repository, request.Number, request.Url, now.Title, now.Body, now.State));
    }
}
