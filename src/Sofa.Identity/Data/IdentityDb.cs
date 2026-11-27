using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenIddict.EntityFrameworkCore.Models;
using Sofa.Contracts;
using Sofa.Identity.Agents;
using Sofa.Identity.Setup;
using Sofa.Identity.Tokens;
using Sofa.Identity.Users;
using Sofa.Identity.Workspaces;

namespace Sofa.Identity.Data;

public sealed class IdentityDb(DbContextOptions<IdentityDb> options) : DbContext(options), IDataProtectionKeyContext
{
    public static readonly string Schema = SofaServices.SchemaOf(SofaServices.Identity);

    public DbSet<Installation> Installations => Set<Installation>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkspaceUser> WorkspaceUsers => Set<WorkspaceUser>();
    public DbSet<TeamUser> TeamUsers => Set<TeamUser>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<TokenKey> TokenKeys => Set<TokenKey>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public static void Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        Configure(options, npgsql => npgsql.UseNpgsql(dataSource, MigrationsInOwnSchema));

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        Configure(options, npgsql => npgsql.UseNpgsql(connectionString, MigrationsInOwnSchema));

    private static void Configure(DbContextOptionsBuilder options, Action<DbContextOptionsBuilder> useNpgsql)
    {
        useNpgsql(options);
        options.UseSnakeCaseNamingConvention();
    }

    private static void MigrationsInOwnSchema(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable("ef_migrations_history", Schema);

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);

        model.UseOpenIddict<Guid>();
        model.Entity<OpenIddictEntityFrameworkCoreApplication<Guid>>().ToTable("openiddict_applications");
        model.Entity<OpenIddictEntityFrameworkCoreAuthorization<Guid>>().ToTable("openiddict_authorizations");
        model.Entity<OpenIddictEntityFrameworkCoreScope<Guid>>().ToTable("openiddict_scopes");
        model.Entity<OpenIddictEntityFrameworkCoreToken<Guid>>().ToTable("openiddict_tokens");

        model.Entity<Installation>(installation =>
        {
            installation.ToTable("installation", table =>
                table.HasCheckConstraint("ck_installation_only_one", $"id = {Installation.TheOnlyOne}"));
            installation.Property(i => i.Id).ValueGeneratedNever();
            installation.Ignore(i => i.IsSetUp);
        });

        model.Entity<Workspace>(workspace => workspace.Property(w => w.Name).HasMaxLength(200));

        model.Entity<Team>(team =>
        {
            team.Property(t => t.Name).HasMaxLength(200);
            team.HasOne<Workspace>().WithMany().HasForeignKey(t => t.WorkspaceId);
            team.HasAlternateKey(t => new { t.WorkspaceId, t.Id });
        });

        model.Entity<User>(user =>
        {
            user.Property(u => u.Name).HasMaxLength(200);
            user.Property(u => u.Email).HasMaxLength(320);
            user.Property(u => u.NormalizedEmail).HasMaxLength(320);
            user.HasIndex(u => u.NormalizedEmail).IsUnique();
        });

        model.Entity<WorkspaceUser>(workspaceUser =>
        {
            workspaceUser.HasKey(w => new { w.WorkspaceId, w.UserId });
            workspaceUser.HasOne<Workspace>().WithMany().HasForeignKey(w => w.WorkspaceId);
            workspaceUser.HasOne<User>().WithMany().HasForeignKey(w => w.UserId);
        });

        model.Entity<TeamUser>(teamUser =>
        {
            teamUser.HasKey(t => new { t.TeamId, t.UserId });
            teamUser.HasOne<Team>().WithMany()
                .HasForeignKey(t => new { t.WorkspaceId, t.TeamId })
                .HasPrincipalKey(t => new { t.WorkspaceId, t.Id });
            teamUser.HasOne<WorkspaceUser>().WithMany()
                .HasForeignKey(t => new { t.WorkspaceId, t.UserId });
        });

        model.Entity<Agent>(agent =>
        {
            agent.Property(a => a.Id).ValueGeneratedNever();
            agent.Property(a => a.Name).HasMaxLength(200);
            agent.Ignore(a => a.Owner);
            agent.ToTable(table => table.HasCheckConstraint(
                "ck_agents_owned_by_a_user_or_a_team",
                "(owner_user_id is null) <> (owner_team_id is null)"));
            agent.HasOne<Workspace>().WithMany().HasForeignKey(a => a.WorkspaceId);
            agent.HasOne<WorkspaceUser>().WithMany()
                .HasForeignKey(a => new { a.WorkspaceId, a.OwnerUserId })
                .IsRequired(false);
            agent.HasOne<Team>().WithMany()
                .HasForeignKey(a => new { a.WorkspaceId, a.OwnerTeamId })
                .HasPrincipalKey(t => new { t.WorkspaceId, t.Id })
                .IsRequired(false);
        });

        model.Entity<TokenKey>(key => key.Property(k => k.Use).HasConversion<string>().HasMaxLength(20));
    }
}
