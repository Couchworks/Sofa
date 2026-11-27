using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Sofa.Contracts;
using Sofa.Contracts.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.AppGateway.Identity;

public static class SignedInUser
{
    public static Guid? IdOf(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(Claims.Subject), out var userId) ? userId : null;
}

public sealed class IdentityUsers(HttpClient identity)
{
    public static readonly Uri Address = new($"http://{SofaServices.Identity}");

    public async Task<UserProfile?> ProfileOfAsync(Guid userId, CancellationToken cancellation)
    {
        using var response = await identity.GetAsync($"/users/{userId}", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfile>(cancellation);
    }

    public async Task<IdentityAnswer<GitHubLinkStarted>> StartGitHubLinkAsync(Guid userId, CancellationToken cancellation)
    {
        using var response = await identity.PostAsync($"/users/{userId}/github-links", content: null, cancellation);
        return await IdentityAnswer<GitHubLinkStarted>.ReadAsync(response, cancellation);
    }

    public async Task<IdentityAnswer<UserAdded>> AddByGitHubLoginAsync(
        Guid workspaceId, AddUserByGitHubLogin added, CancellationToken cancellation)
    {
        using var response = await identity.PostAsJsonAsync($"/workspaces/{workspaceId}/users", added, cancellation);
        return await IdentityAnswer<UserAdded>.ReadAsync(response, cancellation);
    }
}

public sealed record IdentityAnswer<T>(T? Value, int Status, string? Problem)
{
    public static async Task<IdentityAnswer<T>> ReadAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (response.IsSuccessStatusCode)
        {
            return new(await response.Content.ReadFromJsonAsync<T>(cancellation), (int)response.StatusCode, null);
        }

        if ((int)response.StatusCode >= 500)
        {
            response.EnsureSuccessStatusCode();
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellation);
        return new(default, (int)response.StatusCode, problem?.Title ?? $"Identity answered {(int)response.StatusCode}.");
    }
}
