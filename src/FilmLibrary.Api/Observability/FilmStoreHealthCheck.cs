using FilmLibrary.Api.Films;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FilmLibrary.Api.Observability;

/// <summary>Ready means the API can read from Cosmos DB: network, managed identity and data-plane role all work.</summary>
public sealed class FilmStoreHealthCheck(IFilmRepository films) : IHealthCheck
{
    // A point read of an id that never exists: one cheap request, and "not found" counts as success.
    private const string ProbeId = "healthz-probe";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await films.FindAsync(ProbeId, cancellationToken);
            return HealthCheckResult.Healthy("Cosmos DB is reachable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Couldn't read from Cosmos DB.", ex);
        }
    }
}
