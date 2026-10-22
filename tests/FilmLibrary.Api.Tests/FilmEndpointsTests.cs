using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FilmLibrary.Api.Films;
using Microsoft.AspNetCore.Mvc;

namespace FilmLibrary.Api.Tests;

public class FilmEndpointsTests(FilmsApiFactory factory) : IClassFixture<FilmsApiFactory>
{
    private const string TwelveYearOld = "2014-03-15";
    private const string SixteenYearOld = "2010-06-01";
    private const string Adult = "1990-01-20";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData("/api/films")]
    [InlineData("/api/films/toy-story")]
    public async Task Anonymous_requests_are_rejected(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_without_the_Films_Read_scope_is_forbidden()
    {
        var response = await factory.CreateClientFor(Adult, scopes: "openid profile").GetAsync("/api/films");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Health_check_needs_no_token()
    {
        var response = await factory.CreateClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_includes_every_film_and_flags_the_ones_the_viewer_is_too_young_for()
    {
        var films = await GetFilmsAsync(factory.CreateClientFor(TwelveYearOld));

        Assert.Equal(SampleFilms.All.Count, films.Count);
        Assert.All(films, f => Assert.Equal(f.Rating is not (Rating.Fifteen or Rating.Eighteen), f.CanView));
    }

    [Fact]
    public async Task List_serializes_ratings_as_certificate_text()
    {
        var json = await factory.CreateClientFor(Adult).GetStringAsync("/api/films");

        Assert.Contains("\"rating\":\"12A\"", json);
        Assert.Contains("\"rating\":\"18\"", json);
    }

    [Theory]
    [InlineData(TwelveYearOld, "inception", HttpStatusCode.OK)]          // 12
    [InlineData(TwelveYearOld, "the-dark-knight", HttpStatusCode.OK)]    // 12A
    [InlineData(TwelveYearOld, "the-matrix", HttpStatusCode.Forbidden)]  // 15
    [InlineData(TwelveYearOld, "pulp-fiction", HttpStatusCode.Forbidden)] // 18
    [InlineData(SixteenYearOld, "the-matrix", HttpStatusCode.OK)]
    [InlineData(SixteenYearOld, "pulp-fiction", HttpStatusCode.Forbidden)]
    [InlineData(Adult, "pulp-fiction", HttpStatusCode.OK)]
    [InlineData(null, "paddington", HttpStatusCode.OK)]                  // PG, no birthdate
    [InlineData(null, "the-dark-knight", HttpStatusCode.Forbidden)]
    public async Task Details_are_only_returned_to_viewers_old_enough(string? birthdate, string id, HttpStatusCode expected)
    {
        var response = await factory.CreateClientFor(birthdate).GetAsync($"/api/films/{id}");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Details_include_the_full_content()
    {
        var details = await factory.CreateClientFor(Adult).GetFromJsonAsync<FilmDetails>("/api/films/toy-story", Json);

        Assert.Equal(("Toy Story", "John Lasseter", Rating.U), (details?.Title, details?.Director, details?.Rating));
        Assert.False(string.IsNullOrWhiteSpace(details?.Synopsis));
    }

    [Fact]
    public async Task Restricted_details_explain_the_age_limit_as_problem_details()
    {
        var response = await factory.CreateClientFor(TwelveYearOld).GetAsync("/api/films/pulp-fiction");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("You must be 18 or over to view this film.", problem?.Detail);
    }

    [Fact]
    public async Task Unknown_film_is_not_found()
    {
        var response = await factory.CreateClientFor(Adult).GetAsync("/api/films/no-such-film");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<List<FilmSummary>> GetFilmsAsync(HttpClient client) =>
        await client.GetFromJsonAsync<List<FilmSummary>>("/api/films", Json) ?? [];
}
