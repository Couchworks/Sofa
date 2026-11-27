using Sofa.AppGateway;
using Sofa.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.AppGateway);
builder.AcceptTokensFor(SofaServices.AppGateway);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IdentityUsers>(identity =>
    identity.BaseAddress = new Uri($"http://{SofaServices.Identity}"));

var webClient = builder.Configuration.GetSection("ReverseProxy");
var proxiesWebClient = webClient.Exists();
var proxy = builder.Services.AddReverseProxy()
    .LoadFromMemory(IdentityRoutes.Routes, IdentityRoutes.Clusters);
if (proxiesWebClient)
{
    proxy.LoadFromConfig(webClient);
}
proxy.AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapDefaultEndpoints();
var api = app.MapGroup("/api");
api.MapStatus();
api.MapSignedInUser();

app.MapReverseProxy();

if (!proxiesWebClient)
{
    app.UseFileServer();
    app.MapFallbackToFile("index.html");
}

app.Run();
