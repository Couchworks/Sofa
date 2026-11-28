using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Contracts.Diagnostics;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Identity;
using Sofa.Contracts.Intake;
using Sofa.Flow.Agents;
using Sofa.Flow.Data;
using Sofa.Flow.Diagnostics;
using Sofa.Contracts.Code;
using Sofa.Flow.Projects;
using Sofa.Flow.Reports;
using Sofa.Flow.Runs;
using Sofa.Flow.WorkItems;
using JasperFx.CodeGeneration.Model;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Flow, options =>
{
    options.PublishMessage<Ping>().ToRabbitQueue(SofaServices.Execution);
    options.PublishMessage<RegisterAgent>().ToRabbitQueue(SofaServices.Identity);
    options.PublishMessage<StartRun>().ToRabbitQueue(SofaServices.Execution);
    options.PublishMessage<AskOnRequest>().ToRabbitQueue(SofaServices.Intake);
    options.PublishMessage<PostReport>().ToRabbitQueue(SofaServices.Intake);
    options.PublishMessage<ReportPhaseOnRequest>().ToLocalQueue(ReportsQueue).UseDurableInbox();
    options.PublishEvent<ProjectBound>(SofaEvents.ProjectBound);
    options.PublishEvent<WorkItemEnteredPhase>(SofaEvents.WorkItemEnteredPhase);
    options.PublishEvent<WorkItemReachedMainBranch>(SofaEvents.WorkItemReachedMainBranch);
    options.PublishEvent<WorkItemAwaitsUser>(SofaEvents.WorkItemAwaitsUser);
    options.PublishEvent<WorkItemNoLongerAwaitsUser>(SofaEvents.WorkItemNoLongerAwaitsUser);
    options.Policies.OnException<DbUpdateConcurrencyException>().RetryTimes(5);
    options.Policies.OnException<HttpRequestException>().RetryWithCooldown(
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.AlwaysUseServiceLocationFor<FlowDb>();
}, eventsSubscribedTo:
[
    SofaEvents.QuestionAnsweredOnRequest,
    SofaEvents.RunEnded,
    SofaEvents.PullRequestOpened,
    SofaEvents.PullRequestMerged,
    SofaEvents.PullRequestClosedUnmerged,
]);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RoundTrips>();
builder.Services.AddDbContext<FlowDb>((services, options) =>
    FlowDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddHostedService<PendingRegistrationsOnStart>();
builder.Services.AddHttpClient<ConnectionsRepositories>(connections => connections.BaseAddress = ConnectionsRepositories.Address);
builder.Services.AddHostedService<RepublishBindingsOnStart>();
builder.Services.AddScoped<WorkItemMoves>();
builder.Services.AddHttpClient<RecordsContributions>(records => records.BaseAddress = RecordsContributions.Address);

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
app.MapTestRuns();

app.Run();

public partial class Program
{
    private const string ReportsQueue = "reports";
}
