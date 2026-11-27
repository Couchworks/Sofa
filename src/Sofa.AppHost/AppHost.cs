using Aspire.Hosting.Dotnet;
using Sofa.Contracts;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("sofa");
var rabbitmq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();

IResourceBuilder<DotnetProjectResource> AddService(string name, string project) =>
    builder.AddDotnetProject(name, $"../{project}/{project}.csproj")
        .WithReference(database).WaitFor(database)
        .WithReference(rabbitmq).WaitFor(rabbitmq)
        .WithHttpHealthCheck("/health");

var web = builder.AddViteApp("web", "../../web")
    .WithNpm(install: true);

AddService(SofaServices.AppGateway, "Sofa.AppGateway")
    .WithReference(web.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .PublishWithContainerFiles(web, "wwwroot");

AddService(SofaServices.AgentGateway, "Sofa.AgentGateway").WithExternalHttpEndpoints();
AddService(SofaServices.Identity, "Sofa.Identity");
AddService(SofaServices.Connections, "Sofa.Connections");
AddService(SofaServices.Intake, "Sofa.Intake");
AddService(SofaServices.Flow, "Sofa.Flow");
AddService(SofaServices.Execution, "Sofa.Execution");
AddService(SofaServices.Records, "Sofa.Records");
AddService(SofaServices.Code, "Sofa.Code");
AddService(SofaServices.Knowledge, "Sofa.Knowledge");
AddService(SofaServices.Notices, "Sofa.Notices");

builder.Build().Run();
