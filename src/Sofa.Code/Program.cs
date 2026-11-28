using JasperFx.CodeGeneration.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Sofa.Code.Bindings;
using Sofa.Code.Data;
using Sofa.Code.GitHub;
using Sofa.Code.PullRequests;
using Sofa.Code.Repositories;
using Sofa.Contracts;
using Sofa.Contracts.Code;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Code, options =>
{
    options.PublishEvent<PullRequestOpened>(SofaEvents.PullRequestOpened);
    options.PublishEvent<PullRequestMerged>(SofaEvents.PullRequestMerged);
    options.PublishEvent<PullRequestClosedUnmerged>(SofaEvents.PullRequestClosedUnmerged);
    options.PublishEvent<PushOutsideItsSandboxCaught>(SofaEvents.PushOutsideItsSandboxCaught);
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.AlwaysUseServiceLocationFor<CodeDb>();
}, eventsSubscribedTo:
[
    SofaEvents.GitHubEventReceived,
    SofaEvents.ProjectBound,
    SofaEvents.RunEnded,
]);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<CodeDb>((services, options) =>
    CodeDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddOptions<GitHubApiOptions>().BindConfiguration(GitHubApiOptions.Section);
builder.Services.AddHttpClient<FlowBindings>(flow => flow.BaseAddress = FlowBindings.Address);
builder.Services.AddHttpClient<ConnectionsTokens>(connections => connections.BaseAddress = ConnectionsTokens.Address);
builder.Services.AddHttpClient<GitHubRepositories>((services, gitHub) =>
    GitHubRepositories.Configure(gitHub, services.GetRequiredService<IOptions<GitHubApiOptions>>().Value));
builder.Services.AddSingleton<SofasGitHubApp>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<CodeDb>().Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.MapRepositoryTokens();
app.MapPullRequests();

app.Run();
