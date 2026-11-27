using Sofa.AgentGateway.Runs;
using Sofa.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.AgentGateway);
builder.AcceptTokensFor(SofaServices.AgentGateway);
builder.Services.AddRunTokenPolicy();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapRunTokenCheck();

app.Run();
