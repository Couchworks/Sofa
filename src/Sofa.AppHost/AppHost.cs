using Aspire.Hosting.Dotnet;
using Sofa.Contracts;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
if (!bool.TryParse(builder.Configuration[SofaAppHostSettings.FreshDatabaseEachStart], out var freshDatabase) || !freshDatabase)
{
    postgres.WithDataVolume();
}

var database = postgres.AddDatabase("sofa");
var rabbitmq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();
var executionClientSecret = builder.AddParameter(
    "execution-client-secret",
    new GenerateParameterDefault { MinLength = 32, Special = false },
    secret: true,
    persist: true);
var executionsSecretForConnections = builder.AddParameter(
    "execution-connections-secret",
    new GenerateParameterDefault { MinLength = 32, Special = false },
    secret: true,
    persist: true);
var keyEncryptionKey = builder.AddParameter(
    "connections-key-encryption-key",
    new GenerateParameterDefault { MinLength = 48, Special = false },
    secret: true,
    persist: true);
var gitHubReturnSigningKey = builder.AddParameter(
    "app-gateway-github-return-signing-key",
    new GenerateParameterDefault { MinLength = 48, Special = false },
    secret: true,
    persist: true);
var configuredPublicAddress = builder.Configuration[SofaAppHostSettings.PublicAddress];

IResourceBuilder<DotnetProjectResource> AddService(string name, string project) =>
    builder.AddDotnetProject(name, $"../{project}/{project}.csproj")
        .WithReference(database).WaitFor(database)
        .WithReference(rabbitmq).WaitFor(rabbitmq)
        .WithHttpHealthCheck("/health");

var web = builder.AddViteApp("web", "../../web")
    .WithNpm(install: true);

var appGateway = AddService(SofaServices.AppGateway, "Sofa.AppGateway")
    .WithReference(web.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .PublishWithContainerFiles(web, "wwwroot")
    .WithEnvironment("GitHubReturns__SigningKey", gitHubReturnSigningKey);

var publicAddress = new PublicAddress(configuredPublicAddress, appGateway);

var connections = AddService(SofaServices.Connections, "Sofa.Connections")
    .WithEnvironment("Credentials__KeyEncryptionKey", keyEncryptionKey)
    .WithEnvironment("ApiKeys__ExecutionSecret", executionsSecretForConnections)
    .WithEnvironment(publicAddress.TellPublicAddress);

var identity = AddService(SofaServices.Identity, "Sofa.Identity")
    .WithReference(connections)
    .WithEnvironment(publicAddress.TellWebClientOrigins)
    .WithEnvironment(publicAddress.TellIssuer)
    .WithEnvironment("Identity__ExecutionClientSecret", executionClientSecret);

var flow = AddService(SofaServices.Flow, "Sofa.Flow")
    .WithReference(connections);
var records = AddService(SofaServices.Records, "Sofa.Records");
var execution = AddService(SofaServices.Execution, "Sofa.Execution")
    .WithReference(identity)
    .WithReference(connections)
    .WithEnvironment("Execution__ClientSecret", executionClientSecret)
    .WithEnvironment("Execution__ConnectionsSecret", executionsSecretForConnections);

var agentGateway = AddService(SofaServices.AgentGateway, "Sofa.AgentGateway")
    .WithReference(identity)
    .WithReference(execution)
    .WithEnvironment(publicAddress.TellIssuer)
    .WithExternalHttpEndpoints()
    .WithEndpoint("http", reachedByWorkerContainers => reachedByWorkerContainers.TargetHost = "0.0.0.0");

execution.WithEnvironment("Workers__AgentGatewayAddress", ReferenceExpression.Create(
    $"http://{WorkerContainers.ReachTheBoxAt}:{agentGateway.GetEndpoint("http").Property(EndpointProperty.Port)}"));

appGateway.WithReference(identity)
    .WithReference(connections)
    .WithReference(flow)
    .WithReference(execution)
    .WithReference(records)
    .WithEnvironment(publicAddress.TellIssuer);

AddService(SofaServices.Intake, "Sofa.Intake")
    .WithReference(connections)
    .WithReference(identity)
    .WithEnvironment(publicAddress.TellPublicAddress);
AddService(SofaServices.Code, "Sofa.Code");
AddService(SofaServices.Knowledge, "Sofa.Knowledge");
AddService(SofaServices.Notices, "Sofa.Notices");

builder.Build().Run();

static class WorkerContainers
{
    public const string ReachTheBoxAt = "host.docker.internal";
}

static class SofaAppHostSettings
{
    public const string PublicAddress = "Sofa:PublicAddress";
    public const string FreshDatabaseEachStart = "Sofa:FreshDatabaseEachStart";
}

sealed class PublicAddress(string? configured, IResourceBuilder<DotnetProjectResource> appGateway)
{
    private bool IsConfigured => !string.IsNullOrWhiteSpace(configured);

    public void TellPublicAddress(EnvironmentCallbackContext context) =>
        context.EnvironmentVariables["Sofa__PublicAddress"] = IsConfigured ? configured! : appGateway.GetEndpoint("http");

    public void TellIssuer(EnvironmentCallbackContext context) =>
        context.EnvironmentVariables["Identity__Issuer"] = IsConfigured ? configured! : AppGatewaysOwnAddresses()[0];

    public void TellWebClientOrigins(EnvironmentCallbackContext context)
    {
        List<object> origins = [.. AppGatewaysOwnAddresses()];
        if (IsConfigured)
        {
            origins.Insert(0, configured!);
        }

        for (var index = 0; index < origins.Count; index++)
        {
            context.EnvironmentVariables[$"Identity__WebClientOrigins__{index}"] = origins[index];
        }
    }

    private List<EndpointReference> AppGatewaysOwnAddresses() =>
        appGateway.Resource.Annotations.OfType<EndpointAnnotation>()
            .Where(endpoint => endpoint.UriScheme is "http" or "https")
            .OrderByDescending(endpoint => endpoint.UriScheme == "https")
            .Select(endpoint => appGateway.GetEndpoint(endpoint.Name))
            .ToList();
}
