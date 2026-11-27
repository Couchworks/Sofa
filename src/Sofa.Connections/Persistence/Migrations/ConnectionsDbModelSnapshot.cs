using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Sofa.Connections.Persistence;

#nullable disable

namespace Sofa.Connections.Persistence.Migrations;

[DbContext(typeof(ConnectionsDb))]
partial class ConnectionsDbModelSnapshot : ModelSnapshot
{
    public override string LastMigrationId => "20261127152235_AddGitHubAppAndConnections";

    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasDefaultSchema("connections")
            .HasAnnotation("ProductVersion", "11.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("Sofa.Connections.Persistence.Connection", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("Account")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("account");

                b.Property<string>("AccountType")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("account_type");

                b.Property<DateTimeOffset>("AddedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("added_at");

                b.Property<long>("GitHubInstallationId")
                    .HasColumnType("bigint")
                    .HasColumnName("github_installation_id");

                b.Property<DateTimeOffset?>("SuspendedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("suspended_at");

                b.Property<string>("System")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("system");

                b.Property<Guid>("WorkspaceId")
                    .HasColumnType("uuid")
                    .HasColumnName("workspace_id");

                b.HasKey("Id")
                    .HasName("pk_connections");

                b.HasIndex("GitHubInstallationId")
                    .IsUnique()
                    .HasDatabaseName("ix_connections_github_installation_id");

                b.HasIndex("WorkspaceId")
                    .HasDatabaseName("ix_connections_workspace_id");

                b.ToTable("connections", "connections");
            });

        modelBuilder.Entity("Sofa.Connections.Persistence.GitHubApp", b =>
            {
                b.Property<long>("Id")
                    .HasColumnType("bigint")
                    .HasColumnName("id");

                b.Property<string>("ClientId")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("client_id");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                b.Property<DateTimeOffset?>("DeliveriesCheckedUntil")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("deliveries_checked_until");

                b.Property<string>("HtmlUrl")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("html_url");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("name");

                b.Property<string>("OwnerLogin")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("owner_login");

                b.Property<string>("SealedClientSecret")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("sealed_client_secret");

                b.Property<string>("SealedPrivateKey")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("sealed_private_key");

                b.Property<string>("SealedWebhookSecret")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("sealed_webhook_secret");

                b.Property<string>("Slug")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("slug");

                b.HasKey("Id")
                    .HasName("pk_github_apps");

                b.ToTable("github_apps", "connections");
            });

        modelBuilder.Entity("Sofa.Connections.Persistence.GitHubAppCreation", b =>
            {
                b.Property<string>("State")
                    .HasColumnType("text")
                    .HasColumnName("state");

                b.Property<DateTimeOffset>("ExpiresAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("expires_at");

                b.Property<Guid>("WorkspaceId")
                    .HasColumnType("uuid")
                    .HasColumnName("workspace_id");

                b.HasKey("State")
                    .HasName("pk_github_app_creations");

                b.ToTable("github_app_creations", "connections");
            });

        modelBuilder.Entity("Sofa.Connections.Persistence.ReceivedDelivery", b =>
            {
                b.Property<Guid>("DeliveryId")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid")
                    .HasColumnName("delivery_id");

                b.Property<DateTimeOffset>("ReceivedAt")
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("received_at");

                b.HasKey("DeliveryId")
                    .HasName("pk_received_deliveries");

                b.HasIndex("ReceivedAt")
                    .HasDatabaseName("ix_received_deliveries_received_at");

                b.ToTable("received_deliveries", "connections");
            });
#pragma warning restore 612, 618
    }
}
