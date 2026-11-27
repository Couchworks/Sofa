namespace Sofa.Execution.Workers;

public sealed class WorkerOptions
{
    public const string Section = "Workers";

    public string Image { get; set; } = "sofa-worker";

    public string? AgentGatewayAddress { get; set; }

    public TimeSpan LongestRun { get; set; } = TimeSpan.FromHours(2);

    public TimeSpan GraceAfterTheRunEnds { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan QueueLooksAgainEvery { get; set; } = TimeSpan.FromSeconds(5);
}
