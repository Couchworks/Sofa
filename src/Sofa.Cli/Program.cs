using Sofa.Cli;

const string Usage = """
    Usage:
      sofa runner login <runner>    sign a subscription Runner in, in a Worker container on its own volume
      sofa runner status <runner>   check a subscription Runner's sign-in again and tell Sofa

    <runner> is the Runner's id, as the Runners page in Sofa shows it.
    SOFA_EXECUTION    Execution's address on this box (default http://localhost:5005)
    SOFA_WORKER_IMAGE the Worker image (default sofa-worker)
    """;

const string DefaultExecution = "http://localhost:5005";
const string DefaultWorkerImage = "sofa-worker";

if (args is not ["runner", var command and ("login" or "status"), var runner])
{
    Console.Error.WriteLine(Usage);
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, stop) =>
{
    stop.Cancel = true;
    cancellation.Cancel();
};

var executionAddress = Environment.GetEnvironmentVariable("SOFA_EXECUTION") is { Length: > 0 } configured ? configured : DefaultExecution;
var image = Environment.GetEnvironmentVariable("SOFA_WORKER_IMAGE") is { Length: > 0 } configuredImage ? configuredImage : DefaultWorkerImage;
using var execution = new HttpClient { BaseAddress = new Uri(executionAddress) };
var signIn = new RunnerSignIn(execution, new DockerOnThisBox(), Console.Out, image);

return command == "login"
    ? await signIn.LoginAsync(runner, inATerminal: !Console.IsInputRedirected && !Console.IsOutputRedirected, cancellation.Token)
    : await signIn.CheckAsync(runner, cancellation.Token);
