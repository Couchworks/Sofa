using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using Sofa.Identity.Data;

namespace Sofa.Identity.Tokens;

public sealed class TokenKeys
{
    private const int RsaKeySize = 2048;

    private RsaSecurityKey? _signing;
    private RsaSecurityKey? _encryption;

    public RsaSecurityKey Signing => _signing ?? throw NotLoadedYet();

    public RsaSecurityKey Encryption => _encryption ?? throw NotLoadedYet();

    public async Task LoadOrCreateAsync(IdentityDb db, TimeProvider time, CancellationToken cancellation)
    {
        _signing = await LoadOrCreateAsync(db, TokenKeyUse.Signing, time, cancellation);
        _encryption = await LoadOrCreateAsync(db, TokenKeyUse.Encryption, time, cancellation);
    }

    private static async Task<RsaSecurityKey> LoadOrCreateAsync(
        IdentityDb db, TokenKeyUse use, TimeProvider time, CancellationToken cancellation)
    {
        var stored = await db.TokenKeys
            .Where(k => k.Use == use)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync(cancellation);

        if (stored is null)
        {
            using var rsa = RSA.Create(RsaKeySize);
            stored = new TokenKey { Use = use, RsaPrivateKeyPem = rsa.ExportRSAPrivateKeyPem(), CreatedAt = time.GetUtcNow() };
            db.TokenKeys.Add(stored);
            await db.SaveChangesAsync(cancellation);
        }

        var key = RSA.Create();
        key.ImportFromPem(stored.RsaPrivateKeyPem);
        return new RsaSecurityKey(key) { KeyId = stored.Id.ToString("N") };
    }

    private static InvalidOperationException NotLoadedYet() =>
        new("Identity's token keys are loaded before it serves requests; see IdentityStartup.");
}

public sealed class TokenKeysForOpenIddict(TokenKeys keys) : IConfigureOptions<OpenIddictServerOptions>
{
    public void Configure(OpenIddictServerOptions options)
    {
        options.SigningCredentials.Add(new SigningCredentials(keys.Signing, SecurityAlgorithms.RsaSha256));
        options.EncryptionCredentials.Add(new EncryptingCredentials(
            keys.Encryption, SecurityAlgorithms.RsaOAEP, SecurityAlgorithms.Aes256CbcHmacSha512));
    }
}
