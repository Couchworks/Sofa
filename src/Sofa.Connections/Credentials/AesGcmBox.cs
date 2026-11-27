using System.Security.Cryptography;

namespace Sofa.Connections.Credentials;

public static class AesGcmBox
{
    private static readonly int NonceBytes = AesGcm.NonceByteSizes.MaxSize;
    private static readonly int TagBytes = AesGcm.TagByteSizes.MaxSize;

    public static byte[] Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> boundTo)
    {
        var box = new byte[NonceBytes + plaintext.Length + TagBytes];
        var nonce = box.AsSpan(0, NonceBytes);
        var ciphertext = box.AsSpan(NonceBytes, plaintext.Length);
        var tag = box.AsSpan(NonceBytes + plaintext.Length, TagBytes);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, boundTo);
        return box;
    }

    public static byte[] Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> box, ReadOnlySpan<byte> boundTo)
    {
        if (box.Length < NonceBytes + TagBytes)
        {
            throw new CryptographicException("The sealed box is too short to hold a nonce and a tag.");
        }

        var ciphertextLength = box.Length - NonceBytes - TagBytes;
        var plaintext = new byte[ciphertextLength];
        using var aes = new AesGcm(key, TagBytes);
        aes.Decrypt(
            box[..NonceBytes],
            box.Slice(NonceBytes, ciphertextLength),
            box[(NonceBytes + ciphertextLength)..],
            plaintext,
            boundTo);
        return plaintext;
    }
}
