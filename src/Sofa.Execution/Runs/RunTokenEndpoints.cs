namespace Sofa.Execution.Runs;

public static class RunTokenEndpoints
{
    public static IEndpointRouteBuilder MapRunTokenDiagnostics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/diagnostics/run-tokens", async (
            RunTokenAsked asked, RunTokenClient runTokens, CancellationToken cancellation) =>
        {
            var runId = Guid.CreateVersion7();
            return await runTokens.RequestAsync(runId, asked.AgentId, cancellation) switch
            {
                RunTokenOutcome.Issued issued => Results.Ok(new RunTokenForMadeUpRun(runId, asked.AgentId, issued.Token, issued.ValidFor)),
                RunTokenOutcome.Refused refused => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: refused.Reason),
                _ => throw new InvalidOperationException("A Run token is either issued or refused."),
            };
        });
        return endpoints;
    }
}

public sealed record RunTokenAsked(Guid AgentId);

public sealed record RunTokenForMadeUpRun(Guid RunId, Guid AgentId, string Token, TimeSpan ValidFor);
