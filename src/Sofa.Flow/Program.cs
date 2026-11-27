using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Contracts.Diagnostics;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Identity;
using Sofa.Flow.Agents;
using Sofa.Flow.Data;
using Sofa.Flow.Diagnostics;
using Sofa.Flow.Projects;
using Sofa.Flow.WorkItems;
using JasperFx.CodeGeneration.Model;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Flow, options =>
{
    options.PublishMessage<Ping>().ToRabbitQueue(SofaServices.Execution);
    options.PublishMessage<RegisterAgent>().ToRabbitQueue(SofaServices.Identity);
    options.PublishEvent<ProjectBound>(SofaEvents.ProjectBound);
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.AlwaysUseServiceLocationFor<FlowDb>();
}, eventsSubscribedTo: [SofaEvents.QuestionAnsweredOnRequest]);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoundTrips>();
builder.Services.AddDbContext<FlowDb>((services, options) =>
    FlowDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddHttpClient<ConnectionsRepositories>(connections => connections.BaseAddress = ConnectionsRepositories.Address);
builder.Services.AddHostedService<RepublishBindingsOnStart>();

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
app.MapWorkItems();

app.Run();
