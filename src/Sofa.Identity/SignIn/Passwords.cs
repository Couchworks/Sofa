using Microsoft.AspNetCore.Identity;
using Sofa.Identity.Users;

namespace Sofa.Identity.SignIn;

public sealed class Passwords(IPasswordHasher<User> hasher)
{
    private static readonly User SomeoneWhoDoesNotExist = new() { Name = "" };
    private readonly Lazy<string> _hashToCompareAgainstWhenNoUserMatches =
        new(() => hasher.HashPassword(SomeoneWhoDoesNotExist, Guid.NewGuid().ToString()));

    public bool Verify(User? user, string password)
    {
        if (user is not { HasPassword: true })
        {
            hasher.VerifyHashedPassword(SomeoneWhoDoesNotExist, _hashToCompareAgainstWhenNoUserMatches.Value, password);
            return false;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
        }

        return result != PasswordVerificationResult.Failed;
    }
}
