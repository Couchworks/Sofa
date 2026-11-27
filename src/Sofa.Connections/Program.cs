using Sofa.Connections;
using Sofa.Connections.GitHub;
using Sofa.Connections.Persistence;
using Sofa.Contracts;
using Sofa.Contracts.Connections;
using JasperFx.CodeGeneration.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Connections, options =>
{
    options.PublishEvent<GitHubEventReceived>(SofaEvents.GitHubEventReceived);
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
});

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddConnectionsDb();
builder.Services.AddCredentialVault(builder.Configuration);
builder.Services.AddGitHub(builder.Configuration);

var app = builder.Build();

await app.Services.MigrateConnectionsDb();

app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.MapConnections();
app.MapGitHubWebhooks();
app.MapGitHubSignIn();

app.Run();
