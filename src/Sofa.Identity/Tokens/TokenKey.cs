namespace Sofa.Identity.Tokens;

public sealed class TokenKey
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required TokenKeyUse Use { get; init; }

    public required string RsaPrivateKeyPem { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public enum TokenKeyUse
{
    Signing,
    Encryption,
}
