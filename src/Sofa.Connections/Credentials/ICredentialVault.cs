namespace Sofa.Connections.Credentials;

public interface ICredentialVault
{
    ValueTask<string> Seal(string credential, CredentialPurpose purpose, CancellationToken cancellation = default);

    ValueTask<string> Open(string sealedCredential, CredentialPurpose purpose, CancellationToken cancellation = default);
}

public sealed record CredentialPurpose(string Name);
