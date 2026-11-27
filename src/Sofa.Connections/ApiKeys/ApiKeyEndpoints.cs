using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sofa.Connections.Credentials;
using Sofa.Connections.Persistence;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.ApiKeys;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeys(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/workspaces/{workspaceId:guid}/api-keys", Save);
        endpoints.MapPost("/workspaces/{workspaceId:guid}/api-keys/{apiKeyId:guid}/opened-for-a-run", OpenForARun);
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

    private static async Task<IResult> OpenForARun(
        Guid workspaceId,
        Guid apiKeyId,
        ApiKeyForARun forTheRun,
        [FromHeader(Name = ApiKeysForRuns.ExecutionsSecretHeader)] string? secretSent,
        IOptions<ApiKeyOptions> options,
        ConnectionsDb db,
        ICredentialVault vault,
        ILogger<ApiKeyForARun> logger,
        CancellationToken cancellation)
    {
        if (!ApiKeyRules.IsExecution(secretSent, options.Value.ExecutionSecret))
        {
            logger.LogWarning("Refused to open API key {ApiKeyId} for Run {RunId}: the asker didn't prove it is Execution", apiKeyId, forTheRun.RunId);
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only Execution opens an API key, for a Run it starts.");
        }

        var apiKey = await db.ApiKeys.FindAsync([apiKeyId], cancellation);
        if (apiKey is null || apiKey.WorkspaceId != workspaceId)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such API key.");
        }

        var opened = await vault.Open(apiKey.SealedKey, ApiKeyRules.PurposeOf(apiKey.Id), cancellation);
        logger.LogInformation("Opened API key {ApiKeyId} of Workspace {WorkspaceId} for Run {RunId}", apiKey.Id, workspaceId, forTheRun.RunId);
        return Results.Ok(new OpenedApiKey(opened));
    }
}

public sealed class ApiKeyOptions
{
    public const string Section = "ApiKeys";

    public string? ExecutionSecret { get; set; }
}
