namespace Sofa.Contracts;

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

    public static string SchemaOf(string service) => service.Replace('-', '_');
}
