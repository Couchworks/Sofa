using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Sofa.Identity.Data;
using Sofa.Identity.Setup;
using Sofa.Identity.Users;

namespace Sofa.Identity.SignIn;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccount(this IEndpointRouteBuilder endpoints)
    {
        var account = endpoints.MapGroup("/account");

        account.MapGet("/setup", async (FirstUserSetup setup, CancellationToken cancellation) =>
            new SetupNeeded(await setup.IsNeededAsync(cancellation)));

        account.MapPost("/setup", async (
            SetupRequest request, FirstUserSetup setup, HttpContext http, CancellationToken cancellation) =>
        {
            var problems = AccountRules.ProblemsWithSetup(
                request.Name, request.Email, request.Password, request.WorkspaceName, request.TeamName);
            if (problems.Count > 0)
            {
                return Results.ValidationProblem(problems);
            }

            var (done, firstUser, refusal) = await setup.RunAsync(request, cancellation);
            switch (refusal)
            {
                case SetupRefusal.AlreadySetUp:
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Sofa is already set up.");
                case SetupRefusal.WrongSetupCode:
                    return Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "That setup code is not right. Use the latest one in Identity's log.");
            }

            await http.SignInAsync(SignInCookie.Scheme, SignInCookie.For(firstUser!));
            return Results.Created((string?)null, done);
        });

        account.MapPost("/sign-in", async (
            SignInRequest request, IdentityDb db, Passwords passwords, HttpContext http, CancellationToken cancellation) =>
        {
            var normalizedEmail = User.Normalize(request.Email ?? "");
            var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellation);
            if (!passwords.Verify(user, request.Password ?? ""))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "That email and password don't match a User.");
            }

            await db.SaveChangesAsync(cancellation);
            await http.SignInAsync(SignInCookie.Scheme, SignInCookie.For(user!));
            return Results.NoContent();
        });

        return endpoints;
    }
}

public sealed record SetupNeeded(bool Needed);

public sealed record SignInRequest(string? Email, string? Password);
