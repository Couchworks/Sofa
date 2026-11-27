using Microsoft.Extensions.Options;
using Sofa.Connections.Credentials;
using Sofa.Connections.GitHub;

namespace Sofa.Connections;

public static class ConnectionsSetup
{
    public static IServiceCollection AddCredentialVault(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KeyEncryptionKeyOptions>()
            .Bind(configuration.GetSection(KeyEncryptionKeyOptions.Section))
            .Validate(
                options => options.KeyEncryptionKey.Length >= KeyEncryptionKeyOptions.MinimumSecretLength,
                $"{KeyEncryptionKeyOptions.Section}:{nameof(KeyEncryptionKeyOptions.KeyEncryptionKey)} must be a secret " +
                $"of at least {KeyEncryptionKeyOptions.MinimumSecretLength} characters.")
            .ValidateOnStart();
        services.AddSingleton<IKeyEncryptionKeys, ConfiguredKeyEncryptionKeys>();
        services.AddSingleton<ICredentialVault, EnvelopeEncryption>();
        return services;
    }

    public static IServiceCollection AddGitHub(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GitHubOptions>().Bind(configuration.GetSection(GitHubOptions.Section));
        services.AddOptions<PublicAddressOptions>()
            .Bind(configuration.GetSection(PublicAddressOptions.Section))
            .Validate(options => options.PublicAddress is { IsAbsoluteUri: true },
                $"{PublicAddressOptions.Section}:{nameof(PublicAddressOptions.PublicAddress)} must be the absolute address " +
                "where GitHub and browsers reach Sofa's App Gateway.")
            .ValidateOnStart();
        services.AddSingleton(provider =>
            new PublicAddress(provider.GetRequiredService<IOptions<PublicAddressOptions>>().Value.PublicAddress!));

        services.AddHttpClient<GitHubApi>((provider, http) =>
            GitHubApi.Configure(http, provider.GetRequiredService<IOptions<GitHubOptions>>().Value));
        services.AddScoped<GitHubAppAccess>();
        services.AddSingleton<GitHubAppManifests>();
        services.AddHostedService<DeliverySweep>();
        return services;
    }
}
