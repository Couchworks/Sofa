using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sofa.Contracts;

namespace Sofa.Connections.Persistence;

public sealed partial class ConnectionsDb(DbContextOptions<ConnectionsDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Connections);

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<GitHubApp> GitHubApps => Set<GitHubApp>();

    public DbSet<GitHubAppCreation> GitHubAppCreations => Set<GitHubAppCreation>();

    public DbSet<Connection> Connections => Set<Connection>();

    public DbSet<ReceivedDelivery> ReceivedDeliveries => Set<ReceivedDelivery>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<GitHubApp>(app =>
        {
            app.HasKey(a => a.Id);
            app.Property(a => a.Id).ValueGeneratedNever();
        });

        model.Entity<GitHubAppCreation>().HasKey(c => c.State);

        model.Entity<Connection>(connection =>
        {
            connection.HasKey(c => c.Id);
            connection.HasIndex(c => c.GitHubInstallationId).IsUnique();
            connection.HasIndex(c => c.WorkspaceId);
        });

        model.Entity<ReceivedDelivery>(delivery =>
        {
            delivery.HasKey(d => d.DeliveryId);
            delivery.HasIndex(d => d.ReceivedAt);
        });

        NameEverythingInSnakeCase(model);
    }

    private static void NameEverythingInSnakeCase(ModelBuilder model)
    {
        foreach (var entity in model.Model.GetEntityTypes())
        {
            entity.SetTableName(SnakeCase(entity.GetTableName()!));
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCase(property.Name));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(SnakeCase(key.GetName()!));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(SnakeCase(index.GetDatabaseName()!));
            }
        }
    }

    private static string SnakeCase(string name) =>
        WordStart().Replace(name.Replace("GitHub", "Github"), "_$1").ToLowerInvariant();

    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex WordStart();
}
