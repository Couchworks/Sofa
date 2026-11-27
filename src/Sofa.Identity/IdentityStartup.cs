using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Sofa.Identity.Data;
using Sofa.Identity.Setup;
using Sofa.Identity.Tokens;

namespace Sofa.Identity;

public static class IdentityStartup
{
    public static async Task PrepareAsync(this WebApplication app, CancellationToken cancellation = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<IdentityDb>();
        var time = services.GetRequiredService<TimeProvider>();

        await db.Database.MigrateAsync(cancellation);
        await services.GetRequiredService<TokenKeys>().LoadOrCreateAsync(db, time, cancellation);
        await Clients.RegisterAsync(
            services.GetRequiredService<IOpenIddictApplicationManager>(),
            services.GetRequiredService<IOptions<ClientOptions>>().Value,
            cancellation);
        await SetupCodeOnStart.IssueWhileNotSetUpAsync(
            db, services.GetRequiredService<ILoggerFactory>().CreateLogger("Sofa.Identity.Setup"), cancellation);
    }
}
