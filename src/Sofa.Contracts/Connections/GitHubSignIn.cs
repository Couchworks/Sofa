namespace Sofa.Contracts.Connections;

public sealed record GitHubSignInApp(string ClientId, string AuthorizeAddress, string CallbackAddress);

public sealed record GitHubUserCode(string Code);

public sealed record GitHubUserAccount(long Id, string Login, string? Name, string? VerifiedPrimaryEmail);
