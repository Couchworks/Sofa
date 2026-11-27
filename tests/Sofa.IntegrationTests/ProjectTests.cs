using System.Net;
using System.Net.Http.Json;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class ProjectTests(SofaApp sofa)
{
    private static readonly TimeSpan RegisteredWithIdentityWithin = TimeSpan.FromSeconds(30);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_Admin_creates_Project_DEMO_bound_to_a_Repository_and_it_shows_its_Flow_and_three_Agents()
    {
        var demo = await TheDemoProjectAsync();

        Assert.Equal("DEMO", demo.Key);
        Assert.Equal(new Binding(await ConnectionAsync(), "acme/demo", "sofa"), demo.Requests);
        Assert.Equal(["acme/demo"], demo.Code.Repositories);
        Assert.Equal(["Triage", "Spec", "Plan", "Implement"], demo.Phases.Select(p => p.Name));
        Assert.All(demo.Phases, phase => Assert.Equal("Solo", phase.Pattern));
        Assert.Equal(["Triager", "Lead", "Builder"], demo.Agents.Select(a => a.Role));
        Assert.Equal("Approve", demo.Phases.Single(p => p.Name == "Spec").Gate);
        Assert.Equal("OwnBranch", demo.Phases.Single(p => p.Name == "Implement").Sandbox);
        Assert.Equal(demo.Agents.Single(a => a.Role == "Lead").Id, demo.Phases.Single(p => p.Name == "Plan").CastAgentId);
    }

    [Fact]
    public async Task Every_User_of_the_Workspace_sees_its_Projects()
    {
        await TheDemoProjectAsync();
        using var bea = await sofa.BeaSignedInAsync(Cancellation);

        using var response = await bea.GetWithTokenAsync($"{await WorkspaceApiAsync()}/projects", Cancellation);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectSummary>>(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(projects!, project => project.Key == "DEMO" && project.RequestRepository == "acme/demo");
    }

    [Fact]
    public async Task A_key_already_taken_in_the_Workspace_is_refused()
    {
        await TheDemoProjectAsync();
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var response = await ada.PostAsJsonAsync($"{await WorkspaceApiAsync()}/projects",
            await NewProjectAsync(" demo ", "Another demo"), Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("already has a Project DEMO", await ProblemOf(response));
    }

    [Fact]
    public async Task A_User_with_Write_changes_the_Leads_model_and_it_sticks()
    {
        var project = await CreateAsync("MODEL");
        var lead = project.Agents.Single(a => a.Role == "Lead");
        using var bea = await sofa.BeaSignedInAsync(Cancellation);

        using var changed = await bea.SendAsync(HttpMethod.Put, $"{await WorkspaceApiAsync()}/projects/MODEL/agents/{lead.Id}",
            new { role = "Lead", instructions = lead.Instructions, vendor = "Anthropic", model = "sonnet" }, Cancellation);
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var readAgain = await ProjectAsync(ada, "MODEL");

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal("sonnet", readAgain.Agents.Single(a => a.Id == lead.Id).Model);
        Assert.Equal("opus", readAgain.Agents.Single(a => a.Role == "Builder").Model);
    }

    [Fact]
    public async Task A_User_with_Write_changes_a_Phases_cast_Gate_and_Sandbox()
    {
        var project = await CreateAsync("CAST");
        var builder = project.Agents.Single(a => a.Role == "Builder");
        using var bea = await sofa.BeaSignedInAsync(Cancellation);

        using var changed = await bea.SendAsync(HttpMethod.Put, $"{await WorkspaceApiAsync()}/projects/CAST/phases/Plan",
            new { castAgentId = builder.Id, gate = "ReviewAfterwards", sandbox = "OwnBranch" }, Cancellation);
        var plan = (await changed.Content.ReadFromJsonAsync<Project>(Cancellation))!.Phases.Single(p => p.Name == "Plan");

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal((builder.Id, "ReviewAfterwards", "OwnBranch"), (plan.CastAgentId, plan.Gate, plan.Sandbox));
    }

    [Fact]
    public async Task A_Phase_casts_only_its_own_Projects_Agents()
    {
        var demo = await TheDemoProjectAsync();
        await CreateAsync("OTHER");
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var response = await ada.SendAsync(HttpMethod.Put, $"{await WorkspaceApiAsync()}/projects/OTHER/phases/Spec",
            new { castAgentId = demo.Agents[0].Id, gate = "Approve", sandbox = "ReadOnly" }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_non_Admin_cannot_create_a_Project_or_change_a_binding()
    {
        await TheDemoProjectAsync();
        using var bea = await sofa.BeaSignedInAsync(Cancellation);
        var api = await WorkspaceApiAsync();

        using var creating = await bea.PostAsJsonAsync($"{api}/projects", await NewProjectAsync("BEA", "Bea's"), Cancellation);
        using var binding = await bea.SendAsync(HttpMethod.Put, $"{api}/projects/DEMO/binding",
            await BindingToAsync(["acme/demo", "acme/web"]), Cancellation);
        using var repositories = await bea.GetWithTokenAsync($"{api}/connections/{await ConnectionAsync()}/repositories", Cancellation);
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, creating.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, binding.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, repositories.StatusCode);
        Assert.Equal(["acme/demo"], (await ProjectAsync(ada, "DEMO")).Code.Repositories);
    }

    [Fact]
    public async Task An_Admin_binds_a_Project_to_more_Repositories_the_Connection_reaches()
    {
        await CreateAsync("BIND");
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var repositories = await ada.GetWithTokenAsync(
            $"{await WorkspaceApiAsync()}/connections/{await ConnectionAsync()}/repositories", Cancellation);
        using var bound = await ada.SendAsync(HttpMethod.Put, $"{await WorkspaceApiAsync()}/projects/BIND/binding",
            await BindingToAsync(["acme/demo", "ACME/WEB"]), Cancellation);

        Assert.Equal(FakeGitHub.Repositories.Select(r => r.FullName).Order(),
            (await repositories.Content.ReadFromJsonAsync<List<Repository>>(Cancellation))!.Select(r => r.FullName));
        Assert.Equal(HttpStatusCode.OK, bound.StatusCode);
        Assert.Equal(["acme/demo", "acme/web"], (await bound.Content.ReadFromJsonAsync<Project>(Cancellation))!.Code.Repositories);
    }

    [Fact]
    public async Task A_Repository_the_Connection_does_not_reach_is_refused()
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var project = await NewProjectAsync("SECRET", "Secret", requestRepository: "someone-else/secret");

        using var response = await ada.PostAsJsonAsync($"{await WorkspaceApiAsync()}/projects", project, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("doesn't reach someone-else/secret", await ProblemOf(response));
    }

    [Fact]
    public async Task A_Projects_default_Agents_are_actors_Identity_issues_Run_tokens_for()
    {
        var lead = (await TheDemoProjectAsync()).Agents.Single(a => a.Role == "Lead");
        using var execution = sofa.CreateHttpClient(SofaServices.Execution);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(RegisteredWithIdentityWithin);

        HttpStatusCode issued;
        do
        {
            using var response = await execution.PostAsJsonAsync("/diagnostics/run-tokens", new { agentId = lead.Id }, timeout.Token);
            issued = response.StatusCode;
            if (issued != HttpStatusCode.OK)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
            }
        }
        while (issued != HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.OK, issued);
    }

    [Fact]
    public async Task Nobody_outside_the_Workspace_reads_its_Projects()
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);

        using var elsewhere = await ada.GetWithTokenAsync($"/api/workspaces/{Guid.NewGuid()}/projects", Cancellation);
        using var anonymous = await gateway.GetAsync($"{await WorkspaceApiAsync()}/projects", Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, elsewhere.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private static Project? _demo;

    private async Task<Project> TheDemoProjectAsync()
    {
        await OneAtATime.WaitAsync(Cancellation);
        try
        {
            return _demo ??= await CreateAsync("DEMO");
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private async Task<Project> CreateAsync(string key)
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        using var created = await ada.PostAsJsonAsync($"{await WorkspaceApiAsync()}/projects",
            await NewProjectAsync(key, $"The {key} Project"), Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<Project>(Cancellation))!;
    }

    private async Task<Project> ProjectAsync(WebClientSignIn user, string key)
    {
        using var response = await user.GetWithTokenAsync($"{await WorkspaceApiAsync()}/projects/{key}", Cancellation);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Project>(Cancellation))!;
    }

    private async Task<object> NewProjectAsync(string key, string name, string requestRepository = "acme/demo") =>
        new
        {
            key,
            name,
            binding = new
            {
                connectionId = await ConnectionAsync(),
                requestRepository,
                label = "sofa",
                codeRepositories = new[] { "acme/demo" },
            },
        };

    private async Task<object> BindingToAsync(string[] codeRepositories) =>
        new { connectionId = await ConnectionAsync(), requestRepository = "acme/demo", label = "sofa", codeRepositories };

    private async Task<Guid> ConnectionAsync() => (await sofa.TheConnection).ConnectionId;

    private async Task<string> WorkspaceApiAsync() => $"/api/workspaces/{(await sofa.SetUp).FirstUser.WorkspaceId}";

    private static async Task<string> ProblemOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Problem>(Cancellation))?.Title ?? "";

    private sealed record Problem(string? Title);

    private sealed record ProjectSummary(Guid Id, string Key, string Name, string RequestRepository, string RequestLabel);

    private sealed record Project(
        Guid Id, string Key, string Name, Binding Requests, CodeBinding Code, List<Phase> Phases, List<AgentOfProject> Agents);

    private sealed record Binding(Guid ConnectionId, string Repository, string Label);

    private sealed record CodeBinding(Guid ConnectionId, List<string> Repositories);

    private sealed record Phase(string Name, string Pattern, Guid CastAgentId, string Gate, string Sandbox);

    private sealed record AgentOfProject(
        Guid Id, string Name, string Role, string Instructions, string Vendor, string Model);

    private sealed record Repository(string FullName, bool Private);
}
