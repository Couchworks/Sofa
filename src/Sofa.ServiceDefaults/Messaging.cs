using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sofa.Contracts;
using Wolverine;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Wolverine as every Sofa service uses it (ADRs 0013, 0014, 0023): RabbitMQ as the broker, an inbox
/// and outbox in the service's own Postgres schema, and one durable queue named after the service
/// that takes the commands sent to it and the replies to what it sent.
/// </summary>
public static class Messaging
{
    /// <summary>The Aspire name of the Postgres database every service keeps its schema in.</summary>
    public const string DatabaseName = "sofa";

    /// <summary>The Aspire name of the RabbitMQ broker.</summary>
    public const string BrokerName = "rabbitmq";

    public static TBuilder AddSofaMessaging<TBuilder>(
        this TBuilder builder,
        string service,
        Action<WolverineOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        if (IsGeneratingOpenApiDocument)
        {
            // Building the OpenAPI description at build time starts the host, without Postgres or RabbitMQ.
            builder.UseWolverine(options =>
            {
                options.ServiceName = service;
                options.Durability.Mode = DurabilityMode.MediatorOnly;
                options.UseRuntimeCompilation();
            });
            return builder;
        }

        builder.AddNpgsqlDataSource(DatabaseName);

        builder.UseWolverine(options =>
        {
            options.ServiceName = service;
            // Called from this shared library, Wolverine would look for handlers here; they live in the service.
            options.ApplicationAssembly = Assembly.GetEntryAssembly();
            options.UseRuntimeCompilation();

            options.PersistMessagesWithPostgresqlUsingDataSourceFromContainer(
                services => services.GetRequiredService<NpgsqlDataSource>(),
                SofaServices.SchemaOf(service));

            options.UseRabbitMqUsingNamedConnection(BrokerName)
                .AutoProvision()
                .DisableSystemRequestReplyQueueDeclaration();

            options.ListenToRabbitQueue(service).UseForReplies();

            options.Policies.UseDurableInboxOnAllListeners();
            options.Policies.UseDurableOutboxOnAllSendingEndpoints();

            configure?.Invoke(options);
        });

        return builder;
    }

    private static bool IsGeneratingOpenApiDocument =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
