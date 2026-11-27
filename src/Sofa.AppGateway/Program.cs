using Sofa.AppGateway;
using Sofa.AppGateway.Admins;
using Sofa.AppGateway.Connections;
using Sofa.Contracts;
using Sofa.Contracts.Connections;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.AppGateway, options =>
{
    options.PublishMessage<StartGitHubAppCreation>().ToRabbitQueue(SofaServices.Connections);
    options.PublishMessage<FinishGitHubAppCreation>().ToRabbitQueue(SofaServices.Connections);
    options.PublishMessage<AddGitHubInstallation>().ToRabbitQueue(SofaServices.Connections);
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IFindSignedInAdmin, EveryoneIsAdminOfOneWorkspaceUntilSignIn>();
builder.Services.AddHttpClient<ConnectionsClient>(http => http.BaseAddress = ConnectionsClient.Address);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();
var proxiesWebClient = builder.Configuration.GetSection("ReverseProxy:Routes:web").Exists();

var app = builder.Build();

app.UseExceptionHandler();
app.UseRouting();
app.UseAdminsOnly();

app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapGroup("/api").MapStatus();

var admins = app.MapAdminsOnly();
admins.MapGroup("/api").MapGitHubAppApi();
admins.MapGitHubAppReturns();

app.MapReverseProxy();

if (!proxiesWebClient)
{
    app.UseFileServer();
    app.MapFallbackToFile("index.html");
}

app.Run();
