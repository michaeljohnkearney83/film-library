using System.Security.Claims;
using FilmLibrary.Api.Auth;
using FilmLibrary.Api.Films;
using Microsoft.Extensions.Time.Testing;

namespace FilmLibrary.Api.Tests;

public class BbfcRatingTests
{
    [Theory]
    [InlineData(Rating.U, 0)]
    [InlineData(Rating.PG, 0)]
    [InlineData(Rating.TwelveA, 12)]
    [InlineData(Rating.Twelve, 12)]
    [InlineData(Rating.Fifteen, 15)]
    [InlineData(Rating.Eighteen, 18)]
    public void MinimumAge_matches_the_BBFC_certificate(Rating rating, int expected) =>
        Assert.Equal(expected, BbfcRating.MinimumAge(rating));
}

public class ViewerTests
{
    private static readonly FakeTimeProvider Clock = TestClock.Create();

    [Theory]
    [InlineData("2014-03-15", 12)]
    [InlineData("2010-06-01", 16)]
    [InlineData("1990-01-20", 36)]
    [InlineData("2008-10-22", 18)] // 18th birthday is today
    [InlineData("2008-10-23", 17)] // 18th birthday is tomorrow
    public void Age_is_whole_years_from_the_birthdate_claim(string birthdate, int expected) =>
        Assert.Equal(expected, Viewer.Age(UserWith(new Claim("birthdate", birthdate)), Clock, "birthdate"));

    [Fact]
    public void Birthday_starts_at_midnight_UK_time()
    {
        // 21 Oct 2026 23:30 UTC is already 22 Oct in the UK (BST, UTC+1).
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(18, Viewer.Age(UserWith(new Claim("birthdate", "2008-10-22")), clock, "birthdate"));
    }

    [Fact]
    public void Reads_the_configured_claim_name() =>
        Assert.Equal(12, Viewer.Age(UserWith(new Claim("extn.Birthdate", "2014-03-15")), Clock, "extn.Birthdate"));

    [Theory]
    [InlineData("15/03/2014")]
    [InlineData("not a date")]
    [InlineData("")]
    public void Age_is_null_for_a_missing_or_malformed_birthdate(string birthdate)
    {
        Assert.Null(Viewer.Age(UserWith(), Clock, "birthdate"));
        Assert.Null(Viewer.Age(UserWith(new Claim("birthdate", birthdate)), Clock, "birthdate"));
    }

    [Theory]
    [InlineData(12, Rating.TwelveA, true)]
    [InlineData(12, Rating.Fifteen, false)]
    [InlineData(15, Rating.Fifteen, true)]
    [InlineData(17, Rating.Eighteen, false)]
    [InlineData(18, Rating.Eighteen, true)]
    [InlineData(null, Rating.PG, true)]
    [InlineData(null, Rating.TwelveA, false)]
    public void CanView_compares_age_with_the_rating(int? age, Rating rating, bool expected) =>
        Assert.Equal(expected, Viewer.CanView(age, rating));

    private static ClaimsPrincipal UserWith(params Claim[] claims) => new(new ClaimsIdentity(claims, "Test"));
}

public class ScopeTests
{
    [Theory]
    [InlineData("Films.Read", true)]
    [InlineData("openid Films.Read profile", true)]
    [InlineData("films.read", false)]
    [InlineData("Films.ReadWrite", false)]
    [InlineData("", false)]
    public void HasScope_matches_one_space_separated_scope_exactly(string scp, bool expected) =>
        Assert.Equal(expected, AuthPolicies.HasScope(new ClaimsPrincipal(new ClaimsIdentity([new Claim("scp", scp)], "Test")), "Films.Read"));
}
