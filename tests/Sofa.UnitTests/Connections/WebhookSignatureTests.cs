using System.Text;
using Sofa.Connections.GitHub;

namespace Sofa.UnitTests.Connections;

public class WebhookSignatureTests
{
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"action":"labeled"}""");

    [Fact]
    public void Matches_GitHubs_documented_example()
    {
        var signature = WebhookSignature.Sign(Encoding.UTF8.GetBytes("Hello, World!"), "It's a Secret to Everybody");

        Assert.Equal("sha256=757107ea0eb2509fc211221cce984b8a37570b6d7586c22c46f4379c8b043e17", signature);
    }

    [Fact]
    public void Accepts_a_body_signed_with_the_secret() =>
        Assert.True(WebhookSignature.Matches(Body, WebhookSignature.Sign(Body, "secret"), "secret"));

    [Fact]
    public void Refuses_a_body_signed_with_another_secret() =>
        Assert.False(WebhookSignature.Matches(Body, WebhookSignature.Sign(Body, "other"), "secret"));

    [Fact]
    public void Refuses_a_body_changed_after_signing() =>
        Assert.False(WebhookSignature.Matches(
            Encoding.UTF8.GetBytes("""{"action":"unlabeled"}"""), WebhookSignature.Sign(Body, "secret"), "secret"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha1=0123")]
    [InlineData("sha256=not-hex")]
    public void Refuses_a_missing_or_malformed_signature(string? signature) =>
        Assert.False(WebhookSignature.Matches(Body, signature, "secret"));
}
