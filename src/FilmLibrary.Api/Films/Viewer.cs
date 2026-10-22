using System.Globalization;
using System.Security.Claims;

namespace FilmLibrary.Api.Films;

/// <summary>Works out the caller's age from the birthdate claim in their Entra access token.</summary>
public static class Viewer
{
    // UK ratings, so a birthday starts at midnight UK time wherever the server runs.
    private static readonly TimeZoneInfo UkTime = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    /// <summary>The viewer's age in whole years, or null if the token carries no valid birthdate (yyyy-MM-dd).</summary>
    public static int? Age(ClaimsPrincipal user, TimeProvider clock, string birthdateClaim)
    {
        var claim = user.FindFirst(birthdateClaim)?.Value;
        if (!DateOnly.TryParseExact(claim, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthdate))
            return null;

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), UkTime).DateTime);
        var age = today.Year - birthdate.Year;
        if (birthdate > today.AddYears(-age))
            age--;
        return age;
    }

    /// <summary>Unknown age is treated as too young for anything above PG.</summary>
    public static bool CanView(int? age, Rating rating) => (age ?? 0) >= BbfcRating.MinimumAge(rating);
}
