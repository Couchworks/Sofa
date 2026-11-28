using Sofa.AppGateway.Live;

namespace Sofa.UnitTests.AppGateway;

public class LiveEndpointsTests
{
    [Theory]
    [InlineData("/api/workspaces/0195e7a0-0000-7000-8000-000000000002/live", true)]
    [InlineData("/api/workspaces/0195e7a0-0000-7000-8000-000000000002/live/negotiate", true)]
    [InlineData("/api/workspaces/0195e7a0-0000-7000-8000-000000000002/inbox", false)]
    [InlineData("/api/workspaces/not-a-workspace/live", false)]
    [InlineData("/api/workspaces/0195e7a0-0000-7000-8000-000000000002/live/negotiate/more", false)]
    [InlineData("/live", false)]
    public void Only_a_live_connection_may_bring_its_token_in_the_query(string path, bool isLive) =>
        Assert.Equal(isLive, LiveEndpoints.IsALiveConnection(path));
}
