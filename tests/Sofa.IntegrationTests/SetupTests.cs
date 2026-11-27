using System.Net;
using System.Net.Http.Json;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class SetupTests(SofaApp sofa)
{
    [Fact]
    public async Task A_fresh_installation_logs_a_setup_code()
    {
        var installation = await sofa.SetUp;

        Assert.Matches("^sofa_setup_[A-Za-z0-9_-]{30,}$", installation.SetupCode);
    }

    [Fact]
    public async Task Setup_refuses_a_code_that_is_not_the_logged_one()
    {
        var installation = await sofa.SetUp;

        Assert.Equal(HttpStatusCode.Forbidden, installation.WithAMadeUpCode);
    }

    [Fact]
    public async Task The_logged_setup_code_creates_the_first_User_and_works_once()
    {
        var installation = await sofa.SetUp;

        Assert.Equal(HttpStatusCode.Created, installation.WithTheLoggedCode);
        Assert.NotEqual(Guid.Empty, installation.FirstUser.Id);
        Assert.Equal(HttpStatusCode.Conflict, installation.WithTheLoggedCodeAgain);
    }

    [Fact]
    public async Task Setup_is_no_longer_needed_once_done()
    {
        await sofa.SetUp;
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);

        var setup = await gateway.GetFromJsonAsync<SetupNeeded>("/account/setup", TestContext.Current.CancellationToken);

        Assert.False(setup!.Needed);
    }

    private sealed record SetupNeeded(bool Needed);
}
