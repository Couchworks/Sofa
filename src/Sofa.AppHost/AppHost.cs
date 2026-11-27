using Aspire.Hosting.Dotnet;
using Sofa.Contracts;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("sofa");
var rabbitmq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();
var executionClientSecret = builder.AddParameter(
    "execution-client-secret",
    new GenerateParameterDefault { MinLength = 32, Special = false },
    secret: true,
    persist: true);

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
    .PublishWithContainerFiles(web, "wwwroot");

var identity = AddService(SofaServices.Identity, "Sofa.Identity")
    .WithEnvironment(context => PublicAddresses.TellIdentity(context, appGateway))
    .WithEnvironment(context => PublicAddresses.TellIssuer(context, appGateway))
    .WithEnvironment("Identity__ExecutionClientSecret", executionClientSecret);

appGateway.WithReference(identity)
    .WithEnvironment(context => PublicAddresses.TellIssuer(context, appGateway));

AddService(SofaServices.AgentGateway, "Sofa.AgentGateway")
    .WithReference(identity)
    .WithEnvironment(context => PublicAddresses.TellIssuer(context, appGateway))
    .WithExternalHttpEndpoints();
AddService(SofaServices.Connections, "Sofa.Connections");
AddService(SofaServices.Intake, "Sofa.Intake");
AddService(SofaServices.Flow, "Sofa.Flow");
AddService(SofaServices.Execution, "Sofa.Execution")
    .WithReference(identity)
    .WithEnvironment("Execution__ClientSecret", executionClientSecret);
AddService(SofaServices.Records, "Sofa.Records");
AddService(SofaServices.Code, "Sofa.Code");
AddService(SofaServices.Knowledge, "Sofa.Knowledge");
AddService(SofaServices.Notices, "Sofa.Notices");

builder.Build().Run();

static class PublicAddresses
{
    public static void TellIdentity(EnvironmentCallbackContext context, IResourceBuilder<DotnetProjectResource> appGateway)
    {
        var addresses = Of(appGateway);
        for (var index = 0; index < addresses.Count; index++)
        {
            context.EnvironmentVariables[$"Identity__WebClientOrigins__{index}"] = addresses[index];
        }
    }

    public static void TellIssuer(EnvironmentCallbackContext context, IResourceBuilder<DotnetProjectResource> appGateway) =>
        context.EnvironmentVariables["Identity__Issuer"] = Of(appGateway)[0];

    private static List<EndpointReference> Of(IResourceBuilder<DotnetProjectResource> appGateway) =>
        appGateway.Resource.Annotations.OfType<EndpointAnnotation>()
            .Where(endpoint => endpoint.UriScheme is "http" or "https")
            .OrderByDescending(endpoint => endpoint.UriScheme == "https")
            .Select(endpoint => appGateway.GetEndpoint(endpoint.Name))
            .ToList();
}
