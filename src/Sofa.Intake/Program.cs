using JasperFx.CodeGeneration.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Sofa.Contracts;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Intake;
using Sofa.Intake;
using Sofa.Intake.Data;
using Sofa.Intake.Diagnostics;
using Sofa.Intake.GitHub;
using Sofa.Intake.Requests;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Intake, options =>
{
    options.PublishMessage<OpenWorkItem>().ToRabbitQueue(SofaServices.Flow);
    options.PublishEvent<RequestAwaitingDelegation>(SofaEvents.RequestAwaitingDelegation);
    options.PublishEvent<QuestionAnsweredOnRequest>(SofaEvents.QuestionAnsweredOnRequest);
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.AlwaysUseServiceLocationFor<IntakeDb>();
}, eventsSubscribedTo:
[
    SofaEvents.GitHubEventReceived,
    SofaEvents.ProjectBound,
    SofaEvents.WorkItemEnteredPhase,
    SofaEvents.WorkItemReachedMainBranch,
]);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<IntakeDb>((services, options) =>
    IntakeDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));

builder.Services.AddOptions<GitHubApiOptions>().BindConfiguration(GitHubApiOptions.Section);
builder.Services.AddOptions<PublicAddressOptions>()
    .BindConfiguration(PublicAddressOptions.Section)
    .Validate(options => options.PublicAddress is { IsAbsoluteUri: true },
        $"{PublicAddressOptions.Section}:{nameof(PublicAddressOptions.PublicAddress)} must be the absolute address " +
        "where browsers reach Sofa's App Gateway, for the links in Reports.")
    .ValidateOnStart();
builder.Services.AddSingleton(services =>
    new SofaAddress(services.GetRequiredService<IOptions<PublicAddressOptions>>().Value.PublicAddress!));

builder.Services.AddHttpClient<ConnectionsTokens>(connections => connections.BaseAddress = ConnectionsTokens.Address);
builder.Services.AddHttpClient<IdentityGitHubAccounts>(identity => identity.BaseAddress = IdentityGitHubAccounts.Address);
builder.Services.AddHttpClient<GitHubIssues>((services, gitHub) =>
    GitHubIssues.Configure(gitHub, services.GetRequiredService<IOptions<GitHubApiOptions>>().Value));
builder.Services.AddTransient<IRequestReporter>(services => services.GetRequiredService<GitHubIssues>());
builder.Services.AddTransient<IRequestState>(services => services.GetRequiredService<GitHubIssues>());

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<IntakeDb>().Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.MapDiagnostics();

app.Run();
