using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Notices.Inbox;
using Sofa.Notices.SignIns;

namespace Sofa.Notices.Data;

public sealed class NoticesDb(DbContextOptions<NoticesDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Notices);

    public DbSet<InboxItem> InboxItems => Set<InboxItem>();
    public DbSet<RunnerSignIn> RunnerSignIns => Set<RunnerSignIn>();

    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    private static void MigrationsInOwnSchema(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("ef_migrations_history", Schema);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<InboxItem>(item =>
        {
            item.Property(i => i.Id).ValueGeneratedNever();
            item.Property(i => i.Kind).HasConversion<string>().HasMaxLength(40);
            item.Property(i => i.For).HasConversion<string>().HasMaxLength(40);
            item.Property(i => i.ProjectKey).HasMaxLength(10);
            item.Property(i => i.WorkItemKey).HasMaxLength(21);
            item.Property(i => i.Title).HasMaxLength(1024);
            item.Property(i => i.Detail).HasMaxLength(InboxRules.LongestDetail);
            item.Property(i => i.Url).HasMaxLength(500);
            item.HasIndex(i => new { i.Kind, i.SubjectId }).IsUnique();
            item.HasIndex(i => new { i.WorkspaceId, i.For });
            item.HasIndex(i => new { i.WorkspaceId, i.UserId });
        });

        model.Entity<RunnerSignIn>(signIn =>
        {
            signIn.HasKey(s => s.RunnerId);
            signIn.Property(s => s.RunnerId).ValueGeneratedNever();
            signIn.Property(s => s.RunnerName).HasMaxLength(100);
            signIn.HasIndex(s => new { s.WorkspaceId, s.UserId });
        });
    }
}
