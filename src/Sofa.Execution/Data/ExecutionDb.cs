using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Execution.Runners;
using Sofa.Execution.Runs;

namespace Sofa.Execution.Data;

public sealed class ExecutionDb(DbContextOptions<ExecutionDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Execution);

    public DbSet<Runner> Runners => Set<Runner>();

    public DbSet<Run> Runs => Set<Run>();

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

        model.Entity<Run>(run =>
        {
            run.Property(r => r.Id).ValueGeneratedNever();
            run.Property(r => r.Phase).HasConversion<string>().HasMaxLength(20);
            run.Property(r => r.Vendor).HasConversion<string>().HasMaxLength(20);
            run.Property(r => r.Sandbox).HasConversion<string>().HasMaxLength(20);
            run.Property(r => r.Priority).HasConversion<string>().HasMaxLength(20);
            run.Property(r => r.State).HasConversion<string>().HasMaxLength(20);
            run.Property(r => r.RefusedAs).HasConversion<string>().HasMaxLength(30);
            run.Property(r => r.Model).HasMaxLength(100);
            run.Property(r => r.WorkItemKey).HasMaxLength(21);
            run.Property(r => r.Brief).HasMaxLength(Contracts.Execution.RunLimits.LongestBrief);
            run.Property(r => r.Why).HasMaxLength(4000);
            run.Property(r => r.Outcome).HasMaxLength(50);
            run.Property(r => r.Question).HasMaxLength(Contracts.Execution.RunLimits.LongestQuestion);
            run.Ignore(r => r.Asked);
            run.Property(r => r.Version).IsRowVersion();
            run.Ignore(r => r.OwnBranch);
            run.Ignore(r => r.HasEnded);
            run.Ignore(r => r.IsRetriedOnItsOwn);
            run.Ignore(r => r.MayBeRetriedByAUser);
            run.Ignore(r => r.Ended);
            run.Ignore(r => r.Details);
            run.HasOne<Runner>().WithMany().HasForeignKey(r => r.RunnerId).OnDelete(DeleteBehavior.Restrict);
            run.HasIndex(r => r.State);
            run.HasIndex(r => new { r.WorkspaceId, r.QueuedAt });
            run.HasIndex(r => r.WorkItemId);
            run.ToTable(table =>
            {
                table.HasCheckConstraint("ck_runs_running_on_a_runner", "state <> 'Running' or runner_id is not null");
                table.HasCheckConstraint("ck_runs_retries_left_within_the_limit", "retries_left >= 0 and retries_left <= retry_limit");
                table.HasCheckConstraint("ck_runs_end_with_an_outcome_or_a_question", "outcome is null or question_id is null");
                table.HasCheckConstraint("ck_runs_questions_have_their_text", "(question_id is null) = (question is null)");
            });
        });
    }
}
