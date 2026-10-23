using FilmLibrary.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FilmLibrary.Api.Tests;

/// <summary>How the "Entra" settings (any OpenID Connect provider: Entra in Azure, or a local Keycloak) configure JWT validation.</summary>
public class AuthSetupTests
{
    private static JwtBearerOptions BearerOptions(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging().AddEntraAuth(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public void Validates_tokens_from_the_configured_authority_and_audience()
    {
        var options = BearerOptions(new()
        {
            ["Entra:Authority"] = "https://contoso.ciamlogin.com/tenant-id/v2.0",
            ["Entra:Audience"] = "api-client-id",
        });

        Assert.Equal("https://contoso.ciamlogin.com/tenant-id/v2.0", options.Authority);
        Assert.Equal("api-client-id", options.Audience);
        Assert.False(options.MapInboundClaims);
    }

    [Fact]
    public void Requires_https_metadata_unless_turned_off()
    {
        Assert.True(BearerOptions(new() { ["Entra:Authority"] = "https://contoso.ciamlogin.com/tenant-id/v2.0" }).RequireHttpsMetadata);
    }

    [Fact]
    public void Allows_http_metadata_for_a_local_provider_when_configured()
    {
        var options = BearerOptions(new()
        {
            ["Entra:Authority"] = "http://localhost:8180/realms/mediahub",
            ["Entra:RequireHttpsMetadata"] = "false",
        });

        Assert.False(options.RequireHttpsMetadata);
    }
}
