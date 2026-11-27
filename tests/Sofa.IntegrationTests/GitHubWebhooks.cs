using System.Text;
using System.Text.Json;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public static class GitHubWebhooks
{
    public static Task<HttpResponseMessage> LabeledAsync(
        SofaApp sofa, string repository, long number, string title, string label, GitHubAccountOnFake by) =>
        DeliverAsync(sofa, "issues", new
        {
            action = "labeled",
            label = new { name = label },
            issue = new { number, title, html_url = $"https://github.com/{repository}/issues/{number}" },
            repository = new { full_name = repository },
            sender = new { id = by.Id, login = by.Login, type = "User" },
            installation = new { id = FakeGitHub.InstallationId },
        });

    public static Task<HttpResponseMessage> CommentedAsync(
        SofaApp sofa, string repository, long number, long commentId, string body, GitHubAccountOnFake by) =>
        DeliverAsync(sofa, "issue_comment", new
        {
            action = "created",
            issue = new { number },
            comment = new { id = commentId, body, user = new { id = by.Id, login = by.Login, type = "User" } },
            repository = new { full_name = repository },
            sender = new { id = by.Id, login = by.Login, type = "User" },
            installation = new { id = FakeGitHub.InstallationId },
        });

    private static async Task<HttpResponseMessage> DeliverAsync(SofaApp sofa, string gitHubEvent, object payload)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/github")
        {
            Content = new ByteArrayContent(body) { Headers = { { "Content-Type", "application/json" } } },
            Headers =
            {
                { "X-GitHub-Event", gitHubEvent },
                { "X-GitHub-Delivery", Guid.NewGuid().ToString() },
                { "X-Hub-Signature-256", sofa.GitHub.Sign(body) },
            },
        };
        var response = await gateway.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
