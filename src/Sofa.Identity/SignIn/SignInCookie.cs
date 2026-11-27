using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Sofa.Identity.Users;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Sofa.Identity.SignIn;

public static class SignInCookie
{
    public const string Scheme = "sofa-sign-in";

    private const string Name = "sofa_sign_in";
    private const string OnlyTheAuthorizationEndpointsReadIt = "/connect";
    private static readonly TimeSpan StaysSignedInWhileUsedWithin = TimeSpan.FromDays(14);

    public static IServiceCollection AddSignInCookie(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddCookie(Scheme, cookie =>
            {
                cookie.Cookie.Name = Name;
                cookie.Cookie.Path = OnlyTheAuthorizationEndpointsReadIt;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                cookie.ExpireTimeSpan = StaysSignedInWhileUsedWithin;
                cookie.SlidingExpiration = true;
            });
        return services;
    }

    public static ClaimsPrincipal For(User user) =>
        new(new ClaimsIdentity([new Claim(Claims.Subject, user.Id.ToString())], Scheme));

    public static Guid? UserIdIn(ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirst(Claims.Subject)?.Value, out var id) ? id : null;
}
