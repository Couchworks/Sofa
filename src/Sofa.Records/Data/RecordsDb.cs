using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Records.Transcripts;

namespace Sofa.Records.Data;

public sealed class RecordsDb(DbContextOptions<RecordsDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Records);

    public DbSet<StoredTranscriptEntry> TranscriptEntries => Set<StoredTranscriptEntry>();

    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    private static void MigrationsInOwnSchema(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("ef_migrations_history", Schema);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<StoredTranscriptEntry>(entry =>
        {
            entry.ToTable("transcript_entries");
            entry.HasKey(e => new { e.RunId, e.Line, e.Part });
            entry.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
            entry.Property(e => e.ToolUseId).HasMaxLength(200);
            entry.Property(e => e.ToolName).HasMaxLength(200);
            entry.Property(e => e.ToolInput).HasColumnType("jsonb");
            entry.HasIndex(e => new { e.WorkspaceId, e.RunId });
            entry.HasIndex(e => new { e.RunId, e.ToolUseId });
        });
    }
}
