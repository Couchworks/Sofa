using Sofa.Contracts.Identity;

namespace Sofa.AgentGateway.Runs;

public static class RunTokenPolicy
{
    public const string Name = "run-token";

    public static IServiceCollection AddRunTokenPolicy(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .AddPolicy(Name, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(SofaClaims.Run)
                .RequireClaim(SofaClaims.Workspace))
            .Services;
}
