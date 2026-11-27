using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sofa.Connections.Persistence;
using Sofa.Contracts.Connections;
using Wolverine;

namespace Sofa.Connections.GitHub;

public static class WebhookEndpoints
{
    public const string EventHeader = "X-GitHub-Event";
    public const string DeliveryHeader = "X-GitHub-Delivery";

    public static IEndpointRouteBuilder MapGitHubWebhooks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(GitHubAppPaths.Webhooks, Receive);
        return endpoints;
    }

    private static async Task<IResult> Receive(
        HttpRequest request,
        ConnectionsDb db,
        GitHubAppAccess access,
        IMessageBus bus,
        TimeProvider time,
        ILogger<GitHubEventReceived> logger,
        CancellationToken cancellation)
    {
        var body = await ReadBody(request, cancellation);

        var app = await access.FindApp(cancellation);
        var signature = request.Headers[WebhookSignature.Header].ToString();
        var webhookSecret = app is null ? null : await access.WebhookSecret(app, cancellation);
        if (webhookSecret is null || !WebhookSignature.Matches(body, signature, webhookSecret))
        {
            logger.LogWarning("Refused a GitHub delivery whose signature doesn't match the App's webhook secret");
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "The delivery's signature doesn't match Sofa's GitHub App.");
        }

        if (!Guid.TryParse(request.Headers[DeliveryHeader], out var deliveryId)
            || request.Headers[EventHeader].ToString() is not { Length: > 0 } eventName)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: $"A delivery needs the {DeliveryHeader} and {EventHeader} headers.");
        }

        if (await db.ReceivedDeliveries.AnyAsync(d => d.DeliveryId == deliveryId, cancellation))
        {
            return Results.Ok();
        }

        var (action, installationId) = ReadActionAndInstallation(body);
        var connection = installationId is null
            ? null
            : await db.Connections.SingleOrDefaultAsync(c => c.GitHubInstallationId == installationId, cancellation);

        var received = new GitHubEventReceived(
            deliveryId, eventName, action, installationId, connection?.Id, connection?.WorkspaceId,
            Encoding.UTF8.GetString(body));

        InstallationLifecycle.Follow(received, connection, db, time, logger);
        await bus.PublishAsync(received);
        db.ReceivedDeliveries.Add(new ReceivedDelivery { DeliveryId = deliveryId, ReceivedAt = time.GetUtcNow() });
        await db.SaveChangesAsync(cancellation);

        logger.LogInformation(
            "Received GitHub {GitHubEvent} delivery {DeliveryId} for installation {InstallationId}",
            received.Name, deliveryId, installationId);
        return Results.Accepted();
    }

    private static async Task<byte[]> ReadBody(HttpRequest request, CancellationToken cancellation)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellation);
        return buffer.ToArray();
    }

    private static (string? Action, long? InstallationId) ReadActionAndInstallation(byte[] body)
    {
        try
        {
            using var payload = JsonDocument.Parse(body);
            var root = payload.RootElement;
            var action = root.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
            long? installation = root.TryGetProperty("installation", out var i)
                && i.ValueKind == JsonValueKind.Object
                && i.TryGetProperty("id", out var id)
                && id.TryGetInt64(out var installationId)
                ? installationId
                : null;
            return (action, installation);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
