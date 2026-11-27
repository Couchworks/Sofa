using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Sofa.Connections.Credentials;

public sealed class KeyEncryptionKeyOptions
{
    public const string Section = "Credentials";

    public const int MinimumSecretLength = 32;

    public string KeyEncryptionKey { get; set; } = "";

    public List<string> RetiredKeyEncryptionKeys { get; set; } = [];
}

public sealed class ConfiguredKeyEncryptionKeys : IKeyEncryptionKeys
{
    private static readonly byte[] DerivationPurpose = Encoding.UTF8.GetBytes("sofa connections key encryption key");
    private const int KeyBytes = 32;
    private const int KeyIdBytes = 8;

    private readonly DerivedKey _current;
    private readonly Dictionary<string, DerivedKey> _byId;

    public ConfiguredKeyEncryptionKeys(IOptions<KeyEncryptionKeyOptions> options)
    {
        var configured = options.Value;
        if (configured.KeyEncryptionKey.Length < KeyEncryptionKeyOptions.MinimumSecretLength)
        {
            throw new InvalidOperationException(
                $"{KeyEncryptionKeyOptions.Section}:{nameof(KeyEncryptionKeyOptions.KeyEncryptionKey)} must be set " +
                $"to a secret of at least {KeyEncryptionKeyOptions.MinimumSecretLength} characters.");
        }

        _current = Derive(configured.KeyEncryptionKey);
        _byId = configured.RetiredKeyEncryptionKeys
            .Select(Derive)
            .Append(_current)
            .DistinctBy(key => key.Id)
            .ToDictionary(key => key.Id);
    }

    public string CurrentKeyId => _current.Id;

    public ValueTask<WrappedDataKey> Wrap(ReadOnlyMemory<byte> dataKey, CancellationToken cancellation = default) =>
        ValueTask.FromResult(new WrappedDataKey(_current.Id, AesGcmBox.Seal(_current.Key, dataKey.Span, BoundTo(_current.Id))));

    public ValueTask<byte[]> Unwrap(WrappedDataKey wrapped, CancellationToken cancellation = default)
    {
        if (!_byId.TryGetValue(wrapped.KeyId, out var key))
        {
            throw new CryptographicException(
                $"The data key was wrapped with key {wrapped.KeyId}, which is neither the current nor a retired key encryption key.");
        }

        return ValueTask.FromResult(AesGcmBox.Open(key.Key, wrapped.Wrapped, BoundTo(key.Id)));
    }

    private static DerivedKey Derive(string secret)
    {
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret), KeyBytes, info: DerivationPurpose);
        var id = Convert.ToHexStringLower(SHA256.HashData(key).AsSpan(0, KeyIdBytes));
        return new DerivedKey(id, key);
    }

    private static byte[] BoundTo(string keyId) => Encoding.UTF8.GetBytes(keyId);

    private sealed record DerivedKey(string Id, byte[] Key);
}
