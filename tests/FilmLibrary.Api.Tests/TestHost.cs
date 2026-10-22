using System.Security.Claims;
using System.Text.Encodings.Web;
using FilmLibrary.Api.Films;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace FilmLibrary.Api.Tests;

/// <summary>A fixed "today" of 22 Oct 2026 (UK), so ages in tests don't drift.</summary>
internal static class TestClock
{
    public static FakeTimeProvider Create() => new(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));
}

/// <summary>Stands in for Cosmos DB.</summary>
internal sealed class InMemoryFilmRepository(IEnumerable<Film> films) : IFilmRepository
{
    private readonly Dictionary<string, Film> _films = films.ToDictionary(f => f.Id);

    public Task<IReadOnlyList<Film>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Film>>([.. _films.Values.OrderBy(f => f.Title)]);

    public Task<Film?> FindAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_films.GetValueOrDefault(id));

    public Task UpsertAsync(Film film, CancellationToken cancellationToken = default)
    {
        _films[film.Id] = film;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Hosts the real API in memory with Entra's JWT validation swapped for a test scheme,
/// Cosmos DB swapped for the sample films in memory, and the clock fixed.
/// </summary>
public sealed class FilmsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cosmos:SeedSampleData"] = "false",
        }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFilmRepository>();
            services.AddSingleton<IFilmRepository>(new InMemoryFilmRepository(SampleFilms.All));
            services.AddSingleton<TimeProvider>(TestClock.Create());
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        });
    }

    /// <summary>A client signed in with this birthdate claim (or none) and the given scopes.</summary>
    public HttpClient CreateClientFor(string? birthdate, string scopes = "Films.Read")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.BirthdateHeader, birthdate ?? TestAuthHandler.None);
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, scopes);
        return client;
    }
}

/// <summary>Signs a request in when the test headers are present, adding claims the way Entra would.</summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string BirthdateHeader = "X-Test-Birthdate";
    public const string ScopeHeader = "X-Test-Scope";
    public const string None = "none";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(BirthdateHeader, out var birthdate))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new("scp", Request.Headers[ScopeHeader].ToString()) };
        if (birthdate != None)
            claims.Add(new Claim("birthdate", birthdate.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
