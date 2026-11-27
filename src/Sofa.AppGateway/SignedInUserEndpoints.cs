using System.Security.Claims;
using Sofa.Contracts.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.AppGateway;

public static class SignedInUserEndpoints
{
    public static RouteGroupBuilder MapSignedInUser(this RouteGroupBuilder api)
    {
        api.MapGet("/me", async (ClaimsPrincipal principal, IdentityUsers users, CancellationToken cancellation) =>
                Guid.TryParse(principal.FindFirstValue(Claims.Subject), out var userId)
                && await users.ProfileOfAsync(userId, cancellation) is { } profile
                    ? Results.Ok(profile)
                    : Results.NotFound())
            .RequireAuthorization()
            .WithName("GetSignedInUser")
            .WithSummary("The signed-in User, with their Workspaces and Teams.")
            .Produces<UserProfile>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return api;
    }
}

public sealed class IdentityUsers(HttpClient identity)
{
    public async Task<UserProfile?> ProfileOfAsync(Guid userId, CancellationToken cancellation)
    {
        using var response = await identity.GetAsync($"/users/{userId}", cancellation);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfile>(cancellation);
    }
}
