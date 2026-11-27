using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Sofa.Connections.Persistence;

public static class PersistenceSetup
{
    public static IServiceCollection AddConnectionsDb(this IServiceCollection services) =>
        services.AddDbContext<ConnectionsDb>((provider, options) =>
            options.UseConnectionsDefaults(provider.GetRequiredService<NpgsqlDataSource>()));

    public static DbContextOptionsBuilder UseConnectionsDefaults(this DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource, InTheServicesOwnSchema);

    public static DbContextOptionsBuilder UseConnectionsDefaults(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, InTheServicesOwnSchema);

    public static async Task MigrateConnectionsDb(this IServiceProvider services, CancellationToken cancellation = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ConnectionsDb>().Database.MigrateAsync(cancellation);
    }

    private static void InTheServicesOwnSchema(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(ConnectionsDb.MigrationsHistoryTable, ConnectionsDb.Schema);
}
