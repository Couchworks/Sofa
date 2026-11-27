using System.Text.Json;
using Sofa.Contracts.Records;

namespace Sofa.UnitTests.Contracts;

public class SecretScrubbingTests
{
    public static TheoryData<string> KnownSecrets =>
    [
        "sofa_setup_" + new string('A', 32),
        "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJydW4tMTIzNCJ9.c2lnbmF0dXJlLW9mLWl0",
        "ghs_" + new string('a', 36),
        "ghp_" + new string('B', 40),
        "gho_" + new string('c', 36),
        "github_pat_" + new string('d', 30),
        "sk-ant-api03-" + new string('e', 40),
        "sk-ant-oat01-" + new string('f', 40),
        "sk-ant-ort01-" + new string('g', 40),
        "AKIAIOSFODNN7EXAMPLE",
        "xoxb-1234567890-abcdefghij",
    ];

    [Theory]
    [MemberData(nameof(KnownSecrets))]
    public void A_known_secret_becomes_scrubbed_wherever_it_sits_in_the_text(string secret)
    {
        Assert.Equal($"before {SecretScrubbing.Scrubbed} after", SecretScrubbing.ScrubText($"before {secret} after"));
        Assert.Equal($"x={SecretScrubbing.Scrubbed}", SecretScrubbing.ScrubText($"x={secret}"));
    }

    [Fact]
    public void A_private_key_block_is_scrubbed_whole_or_to_the_end_when_it_never_ends()
    {
        const string block = "-----BEGIN RSA PRIVATE KEY-----\nMIIEow\nIBAA\n-----END RSA PRIVATE KEY-----";

        Assert.Equal($"key: {SecretScrubbing.Scrubbed} done", SecretScrubbing.ScrubText($"key: {block} done"));
        Assert.Equal($"key: {SecretScrubbing.Scrubbed}", SecretScrubbing.ScrubText("key: -----BEGIN OPENSSH PRIVATE KEY-----\nb3Blbn"));
    }

    [Fact]
    public void A_bearer_value_is_scrubbed_but_the_word_Bearer_is_kept()
    {
        Assert.Equal($"Authorization: Bearer {SecretScrubbing.Scrubbed}", SecretScrubbing.ScrubText("Authorization: Bearer abc123def456"));
        Assert.Equal("Use a Bearer token here", SecretScrubbing.ScrubText("Use a Bearer token here"));
    }

    [Fact]
    public void An_AWS_secret_access_key_is_scrubbed_where_it_is_named()
    {
        Assert.Equal($"aws_secret_access_key = {SecretScrubbing.Scrubbed}",
            SecretScrubbing.ScrubText("aws_secret_access_key = wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"));
    }

    [Fact]
    public void Text_without_secrets_is_left_as_it_is()
    {
        const string text = "Ran git push to sofa/DEMO-1 and opened a pull request; the AKIA prefix alone is fine.";

        Assert.Equal(text, SecretScrubbing.ScrubText(text));
    }

    [Fact]
    public void A_JSON_line_is_scrubbed_in_its_strings_and_stays_JSON_even_when_a_key_block_never_ends()
    {
        var line = """{"type":"user","message":{"content":[{"type":"tool_result","content":"-----BEGIN PRIVATE KEY-----\nMIIE"}]},"n":3}""";

        var scrubbed = SecretScrubbing.ScrubLine(line);

        using var parsed = JsonDocument.Parse(scrubbed);
        Assert.Equal(SecretScrubbing.Scrubbed,
            parsed.RootElement.GetProperty("message").GetProperty("content")[0].GetProperty("content").GetString());
        Assert.Equal(3, parsed.RootElement.GetProperty("n").GetInt32());
    }

    [Fact]
    public void A_line_that_is_not_JSON_is_scrubbed_as_text()
    {
        Assert.Equal($"claude: token {SecretScrubbing.Scrubbed} refused", SecretScrubbing.ScrubLine("claude: token ghs_" + new string('z', 36) + " refused"));
    }
}
