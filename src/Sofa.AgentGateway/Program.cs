using Sofa.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.AgentGateway);

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
