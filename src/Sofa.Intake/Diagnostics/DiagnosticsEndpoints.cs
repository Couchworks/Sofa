using Sofa.Contracts.Flow;
using Sofa.Contracts.Intake;
using Wolverine;

namespace Sofa.Intake.Diagnostics;

public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnostics(this IEndpointRouteBuilder endpoints)
    {
        var diagnostics = endpoints.MapGroup("/diagnostics");
        diagnostics.MapPost("/reports", (PostReport report, IMessageBus bus) => HandledAsync(report, bus));
        diagnostics.MapPost("/questions", (AskOnRequest asked, IMessageBus bus) => HandledAsync(asked, bus));
        diagnostics.MapPost("/work-items/entered-phase", (WorkItemEnteredPhase entered, IMessageBus bus) => HandledAsync(entered, bus));
        diagnostics.MapPost("/work-items/reached-main-branch", (WorkItemReachedMainBranch reached, IMessageBus bus) => HandledAsync(reached, bus));
        return endpoints;
    }

    private static async Task<IResult> HandledAsync(object message, IMessageBus bus)
    {
        await bus.InvokeAsync(message);
        return Results.NoContent();
    }
}
