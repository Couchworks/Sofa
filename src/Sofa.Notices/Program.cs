using JasperFx.CodeGeneration.Model;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts;
using Sofa.Contracts.Notices;
using Sofa.Notices.Data;
using Sofa.Notices.Inbox;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Notices, options =>
{
    options.PublishEvent<InboxChanged>(SofaEvents.InboxChanged);
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.CodeGeneration.AlwaysUseServiceLocationFor<NoticesDb>();
}, eventsSubscribedTo:
[
    SofaEvents.RequestAwaitingDelegation,
    SofaEvents.SubscriptionSignedOut,
    SofaEvents.SubscriptionSignInSeen,
    SofaEvents.WorkItemAwaitsUser,
    SofaEvents.WorkItemNoLongerAwaitsUser,
]);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<NoticesDb>((services, options) =>
    NoticesDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<NoticesDb>().Database.MigrateAsync();
}

app.MapDefaultEndpoints();
app.MapInbox();

app.Run();
