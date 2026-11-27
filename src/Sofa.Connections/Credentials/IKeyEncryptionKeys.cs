namespace Sofa.Connections.Credentials;

public interface IKeyEncryptionKeys
{
    ValueTask<WrappedDataKey> Wrap(ReadOnlyMemory<byte> dataKey, CancellationToken cancellation = default);

    ValueTask<byte[]> Unwrap(WrappedDataKey wrapped, CancellationToken cancellation = default);
}

public sealed record WrappedDataKey(string KeyId, byte[] Wrapped);
