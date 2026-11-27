using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Microsoft.AspNetCore.Http.Features;
using Sofa.Records.Contributions;
using Sofa.Records.Data;
using Sofa.Records.Files;
using Sofa.Records.Transcripts;
using JasperFx.CodeGeneration.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Records, options =>
{
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.AlwaysUseServiceLocationFor<RecordsDb>();
});

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<RecordsDb>((services, options) =>
    RecordsDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddOptions<FileStoreOptions>().BindConfiguration(FileStoreOptions.Section);
builder.Services.AddSingleton<IFileStore, LocalDiskFiles>();
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = ContributionRules.LargestUpload);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<RecordsDb>().Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapDefaultEndpoints();
app.MapTranscripts();
app.MapContributions();

app.Run();
