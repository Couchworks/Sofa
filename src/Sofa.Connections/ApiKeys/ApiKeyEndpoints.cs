using Sofa.Connections.Credentials;
using Sofa.Connections.Persistence;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.ApiKeys;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeys(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/workspaces/{workspaceId:guid}/api-keys", Save);
        return endpoints;
    }

    private static async Task<IResult> Save(
        Guid workspaceId,
        ApiKeyToSave toSave,
        ConnectionsDb db,
        ICredentialVault vault,
        TimeProvider time,
        ILogger<ApiKeyToSave> logger,
        CancellationToken cancellation)
    {
        if (ApiKeyRules.ProblemWith(toSave.Vendor, toSave.ApiKey) is { } problem)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: problem);
        }

        var id = Guid.CreateVersion7();
        db.ApiKeys.Add(new ApiKey
        {
            Id = id,
            WorkspaceId = workspaceId,
            Vendor = toSave.Vendor.ToString(),
            SealedKey = await vault.Seal(toSave.ApiKey.Trim(), ApiKeyRules.PurposeOf(id), cancellation),
            SavedAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Saved {Vendor} API key {ApiKeyId} of Workspace {WorkspaceId}", toSave.Vendor, id, workspaceId);
        return Results.Created((string?)null, new ApiKeySaved(id));
    }
}
