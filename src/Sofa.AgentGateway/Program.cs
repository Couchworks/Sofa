using ModelContextProtocol.AspNetCore;
using Sofa.AgentGateway.Mcp;
using Sofa.AgentGateway.Runs;
using Sofa.AgentGateway.WorkItems;
using Sofa.Contracts;
using Sofa.Contracts.AgentGateway;
using Sofa.Contracts.Execution;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.AgentGateway, options =>
{
    options.PublishMessage<FinishRun>().ToRabbitQueue(SofaServices.Execution);
    options.PublishMessage<EndRunWithQuestion>().ToRabbitQueue(SofaServices.Execution);
});
builder.AcceptTokensFor(SofaServices.AgentGateway);
builder.Services.AddRunTokenPolicy();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ExecutionRuns>(execution => execution.BaseAddress = ExecutionRuns.Address);
builder.Services.AddHttpClient<FlowWorkItems>(flow => flow.BaseAddress = FlowWorkItems.Address);
builder.Services.AddHttpClient<IntakeRequests>(intake => intake.BaseAddress = IntakeRequests.Address);
builder.Services.AddHttpClient<RecordsContributions>(records => records.BaseAddress = RecordsContributions.Address);
builder.Services.AddMcpServer(server => server.ServerInfo = new() { Name = SofaTools.Server, Version = "1" })
    .WithHttpTransport(transport => transport.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<RunTools>()
    .OfferOnlyTheToolsOfEachRunsSandbox();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.ServeRunsOnlyWhileTheyRun();

app.MapDefaultEndpoints();
app.MapRunTokenCheck();
app.MapMcp(SofaTools.McpPath).RequireAuthorization(RunTokenPolicy.Name);

app.Run();
