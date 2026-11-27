using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenIddict.Server;
using Sofa.Contracts;
using Sofa.Contracts.Identity;
using Sofa.Identity;
using Sofa.Identity.Data;
using Sofa.Identity.GitHub;
using Sofa.Identity.Setup;
using Sofa.Identity.SignIn;
using Sofa.Identity.Tokens;
using Sofa.Identity.Users;
using Sofa.Identity.Workspaces;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSofaMessaging(SofaServices.Identity, messaging =>
    messaging.CodeGeneration.AlwaysUseServiceLocationFor<IdentityDb>());

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<IdentityDb>((services, options) =>
    IdentityDb.Configure(options, services.GetRequiredService<NpgsqlDataSource>()));

builder.Services.AddDataProtection()
    .SetApplicationName(SofaServices.Identity)
    .PersistKeysToDbContext<IdentityDb>();

builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
{
    forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
});

builder.Services.AddOptions<ClientOptions>().BindConfiguration(ClientOptions.Section);
var issuer = builder.Configuration.GetSection(ClientOptions.Section).Get<ClientOptions>()?.Issuer
    ?? throw new InvalidOperationException(
        $"Identity needs {ClientOptions.Section}:Issuer, Sofa's public address, which the App Gateway serves.");

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<Passwords>();
builder.Services.AddScoped<FirstUserSetup>();
builder.Services.AddSingleton<TokenKeys>();
builder.Services.AddSingleton<IConfigureOptions<OpenIddictServerOptions>, TokenKeysForOpenIddict>();

builder.Services.AddSignInCookie();

builder.Services.AddHttpClient<ConnectionsGitHub>(connections => connections.BaseAddress = ConnectionsGitHub.Address);
builder.Services.AddScoped<GitHubAuthorizations>();
builder.Services.AddScoped<GitHubAccountLinks>();

builder.Services.AddOpenIddict()
    .AddCore(core => core.UseEntityFrameworkCore().UseDbContext<IdentityDb>().ReplaceDefaultEntities<Guid>())
    .AddServer(server =>
    {
        server.SetIssuer(issuer);
        server.SetAuthorizationEndpointUris(ConnectEndpoints.AuthorizationPath.TrimStart('/'))
            .SetTokenEndpointUris(ConnectEndpoints.TokenPath.TrimStart('/'))
            .SetEndSessionEndpointUris(ConnectEndpoints.EndSessionPath.TrimStart('/'))
            .SetRevocationEndpointUris(ConnectEndpoints.RevocationPath.TrimStart('/'));

        server.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow()
            .AllowClientCredentialsFlow();

        server.RegisterScopes([.. UserPrincipals.Scopes, RunTokens.Scope]);
        server.RegisterClaims(Claims.Name, Claims.Email, SofaClaims.Run, SofaClaims.Workspace);

        server.DisableAccessTokenEncryption();

        server.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .DisableTransportSecurityRequirement();
    });

var app = builder.Build();

await app.PrepareAsync();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseAuthentication();

app.MapDefaultEndpoints();
app.MapAccount();
app.MapConnect();
app.MapUsers();
app.MapGitHubSignIn();
app.MapTeams();

app.Run();
