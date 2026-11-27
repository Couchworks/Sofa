using Sofa.Contracts.Records;
using Sofa.Records.Transcripts;

namespace Sofa.UnitTests.Records;

public class TranscriptRulesTests
{
    [Fact]
    public void Lines_are_numbered_from_where_they_arrive_and_lines_stored_before_are_not_stored_again()
    {
        var arrived = new AppendTranscript(Guid.NewGuid(), Guid.NewGuid(), FirstLine: 4, ["four", "five", "six"]);

        var stored = TranscriptRules.LinesNotYetStored(arrived, new HashSet<int> { 5 }, DateTimeOffset.UnixEpoch).ToList();

        Assert.Equal([(4, "four"), (6, "six")], stored.Select(line => (line.Number, line.Text)));
        Assert.All(stored, line => Assert.Equal((arrived.RunId, arrived.WorkspaceId), (line.RunId, line.WorkspaceId)));
    }
}
