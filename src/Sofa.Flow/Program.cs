using Sofa.Contracts;
using Sofa.Contracts.Diagnostics;
using Sofa.Contracts.Identity;
using Sofa.Flow.Agents;
using Sofa.Flow.Diagnostics;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Flow, options =>
{
    options.PublishMessage<Ping>().ToRabbitQueue(SofaServices.Execution);
    options.PublishMessage<RegisterAgent>().ToRabbitQueue(SofaServices.Identity);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoundTrips>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapRoundTrips();
app.MapAgentDiagnostics();

app.Run();
