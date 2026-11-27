using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Contracts.Diagnostics;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Identity;
using Sofa.Flow.Agents;
using Sofa.Flow.Data;
using Sofa.Flow.Diagnostics;
using Sofa.Flow.Projects;
using Sofa.Flow.Runs;
using JasperFx.CodeGeneration.Model;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Flow, options =>
{
    options.PublishMessage<Ping>().ToRabbitQueue(SofaServices.Execution);
    options.PublishMessage<RegisterAgent>().ToRabbitQueue(SofaServices.Identity);
    options.PublishMessage<StartRun>().ToRabbitQueue(SofaServices.Execution);
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.AlwaysUseServiceLocationFor<FlowDb>();
});

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoundTrips>();
builder.Services.AddDbContext<FlowDb>((services, options) =>
    FlowDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddHostedService<PendingRegistrationsOnStart>();
builder.Services.AddHttpClient<ConnectionsRepositories>(connections => connections.BaseAddress = ConnectionsRepositories.Address);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<FlowDb>().Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.MapRoundTrips();
app.MapAgentDiagnostics();
app.MapProjects();
app.MapTestRuns();

app.Run();
