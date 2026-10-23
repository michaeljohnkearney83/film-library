using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using FilmLibrary.Api.Films;
using FilmLibrary.Api.Observability;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace FilmLibrary.Api.Tests;

public class ObservabilityTests(FilmsApiFactory factory) : IClassFixture<FilmsApiFactory>
{
    private const string TwelveYearOld = "2014-03-15";
    private const string Adult = "1990-01-20";

    [Theory]
    [InlineData(Adult, "pulp-fiction", "shown", "18", true)]
    [InlineData(TwelveYearOld, "pulp-fiction", "age_restricted", "18", true)]
    [InlineData(TwelveYearOld, "the-dark-knight", "shown", "12A", true)]
    [InlineData(null, "the-dark-knight", "age_restricted", "12A", false)]
    [InlineData(Adult, "no-such-film", "not_found", "none", true)]
    public async Task Each_film_request_is_counted_by_outcome_and_rating(string? birthdate, string id, string outcome, string rating, bool birthdateKnown)
    {
        using var views = FilmViews();

        await factory.CreateClientFor(birthdate).GetAsync($"/api/films/{id}");

        var view = Assert.Single(views.GetMeasurementSnapshot());
        Assert.Equal(1, view.Value);
        Assert.Equal(outcome, view.Tags["outcome"]);
        Assert.Equal(rating, view.Tags["film.rating"]);
        Assert.Equal(birthdateKnown, view.Tags["viewer.birthdate_known"]);
    }

    [Fact]
    public async Task Film_metrics_never_include_the_birthdate_or_the_film_id()
    {
        using var views = FilmViews();

        await factory.CreateClientFor(TwelveYearOld).GetAsync("/api/films/pulp-fiction");

        var tags = Assert.Single(views.GetMeasurementSnapshot()).Tags;
        Assert.DoesNotContain(tags.Values, value => value?.ToString() is TwelveYearOld or "pulp-fiction");
    }

    [Fact]
    public async Task Listing_films_is_not_counted_as_viewing_one()
    {
        using var views = FilmViews();

        await factory.CreateClientFor(Adult).GetAsync("/api/films");

        Assert.Empty(views.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task Ready_when_the_film_store_answers()
    {
        var response = await factory.CreateClient().GetAsync("/healthz/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Not_ready_when_the_film_store_fails()
    {
        using var broken = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFilmRepository>();
            services.AddSingleton<IFilmRepository>(new UnreachableFilmRepository());
        }));

        var response = await broken.CreateClient().GetAsync("/healthz/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Liveness_doesnt_depend_on_the_film_store()
    {
        using var broken = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFilmRepository>();
            services.AddSingleton<IFilmRepository>(new UnreachableFilmRepository());
        }));

        var response = await broken.CreateClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllRatings))]
    public void Certificate_text_matches_the_json_the_api_returns(Rating rating)
    {
        Assert.Equal($"\"{BbfcRating.Certificate(rating)}\"", JsonSerializer.Serialize(rating));
    }

    public static TheoryData<Rating> AllRatings() => [.. Enum.GetValues<Rating>()];

    private MetricCollector<long> FilmViews() =>
        new(factory.Services.GetRequiredService<IMeterFactory>(), FilmMetrics.MeterName, FilmMetrics.FilmViews);

    private sealed class UnreachableFilmRepository : IFilmRepository
    {
        public Task<IReadOnlyList<Film>> GetAllAsync(CancellationToken cancellationToken = default) => throw Unreachable();

        public Task<Film?> FindAsync(string id, CancellationToken cancellationToken = default) => throw Unreachable();

        public Task UpsertAsync(Film film, CancellationToken cancellationToken = default) => throw Unreachable();

        private static HttpRequestException Unreachable() => new("Cosmos DB is unreachable.");
    }
}
