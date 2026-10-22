using System.Text.Json.Serialization;

namespace FilmLibrary.Api.Films;

/// <summary>UK (BBFC) certificate. Serialized as the certificate text, matching <c>Rating</c> in api-spec/main.tsp.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Rating>))]
public enum Rating
{
    U,
    PG,
    [JsonStringEnumMemberName("12A")] TwelveA,
    [JsonStringEnumMemberName("12")] Twelve,
    [JsonStringEnumMemberName("15")] Fifteen,
    [JsonStringEnumMemberName("18")] Eighteen,
}

public static class BbfcRating
{
    public static int MinimumAge(Rating rating) => rating switch
    {
        Rating.U or Rating.PG => 0,
        // 12A lets under-12s in with an adult; the app has no notion of "accompanied", so treat it as 12.
        Rating.TwelveA or Rating.Twelve => 12,
        Rating.Fifteen => 15,
        Rating.Eighteen => 18,
        _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "Unknown BBFC rating."),
    };
}
