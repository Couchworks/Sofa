using System.Security.Cryptography;
using System.Text;

namespace Sofa.Connections.Credentials;

public sealed class EnvelopeEncryption(IKeyEncryptionKeys keyEncryptionKeys) : ICredentialVault
{
    private const string FormatVersion = "v1";
    private const char Separator = '.';
    private const int DataKeyBytes = 32;

    public async ValueTask<string> Seal(string credential, CredentialPurpose purpose, CancellationToken cancellation = default)
    {
        var dataKey = RandomNumberGenerator.GetBytes(DataKeyBytes);
        try
        {
            var sealedCredential = AesGcmBox.Seal(dataKey, Encoding.UTF8.GetBytes(credential), BoundTo(purpose));
            var wrapped = await keyEncryptionKeys.Wrap(dataKey, cancellation);
            return string.Join(Separator,
                FormatVersion,
                wrapped.KeyId,
                Convert.ToBase64String(wrapped.Wrapped),
                Convert.ToBase64String(sealedCredential));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public async ValueTask<string> Open(string sealedCredential, CredentialPurpose purpose, CancellationToken cancellation = default)
    {
        var parts = sealedCredential.Split(Separator);
        if (parts is not [FormatVersion, var keyId, var wrappedDataKey, var sealedBytes])
        {
            throw new CryptographicException("The sealed credential is not in a format this vault knows.");
        }

        var dataKey = await keyEncryptionKeys.Unwrap(
            new WrappedDataKey(keyId, Convert.FromBase64String(wrappedDataKey)), cancellation);
        try
        {
            return Encoding.UTF8.GetString(AesGcmBox.Open(dataKey, Convert.FromBase64String(sealedBytes), BoundTo(purpose)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private static byte[] BoundTo(CredentialPurpose purpose) => Encoding.UTF8.GetBytes(purpose.Name);
}
