using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sofa.Contracts;
using Wolverine;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace Microsoft.Extensions.Hosting;

public static class Messaging
{
    public const string DatabaseName = "sofa";

    public const string BrokerName = "rabbitmq";

    public static TBuilder AddSofaMessaging<TBuilder>(
        this TBuilder builder,
        string service,
        Action<WolverineOptions>? configure = null)
        where TBuilder : IHostApplicationBuilder
    {
        if (IsGeneratingOpenApiDocument)
        {
            return builder.UseWolverineWithoutBrokerOrDatabase(service);
        }

        builder.AddNpgsqlDataSource(DatabaseName);

        builder.UseWolverine(options =>
        {
            options.ServiceName = service;
            options.ApplicationAssembly = ServiceAssemblyWithTheHandlers;
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

    private static TBuilder UseWolverineWithoutBrokerOrDatabase<TBuilder>(this TBuilder builder, string service)
        where TBuilder : IHostApplicationBuilder
    {
        builder.UseWolverine(options =>
        {
            options.ServiceName = service;
            options.Durability.Mode = DurabilityMode.MediatorOnly;
            options.UseRuntimeCompilation();
        });
        return builder;
    }

    private static Assembly? ServiceAssemblyWithTheHandlers => Assembly.GetEntryAssembly();

    private static bool IsGeneratingOpenApiDocument =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
