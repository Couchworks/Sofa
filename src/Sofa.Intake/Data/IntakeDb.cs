using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Intake.Bindings;
using Sofa.Intake.Questions;
using Sofa.Intake.Reports;
using Sofa.Intake.Requests;

namespace Sofa.Intake.Data;

public sealed class IntakeDb(DbContextOptions<IntakeDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Intake);

    public DbSet<RequestBinding> RequestBindings => Set<RequestBinding>();
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<ReportComment> ReportComments => Set<ReportComment>();
    public DbSet<QuestionComment> QuestionComments => Set<QuestionComment>();

    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    private static void MigrationsInOwnSchema(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("ef_migrations_history", Schema);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<RequestBinding>(binding =>
        {
            binding.HasKey(b => b.ProjectId);
            binding.Property(b => b.ProjectKey).HasMaxLength(10);
            binding.Property(b => b.Repository).HasMaxLength(140);
            binding.Property(b => b.Label).HasMaxLength(50);
            binding.HasIndex(b => b.ConnectionId);
        });

        model.Entity<Request>(request =>
        {
            request.Property(r => r.Id).ValueGeneratedNever();
            request.Property(r => r.ProjectKey).HasMaxLength(10);
            request.Property(r => r.Repository).HasMaxLength(140);
            request.Property(r => r.Title).HasMaxLength(RequestRules.LongestTitle);
            request.Property(r => r.Url).HasMaxLength(500);
            request.Property(r => r.MarkedByGitHubAccountId).HasColumnName("marked_by_github_account_id");
            request.Property(r => r.MarkedByGitHubLogin).HasColumnName("marked_by_github_login").HasMaxLength(39);
            request.Property(r => r.PhaseLabel).HasMaxLength(50);
            request.HasIndex(r => new { r.ProjectId, r.Repository, r.Number }).IsUnique();
            request.HasIndex(r => new { r.ConnectionId, r.Repository, r.Number });
            request.Ignore(r => r.Issue);
            request.Ignore(r => r.Ref);
        });

        model.Entity<ReportComment>(report =>
        {
            report.HasKey(r => new { r.RequestId, r.Phase });
            report.Property(r => r.Phase).HasConversion<string>().HasMaxLength(20);
            report.HasOne<Request>().WithMany().HasForeignKey(r => r.RequestId);
        });

        model.Entity<QuestionComment>(question =>
        {
            question.HasKey(q => q.QuestionId);
            question.Property(q => q.Phase).HasConversion<string>().HasMaxLength(20);
            question.Property(q => q.WorkItemKey).HasMaxLength(21);
            question.Property(q => q.Answer).HasMaxLength(65_536);
            question.HasOne<Request>().WithMany().HasForeignKey(q => q.RequestId);
            question.HasIndex(q => q.RequestId);
            question.Ignore(q => q.IsOpen);
        });
    }
}
