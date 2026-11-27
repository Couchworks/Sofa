using Sofa.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Records);

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
