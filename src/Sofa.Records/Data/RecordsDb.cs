using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Records.Transcripts;

namespace Sofa.Records.Data;

public sealed class RecordsDb(DbContextOptions<RecordsDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Records);

    public DbSet<StoredTranscriptLine> TranscriptLines => Set<StoredTranscriptLine>();

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
    }
}
