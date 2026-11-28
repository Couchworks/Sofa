namespace Sofa.Code.GitHub;

public sealed class SofasGitHubApp(IServiceScopeFactory scopes)
{
    private string? _bot;

    public async Task<string?> BotAsync(CancellationToken cancellation)
    {
        if (_bot is not null)
        {
            return _bot;
        }

        await using var scope = scopes.CreateAsyncScope();
        _bot = await scope.ServiceProvider.GetRequiredService<ConnectionsTokens>().AppBotAsync(cancellation);
        return _bot;
    }
}
