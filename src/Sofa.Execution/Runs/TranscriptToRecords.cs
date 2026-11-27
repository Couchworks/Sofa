using System.Threading.Channels;
using Sofa.Contracts.Records;
using Wolverine;

namespace Sofa.Execution.Runs;

public sealed class TranscriptToRecords : IAsyncDisposable
{
    private const int MostLinesAtOnce = 100;

    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Guid _runId;
    private readonly Guid _workspaceId;
    private readonly IServiceScopeFactory _scopes;
    private readonly Task _sent;

    public TranscriptToRecords(Guid runId, Guid workspaceId, IServiceScopeFactory scopes)
    {
        _runId = runId;
        _workspaceId = workspaceId;
        _scopes = scopes;
        _sent = SendAsTheyComeAsync();
    }

    public void Add(string line) => _lines.Writer.TryWrite(line);

    public async ValueTask DisposeAsync()
    {
        _lines.Writer.TryComplete();
        await _sent;
    }

    private async Task SendAsTheyComeAsync()
    {
        var nextLine = 1;
        while (await _lines.Reader.WaitToReadAsync())
        {
            var batch = new List<string>();
            while (batch.Count < MostLinesAtOnce && _lines.Reader.TryRead(out var line))
            {
                batch.Add(line);
            }

            await using var scope = _scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .SendAsync(new AppendTranscript(_runId, _workspaceId, nextLine, batch));
            nextLine += batch.Count;
        }
    }
}
