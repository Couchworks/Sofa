using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;
using Sofa.Contracts;

namespace Microsoft.Extensions.Hosting;

public static class TokenChecking
{
    public static readonly Uri IdentityDiscoveryDocument =
        new($"http://{SofaServices.Identity}/.well-known/openid-configuration");

    public const string IssuerSetting = "Identity:Issuer";

    public static TBuilder AcceptTokensFor<TBuilder>(this TBuilder builder, string audience)
        where TBuilder : IHostApplicationBuilder
    {
        var issuer = builder.Configuration[IssuerSetting];
        builder.Services.AddOpenIddict()
            .AddValidation(validation =>
            {
                if (issuer is not null)
                {
                    validation.SetIssuer(issuer);
                }

                validation.Configure(options => options.ConfigurationEndpoint = IdentityDiscoveryDocument);
                validation.AddAudiences(audience);
                validation.UseSystemNetHttp();
                validation.UseAspNetCore()
                    .DisableAccessTokenExtractionFromBodyForm()
                    .DisableAccessTokenExtractionFromQueryString();
            });

        builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        builder.Services.AddAuthorization();
        return builder;
    }
}
