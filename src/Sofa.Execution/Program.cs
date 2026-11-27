using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Execution.Data;
using Sofa.Execution.Runners;
using Sofa.Execution.Runs;
using JasperFx.CodeGeneration.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Execution, options =>
{
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.AlwaysUseServiceLocationFor<ExecutionDb>();
});

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ExecutionDb>((services, options) =>
    ExecutionDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddOptions<RunTokenClientOptions>().BindConfiguration(RunTokenClientOptions.Section);
builder.Services.AddHttpClient<RunTokenClient>(identity => identity.BaseAddress = RunTokenClient.IdentityAddress);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ExecutionDb>().Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.MapRunTokenDiagnostics();
app.MapRunners();

app.Run();
