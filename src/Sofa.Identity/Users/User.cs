namespace Sofa.Identity.Users;

public sealed class User
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public string PasswordHash { get; set; } = "";

    public long? GitHubAccountId { get; set; }

    public string? GitHubLogin { get; set; }

    public string? NormalizedGitHubLogin { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public bool HasPassword => PasswordHash.Length > 0;

    public bool IsOnlyAddedByGitHubLogin => GitHubAccountId is null && Email is null && !HasPassword;

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();

    public static string NormalizeGitHubLogin(string login) => login.Trim().ToUpperInvariant();
}
