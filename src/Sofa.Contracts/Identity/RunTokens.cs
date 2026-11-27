namespace Sofa.Contracts.Identity;

public static class RunTokens
{
    public const string ClientId = SofaServices.Execution;
    public const string Scope = "run";
    public const string RunIdParameter = "run_id";
    public const string AgentIdParameter = "agent_id";
    public const string Audience = SofaServices.AgentGateway;
}

public static class SofaClaims
{
    public const string Run = "sofa_run";
    public const string Workspace = "sofa_workspace";
}
