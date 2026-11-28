using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Records.Contributions;
using Sofa.Records.Transcripts;

namespace Sofa.Records.Data;

public sealed class RecordsDb(DbContextOptions<RecordsDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Records);

    public DbSet<StoredTranscriptLine> TranscriptLines => Set<StoredTranscriptLine>();

    public DbSet<StoredContribution> Contributions => Set<StoredContribution>();

    public DbSet<StoredAttachment> Attachments => Set<StoredAttachment>();

    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    private static void MigrationsInOwnSchema(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("ef_migrations_history", Schema);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<StoredTranscriptLine>(line =>
        {
            line.ToTable("transcript_lines");
            line.HasKey(l => new { l.RunId, l.Number });
            line.HasIndex(l => new { l.WorkspaceId, l.RunId });
        });

        model.Entity<StoredContribution>(contribution =>
        {
            contribution.ToTable("contributions");
            contribution.HasKey(c => c.RunId);
            contribution.Property(c => c.Phase).HasConversion<string>().HasMaxLength(20);
            contribution.Property(c => c.Outcome).HasMaxLength(ContributionRules.LongestOutcome);
            contribution.Property(c => c.Markdown).HasMaxLength(Contracts.Execution.RunLimits.LongestContribution);
            contribution.HasMany(c => c.Attachments).WithOne().HasForeignKey(a => a.RunId).OnDelete(DeleteBehavior.Cascade);
            contribution.HasIndex(c => new { c.WorkspaceId, c.WorkItemId, c.MadeAt });
            contribution.Ignore(c => c.Details);
        });

        model.Entity<StoredAttachment>(attachment =>
        {
            attachment.ToTable("contribution_attachments");
            attachment.Property(a => a.Id).ValueGeneratedNever();
            attachment.Property(a => a.Name).HasMaxLength(ContributionRules.LongestAttachmentName);
            attachment.Property(a => a.ContentType).HasMaxLength(100);
            attachment.Property(a => a.FileKey).HasMaxLength(500);
            attachment.HasIndex(a => new { a.RunId, a.Position }).IsUnique();
            attachment.Ignore(a => a.Details);
        });
    }
}
