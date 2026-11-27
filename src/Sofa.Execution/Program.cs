using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Execution.Data;
using Sofa.Execution.Runners;
using Sofa.Execution.Runs;
using Sofa.Execution.Workers;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Records;
using Wolverine.RabbitMQ;
using JasperFx.CodeGeneration.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Execution, options =>
{
    options.PublishEvent<RunEnded>(SofaEvents.RunEnded);
    options.PublishEvent<SubscriptionSignedOut>(SofaEvents.SubscriptionSignedOut);
    options.PublishMessage<AppendTranscript>().ToRabbitQueue(SofaServices.Records);
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.AlwaysUseServiceLocationFor<ExecutionDb>();
});

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ExecutionDb>((services, options) =>
    ExecutionDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddOptions<RunTokenClientOptions>().BindConfiguration(RunTokenClientOptions.Section);
builder.Services.AddHttpClient<RunTokenClient>(identity => identity.BaseAddress = RunTokenClient.IdentityAddress);
builder.Services.AddHttpClient<IdentityTeams>(identity => identity.BaseAddress = IdentityTeams.Address);
builder.Services.AddHttpClient<ConnectionsApiKeys>(connections => connections.BaseAddress = ConnectionsApiKeys.Address);
builder.Services.AddOptions<WorkerOptions>().BindConfiguration(WorkerOptions.Section);
builder.Services.AddSingleton<IWorkerContainers, DockerWorkerContainers>();
builder.Services.AddSingleton<RunQueueSignal>();
builder.Services.AddSingleton<RunChanges>();
builder.Services.AddSingleton<RunsInContainers>();
builder.Services.AddHostedService<RunQueue>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ExecutionDb>().Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.MapRunTokenDiagnostics();
app.MapRunners();
app.MapRuns();

app.Run();
