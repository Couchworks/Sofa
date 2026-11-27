using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Execution.Runners;

namespace Sofa.Execution.Data;

public sealed class ExecutionDb(DbContextOptions<ExecutionDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Execution);

    public DbSet<Runner> Runners => Set<Runner>();

    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    private static void MigrationsInOwnSchema(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("ef_migrations_history", Schema);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<Runner>(runner =>
        {
            runner.Property(r => r.Id).ValueGeneratedNever();
            runner.Property(r => r.Name).HasMaxLength(Contracts.Execution.RunnerLimits.LongestName);
            runner.Property(r => r.Kind).HasConversion<string>().HasMaxLength(20);
            runner.Property(r => r.Vendor).HasConversion<string>().HasMaxLength(20);
            runner.Property(r => r.WorkerVolume).HasMaxLength(100);
            runner.Ignore(r => r.Owner);
            runner.Ignore(r => r.Details);
            runner.HasIndex(r => new { r.WorkspaceId, r.OwnerUserId });
            runner.HasIndex(r => new { r.WorkspaceId, r.OwnerTeamId });
            runner.ToTable(table =>
            {
                table.HasCheckConstraint("ck_runners_owned_by_a_user_or_a_team", "(owner_user_id is null) <> (owner_team_id is null)");
                table.HasCheckConstraint("ck_runners_subscriptions_are_personal",
                    "kind <> 'Subscription' or (owner_user_id is not null and worker_volume is not null and api_key_id is null)");
                table.HasCheckConstraint("ck_runners_api_key_runners_have_a_key", "kind <> 'ApiKey' or api_key_id is not null");
                table.HasCheckConstraint("ck_runners_run_at_least_one_at_once", "runs_at_once >= 1");
            });
        });
    }
}
