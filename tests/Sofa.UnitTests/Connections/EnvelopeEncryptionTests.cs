using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Sofa.Connections.Credentials;

namespace Sofa.UnitTests.Connections;

public class EnvelopeEncryptionTests
{
    private const string Secret = "a-key-encryption-key-of-at-least-32-characters";
    private const string RotatedSecret = "the-next-key-encryption-key-also-32-characters-long";
    private static readonly CredentialPurpose PrivateKey = new("github-app:1:private-key");

    [Fact]
    public async Task Opens_what_it_sealed()
    {
        var vault = VaultWith(Secret);

        var sealedCredential = await vault.Seal("-----BEGIN RSA PRIVATE KEY-----", PrivateKey, Cancellation);

        Assert.Equal("-----BEGIN RSA PRIVATE KEY-----", await vault.Open(sealedCredential, PrivateKey, Cancellation));
    }

    [Fact]
    public async Task Never_keeps_the_credential_in_the_clear()
    {
        var sealedCredential = await VaultWith(Secret).Seal("hunter2-hunter2", PrivateKey, Cancellation);

        Assert.DoesNotContain("hunter2", sealedCredential);
    }

    [Fact]
    public async Task Seals_each_credential_with_a_data_key_of_its_own()
    {
        var vault = VaultWith(Secret);

        var first = await vault.Seal("same", PrivateKey, Cancellation);
        var second = await vault.Seal("same", PrivateKey, Cancellation);

        Assert.NotEqual(WrappedDataKeyOf(first), WrappedDataKeyOf(second));
    }

    [Fact]
    public async Task Refuses_to_open_a_credential_sealed_for_another_purpose()
    {
        var vault = VaultWith(Secret);
        var sealedCredential = await vault.Seal("secret", PrivateKey, Cancellation);

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await vault.Open(sealedCredential, new CredentialPurpose("github-app:1:webhook-secret"), Cancellation));
    }

    [Fact]
    public async Task Refuses_to_open_a_tampered_credential()
    {
        var vault = VaultWith(Secret);
        var sealedCredential = await vault.Seal("secret", PrivateKey, Cancellation);
        var parts = sealedCredential.Split('.');
        var box = Convert.FromBase64String(parts[3]);
        box[^1] ^= 0x01;
        parts[3] = Convert.ToBase64String(box);

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await vault.Open(string.Join('.', parts), PrivateKey, Cancellation));
    }

    [Fact]
    public async Task Opens_credentials_sealed_under_a_retired_key_after_rotation()
    {
        var sealedBeforeRotation = await VaultWith(Secret).Seal("secret", PrivateKey, Cancellation);

        var rotated = VaultWith(RotatedSecret, retired: Secret);

        Assert.Equal("secret", await rotated.Open(sealedBeforeRotation, PrivateKey, Cancellation));
    }

    [Fact]
    public async Task Refuses_credentials_sealed_under_a_key_it_no_longer_has()
    {
        var sealedCredential = await VaultWith(Secret).Seal("secret", PrivateKey, Cancellation);

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await VaultWith(RotatedSecret).Open(sealedCredential, PrivateKey, Cancellation));
    }

    [Fact]
    public void Refuses_a_key_encryption_key_too_short_to_be_a_secret() =>
        Assert.Throws<InvalidOperationException>(() => KeysWith("short"));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static EnvelopeEncryption VaultWith(string secret, params string[] retired) => new(KeysWith(secret, retired));

    private static ConfiguredKeyEncryptionKeys KeysWith(string secret, params string[] retired) =>
        new(Options.Create(new KeyEncryptionKeyOptions { KeyEncryptionKey = secret, RetiredKeyEncryptionKeys = [.. retired] }));

    private static string WrappedDataKeyOf(string sealedCredential) => sealedCredential.Split('.')[2];
}
