using Sofa.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Intake, eventsSubscribedTo: [SofaEvents.GitHubEventReceived]);

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
