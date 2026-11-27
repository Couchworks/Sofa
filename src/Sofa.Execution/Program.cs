using Sofa.Contracts;
using Sofa.Execution.Runs;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Execution);

builder.Services.AddProblemDetails();
builder.Services.AddOptions<RunTokenClientOptions>().BindConfiguration(RunTokenClientOptions.Section);
builder.Services.AddHttpClient<RunTokenClient>(identity => identity.BaseAddress = RunTokenClient.IdentityAddress);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapRunTokenDiagnostics();

app.Run();
