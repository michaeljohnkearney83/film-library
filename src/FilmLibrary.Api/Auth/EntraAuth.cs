using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace FilmLibrary.Api.Auth;

public sealed class EntraOptions
{
    public const string Section = "Entra";

    /// <summary>External ID tenant authority, e.g. https://contoso.ciamlogin.com/{tenant-id}/v2.0</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>The API app registration's client id, which Entra puts in the token's <c>aud</c> claim.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Delegated scope the React app must request.</summary>
    public string RequiredScope { get; set; } = "Films.Read";

    /// <summary>Token claim holding the user's date of birth (yyyy-MM-dd). Mapped from a custom user attribute in Entra.</summary>
    public string BirthdateClaim { get; set; } = "birthdate";
}

public static class AuthPolicies
{
    public const string FilmsRead = "FilmsRead";

    public static IServiceCollection AddEntraAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var entra = configuration.GetSection(EntraOptions.Section).Get<EntraOptions>() ?? new EntraOptions();
        services.Configure<EntraOptions>(configuration.GetSection(EntraOptions.Section));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = entra.Authority;
                options.Audience = entra.Audience;
                options.MapInboundClaims = false; // keep Entra's claim names, e.g. "scp" and the birthdate claim
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(FilmsRead, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasScope(context.User, entra.RequiredScope)));

        return services;
    }

    /// <summary>Entra puts delegated scopes in one space-separated <c>scp</c> claim.</summary>
    public static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.FindAll("scp").Any(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(scope, StringComparer.Ordinal));
}
