using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Sofa.Connections.ApiKeys;
using Sofa.Connections.Credentials;
using Sofa.Contracts.Flow;

namespace Sofa.UnitTests.Connections;

public class ApiKeyRulesTests
{
    private const string AnApiKey = "sk-ant-api03-not-a-real-key";

    [Fact]
    public void An_Anthropic_API_key_is_taken()
    {
        Assert.Null(ApiKeyRules.ProblemWith(Vendor.Anthropic, $" {AnApiKey} "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sk-proj-openai")]
    [InlineData("sk-ant-with a space")]
    public void Anything_else_is_refused(string apiKey)
    {
        Assert.NotNull(ApiKeyRules.ProblemWith(Vendor.Anthropic, apiKey));
    }

    [Fact]
    public async Task A_sealed_API_key_opens_only_as_the_key_it_was_sealed_for()
    {
        var vault = new EnvelopeEncryption(new ConfiguredKeyEncryptionKeys(Options.Create(
            new KeyEncryptionKeyOptions { KeyEncryptionKey = new string('k', KeyEncryptionKeyOptions.MinimumSecretLength) })));
        var keyId = Guid.NewGuid();

        var sealedKey = await vault.Seal(AnApiKey, ApiKeyRules.PurposeOf(keyId), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(AnApiKey, sealedKey);
        Assert.Equal(AnApiKey, await vault.Open(sealedKey, ApiKeyRules.PurposeOf(keyId), TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await vault.Open(sealedKey, ApiKeyRules.PurposeOf(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }
}
