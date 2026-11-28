using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Code.Bindings;
using Sofa.Code.PullRequests;
using Sofa.Code.Pushes;
using Sofa.Code.Repositories;
using Sofa.Contracts;

namespace Sofa.Code.Data;

public sealed class CodeDb(DbContextOptions<CodeDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Code);

    public DbSet<CodeBinding> Bindings => Set<CodeBinding>();

    public DbSet<RepositoryGrant> Grants => Set<RepositoryGrant>();

    public DbSet<KeptPullRequest> PullRequests => Set<KeptPullRequest>();

    public DbSet<UndonePush> UndonePushes => Set<UndonePush>();

    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    private static void MigrationsInOwnSchema(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("ef_migrations_history", Schema);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<CodeBinding>(binding =>
        {
            binding.ToTable("bindings");
            binding.HasKey(b => b.ProjectId);
            binding.Property(b => b.ProjectKey).HasMaxLength(10);
            binding.HasIndex(b => new { b.WorkspaceId, b.ProjectKey });
            binding.HasIndex(b => b.ConnectionId);
        });

        model.Entity<RepositoryGrant>(grant =>
        {
            grant.ToTable("repository_grants");
            grant.HasKey(g => g.RunId);
            grant.Property(g => g.Access).HasConversion<string>().HasMaxLength(30);
            grant.Property(g => g.OwnBranch).HasMaxLength(255);
            grant.HasIndex(g => new { g.WorkspaceId, g.ExpiresAt });
        });

        model.Entity<KeptPullRequest>(pull =>
        {
            pull.ToTable("pull_requests");
            pull.Property(p => p.Id).ValueGeneratedNever();
            pull.Property(p => p.ProjectKey).HasMaxLength(10);
            pull.Property(p => p.WorkItemKey).HasMaxLength(21);
            pull.Property(p => p.Repository).HasMaxLength(140);
            pull.Property(p => p.Title).HasMaxLength(1024);
            pull.Property(p => p.Url).HasMaxLength(500);
            pull.Property(p => p.HeadBranch).HasMaxLength(255);
            pull.Property(p => p.BaseBranch).HasMaxLength(255);
            pull.Property(p => p.State).HasConversion<string>().HasMaxLength(20);
            pull.Ignore(p => p.AsRead);
            pull.HasIndex(p => new { p.ProjectId, p.Repository, p.Number }).IsUnique();
            pull.HasIndex(p => new { p.ProjectId, p.WorkItemKey });
        });

        model.Entity<UndonePush>(undone =>
        {
            undone.ToTable("undone_pushes");
            undone.Property(u => u.Id).ValueGeneratedNever();
            undone.Property(u => u.Repository).HasMaxLength(140);
            undone.Property(u => u.Ref).HasMaxLength(255);
            undone.Property(u => u.PutBackTo).HasMaxLength(40);
            undone.HasIndex(u => u.Ref);
        });
    }
}
