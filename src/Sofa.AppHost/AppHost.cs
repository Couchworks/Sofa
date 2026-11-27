using Aspire.Hosting.Dotnet;
using Sofa.Contracts;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("sofa");
var rabbitmq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();

var keyEncryptionKey = builder.AddParameter(
    "connections-key-encryption-key",
    new GenerateParameterDefault { MinLength = 48, Special = false },
    secret: true,
    persist: true);
var publicAddress = builder.Configuration["Sofa:PublicAddress"];

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

var connections = AddService(SofaServices.Connections, "Sofa.Connections")
    .WithEnvironment("Credentials__KeyEncryptionKey", keyEncryptionKey);
if (string.IsNullOrWhiteSpace(publicAddress))
{
    connections.WithEnvironment("Sofa__PublicAddress", appGateway.GetEndpoint("http"));
}
else
{
    connections.WithEnvironment("Sofa__PublicAddress", publicAddress);
}

appGateway.WithReference(connections);

AddService(SofaServices.AgentGateway, "Sofa.AgentGateway").WithExternalHttpEndpoints();
AddService(SofaServices.Identity, "Sofa.Identity");
AddService(SofaServices.Intake, "Sofa.Intake");
AddService(SofaServices.Flow, "Sofa.Flow");
AddService(SofaServices.Execution, "Sofa.Execution");
AddService(SofaServices.Records, "Sofa.Records");
AddService(SofaServices.Code, "Sofa.Code");
AddService(SofaServices.Knowledge, "Sofa.Knowledge");
AddService(SofaServices.Notices, "Sofa.Notices");

builder.Build().Run();
