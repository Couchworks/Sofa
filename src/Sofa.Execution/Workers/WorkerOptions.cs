namespace Sofa.Execution.Workers;

public sealed class WorkerOptions
{
    public const string Section = "Workers";

    public string Image { get; set; } = "sofa-worker";

    public string? AgentGatewayAddress { get; set; }

    public string Network { get; set; } = "sofa-workers";

    public string RelayImage { get; set; } = "nginx:1.27-alpine";

    public TimeSpan LongestRun { get; set; } = TimeSpan.FromHours(2);

    public TimeSpan GraceAfterTheRunEnds { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan QueueLooksAgainEvery { get; set; } = TimeSpan.FromSeconds(5);
}
