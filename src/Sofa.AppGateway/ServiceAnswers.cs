using Microsoft.AspNetCore.Mvc;

namespace Sofa.AppGateway;

public sealed record ServiceAnswer<T>(T? Value, int Status, string? Problem)
{
    public static async Task<ServiceAnswer<T>> ReadAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (response.IsSuccessStatusCode)
        {
            var value = response.Content.Headers.ContentLength == 0 || response.StatusCode == System.Net.HttpStatusCode.NoContent
                ? default
                : await response.Content.ReadFromJsonAsync<T>(cancellation);
            return new(value, (int)response.StatusCode, null);
        }

        if ((int)response.StatusCode >= 500)
        {
            response.EnsureSuccessStatusCode();
        }

        var problem = await ProblemIn(response, cancellation);
        return new(default, (int)response.StatusCode, problem ?? $"The service answered {(int)response.StatusCode}.");
    }

    public bool Succeeded => Problem is null;

    private static async Task<string?> ProblemIn(HttpResponseMessage response, CancellationToken cancellation)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellation))?.Title;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
