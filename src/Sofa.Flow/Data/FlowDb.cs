using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Flow.Projects;
using Sofa.Flow.Questions;
using Sofa.Flow.WorkItems;

namespace Sofa.Flow.Data;

public sealed class FlowDb(DbContextOptions<FlowDb> options) : DbContext(options)
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Flow);

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Phase> Phases => Set<Phase>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<WorkItemRun> WorkItemRuns => Set<WorkItemRun>();
    public DbSet<PhaseEnd> PhaseEnds => Set<PhaseEnd>();

    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        options.UseNpgsql(dataSource, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, MigrationsInOwnSchema).UseSnakeCaseNamingConvention();

    private static void MigrationsInOwnSchema(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("ef_migrations_history", Schema);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.Entity<Project>(project =>
        {
            project.Property(p => p.Id).ValueGeneratedNever();
            project.Property(p => p.Key).HasMaxLength(ProjectRules.LongestKey);
            project.Property(p => p.Name).HasMaxLength(ProjectRules.LongestName);
            project.Property(p => p.RequestRepository).HasMaxLength(ProjectRules.LongestRepository);
            project.Property(p => p.RequestLabel).HasMaxLength(ProjectRules.LongestLabel);
            project.HasIndex(p => new { p.WorkspaceId, p.Key }).IsUnique();
            project.Ignore(p => p.Requests);
            project.Ignore(p => p.Code);
            project.Ignore(p => p.Bound);
        });

        model.Entity<WorkItem>(workItem =>
        {
            workItem.Property(w => w.Id).ValueGeneratedNever();
            workItem.Property(w => w.Key).HasMaxLength(ProjectRules.LongestKey + 11);
            workItem.Property(w => w.Title).HasMaxLength(WorkItemRules.LongestTitle);
            workItem.Property(w => w.RequestRepository).HasMaxLength(ProjectRules.LongestRepository);
            workItem.Property(w => w.RequestUrl).HasMaxLength(WorkItemRules.LongestUrl);
            workItem.HasOne<Project>().WithMany().HasForeignKey(w => w.ProjectId);
            workItem.HasIndex(w => new { w.ProjectId, w.Number }).IsUnique();
            workItem.HasIndex(w => w.RequestId).IsUnique();
            workItem.Property(w => w.State).HasConversion<string>().HasMaxLength(30);
            workItem.Property(w => w.Phase).HasConversion<string>().HasMaxLength(20);
            workItem.Property(w => w.Track).HasConversion<string>().HasMaxLength(20);
            workItem.Property(w => w.WaitingFor).HasConversion<string>().HasMaxLength(40);
            workItem.Property(w => w.WaitingBecause).HasMaxLength(WorkItemRules.LongestWhy);
            workItem.Property(w => w.PullRequestRepository).HasMaxLength(ProjectRules.LongestRepository);
            workItem.Property(w => w.PullRequestUrl).HasMaxLength(WorkItemRules.LongestUrl);
            workItem.Property(w => w.PullRequestState).HasConversion<string>().HasMaxLength(20);
            workItem.Property(w => w.Version).IsRowVersion();
            workItem.HasMany(w => w.Runs).WithOne().HasForeignKey(r => r.WorkItemId);
            workItem.HasMany(w => w.PhaseEnds).WithOne().HasForeignKey(e => e.WorkItemId);
            workItem.Ignore(w => w.Request);
            workItem.Ignore(w => w.Waiting);
            workItem.Ignore(w => w.PullRequest);
        });

        model.Entity<WorkItemRun>(run =>
        {
            run.HasKey(r => r.RunId);
            run.Property(r => r.RunId).ValueGeneratedNever();
            run.Property(r => r.Phase).HasConversion<string>().HasMaxLength(20);
            run.Property(r => r.EndedAs).HasMaxLength(100);
            run.HasIndex(r => new { r.WorkItemId, r.StartedAt });
            run.Ignore(r => r.Details);
        });

        model.Entity<PhaseEnd>(end =>
        {
            end.HasKey(e => e.RunId);
            end.Property(e => e.RunId).ValueGeneratedNever();
            end.Property(e => e.Phase).HasConversion<string>().HasMaxLength(20);
            end.Property(e => e.Outcome).HasMaxLength(40);
            end.Property(e => e.Gate).HasConversion<string>().HasMaxLength(20);
            end.Property(e => e.Standing).HasConversion<string>().HasMaxLength(20);
            end.Property(e => e.Comment).HasMaxLength(WorkItemRules.LongestComment);
            end.HasIndex(e => new { e.WorkItemId, e.EndedAt });
            end.Ignore(e => e.Details);
        });

        model.Entity<Question>(question =>
        {
            question.Property(q => q.Id).ValueGeneratedNever();
            question.Property(q => q.Phase).HasConversion<string>().HasMaxLength(20);
            question.Property(q => q.Text).HasMaxLength(Contracts.Execution.RunLimits.LongestQuestion);
            question.Property(q => q.Answer).HasMaxLength(QuestionRules.LongestAnswer);
            question.HasOne<WorkItem>().WithMany().HasForeignKey(q => q.WorkItemId);
            question.HasIndex(q => new { q.WorkItemId, q.AskedAt });
            question.Ignore(q => q.IsOpen);
            question.Ignore(q => q.Details);
        });

        model.Entity<Agent>(agent =>
        {
            agent.Property(a => a.Id).ValueGeneratedNever();
            agent.Property(a => a.Name).HasMaxLength(200);
            agent.Property(a => a.Role).HasConversion<string>().HasMaxLength(20);
            agent.Property(a => a.Vendor).HasConversion<string>().HasMaxLength(20);
            agent.Property(a => a.Model).HasMaxLength(100);
            agent.Property(a => a.Instructions).HasMaxLength(ProjectRules.LongestInstructions);
            agent.Property(a => a.Registration).HasConversion<string>().HasMaxLength(20);
            agent.Property(a => a.RegistrationRefusedBecause).HasMaxLength(1000);
            agent.HasOne<Project>().WithMany().HasForeignKey(a => a.ProjectId);
            agent.HasAlternateKey(a => new { a.ProjectId, a.Id });
        });

        model.Entity<Phase>(phase =>
        {
            phase.HasKey(p => new { p.ProjectId, p.Name });
            phase.Property(p => p.Name).HasConversion<string>().HasMaxLength(20);
            phase.Property(p => p.Pattern).HasConversion<string>().HasMaxLength(20);
            phase.Property(p => p.Gate).HasConversion<string>().HasMaxLength(20);
            phase.Property(p => p.Sandbox).HasConversion<string>().HasMaxLength(20);
            phase.Property(p => p.Instructions).HasMaxLength(ProjectRules.LongestInstructions);
            phase.HasOne<Project>().WithMany().HasForeignKey(p => p.ProjectId);
            phase.HasOne<Agent>().WithMany()
                .HasForeignKey(p => new { p.ProjectId, p.CastAgentId })
                .HasPrincipalKey(a => new { a.ProjectId, a.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
