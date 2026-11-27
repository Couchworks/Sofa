namespace Sofa.Contracts;

/// <summary>
/// The names of the services of ADR 0012. Each name is the service's Aspire resource,
/// its Wolverine service name and its own queue on the broker (ADR 0023).
/// </summary>
public static class SofaServices
{
    public const string AppGateway = "app-gateway";
    public const string AgentGateway = "agent-gateway";
    public const string Identity = "identity";
    public const string Connections = "connections";
    public const string Intake = "intake";
    public const string Flow = "flow";
    public const string Execution = "execution";
    public const string Records = "records";
    public const string Code = "code";
    public const string Knowledge = "knowledge";
    public const string Notices = "notices";

    public static IReadOnlyList<string> All { get; } =
    [
        AppGateway, AgentGateway, Identity, Connections, Intake,
        Flow, Execution, Records, Code, Knowledge, Notices,
    ];

    /// <summary>The Postgres schema a service keeps its data and its outbox in (ADR 0014).</summary>
    public static string SchemaOf(string service) => service.Replace('-', '_');
}
