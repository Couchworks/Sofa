using Microsoft.AspNetCore.HttpOverrides;
using Sofa.AppGateway;
using Sofa.AppGateway.Admins;
using Sofa.AppGateway.Connections;
using Sofa.AppGateway.GitHubReturns;
using Sofa.AppGateway.Identity;
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
builder.AcceptTokensFor(SofaServices.AppGateway);

builder.Services.Configure<ForwardedHeadersOptions>(theTunnelOrProxyInFront =>
{
    theTunnelOrProxyInFront.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    theTunnelOrProxyInFront.KnownIPNetworks.Clear();
    theTunnelOrProxyInFront.KnownProxies.Clear();
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IdentityUsers>(identity => identity.BaseAddress = IdentityUsers.Address);
builder.Services.AddScoped<WorkspaceAdmins>();
builder.Services.AddScoped<IFindSignedInAdmin, AdminOfTheWorkspaceInTheRoute>();
builder.Services.AddHttpClient<ConnectionsClient>(http => http.BaseAddress = ConnectionsClient.Address);
builder.Services.AddOptions<GitHubReturnOptions>().BindConfiguration(GitHubReturnOptions.Section);
builder.Services.AddSingleton<GitHubReturnStates>();

builder.Services.AddReverseProxy()
    .LoadFromMemory(IdentityRoutes.Routes, IdentityRoutes.Clusters)
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();
var proxiesWebClient = builder.Configuration.GetSection("ReverseProxy:Routes:web").Exists();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAdminsOnly();

app.MapOpenApi();
app.MapDefaultEndpoints();
var api = app.MapGroup("/api");
api.MapStatus();
api.MapSignedInUser();

app.MapAdminsOnly()
    .MapGitHubAppApi()
    .MapWorkspaceUsers();
app.MapGitHubAppReturns();

app.MapReverseProxy();

if (!proxiesWebClient)
{
    app.UseFileServer();
    app.MapFallbackToFile("index.html");
}

app.Run();
