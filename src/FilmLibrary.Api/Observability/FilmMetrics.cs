using System.Diagnostics;
using System.Diagnostics.Metrics;
using FilmLibrary.Api.Films;

namespace FilmLibrary.Api.Observability;

public enum FilmViewOutcome
{
    Shown,
    AgeRestricted,
    NotFound,
}

/// <summary>Film Library's own metrics, on top of the HTTP, Cosmos DB and runtime telemetry that OpenTelemetry collects.</summary>
public sealed class FilmMetrics
{
    public const string MeterName = "FilmLibrary.Api";
    public const string FilmViews = "filmlibrary.film.views";

    private readonly Counter<long> _filmViews;

    public FilmMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _filmViews = meter.CreateCounter<long>(FilmViews, unit: "{request}", description: "Requests for a film's details, by outcome and rating.");
    }

    /// <summary>
    /// Counts a request for a film's details and tags the current request's trace with it.
    /// Never records the viewer's birthdate or age: only whether the token had a usable birthdate.
    /// </summary>
    public void FilmViewed(string filmId, FilmViewOutcome outcome, Rating? rating, bool birthdateKnown)
    {
        var outcomeText = outcome switch
        {
            FilmViewOutcome.Shown => "shown",
            FilmViewOutcome.AgeRestricted => "age_restricted",
            FilmViewOutcome.NotFound => "not_found",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
        var ratingText = rating is { } r ? BbfcRating.Certificate(r) : "none";

        // The film id goes on the trace only: as a metric dimension it would create one time series per film.
        _filmViews.Add(1, new("outcome", outcomeText), new("film.rating", ratingText), new("viewer.birthdate_known", birthdateKnown));
        Activity.Current?
            .SetTag("film.id", filmId)
            .SetTag("film.rating", ratingText)
            .SetTag("filmlibrary.outcome", outcomeText);
    }
}
