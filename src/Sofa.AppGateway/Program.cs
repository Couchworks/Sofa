using Sofa.AppGateway;
using Sofa.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.AppGateway);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);

var webClient = builder.Configuration.GetSection("ReverseProxy");
var proxiesWebClient = webClient.Exists();
if (proxiesWebClient)
{
    builder.Services.AddReverseProxy()
        .LoadFromConfig(webClient)
        .AddServiceDiscoveryDestinationResolver();
}

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapGroup("/api").MapStatus();

if (proxiesWebClient)
{
    app.MapReverseProxy();
}
else
{
    app.UseFileServer();
    app.MapFallbackToFile("index.html");
}

app.Run();
