namespace Sofa.Identity.Users;

public sealed class User
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public string PasswordHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; init; }

    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
