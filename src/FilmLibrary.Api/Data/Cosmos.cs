using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using FilmLibrary.Api.Films;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace FilmLibrary.Api.Data;

public sealed class CosmosOptions
{
    public const string Section = "Cosmos";

    public string Endpoint { get; set; } = string.Empty;

    public string Database { get; set; } = "filmlibrary";

    public string FilmsContainer { get; set; } = "films";

    /// <summary>Account key. Local emulator only; in Azure leave empty and use managed identity.</summary>
    public string? Key { get; set; }

    /// <summary>Client id of the user-assigned managed identity. Empty means DefaultAzureCredential (e.g. az login).</summary>
    public string? ManagedIdentityClientId { get; set; }

    /// <summary>Create the database/container (emulator only) and add sample films when the container is empty.</summary>
    public bool SeedSampleData { get; set; }
}

public static class CosmosSetup
{
    public static IServiceCollection AddCosmosFilms(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CosmosOptions>(configuration.GetSection(CosmosOptions.Section));

        services.AddSingleton(sp =>
        {
            var cosmos = sp.GetRequiredService<IOptions<CosmosOptions>>().Value;
            var clientOptions = new CosmosClientOptions
            {
                ApplicationName = "FilmLibrary.Api",
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() },
                },
            };

            if (!string.IsNullOrEmpty(cosmos.Key))
            {
                // Local emulator: gateway mode over the single published endpoint.
                clientOptions.ConnectionMode = ConnectionMode.Gateway;
                clientOptions.LimitToEndpoint = true;
                return new CosmosClient(cosmos.Endpoint, cosmos.Key, clientOptions);
            }

            TokenCredential credential = string.IsNullOrEmpty(cosmos.ManagedIdentityClientId)
                ? new DefaultAzureCredential()
                : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(cosmos.ManagedIdentityClientId));
            return new CosmosClient(cosmos.Endpoint, credential, clientOptions);
        });

        services.AddSingleton<IFilmRepository, CosmosFilmRepository>();
        services.AddHostedService<SampleDataSeeder>();
        return services;
    }
}

/// <summary>Seeds sample films in the background so a missing database never blocks startup.</summary>
public sealed class SampleDataSeeder(IServiceProvider services, IOptions<CosmosOptions> options, ILogger<SampleDataSeeder> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cosmos = options.Value;
        if (!cosmos.SeedSampleData)
            return;

        try
        {
            if (!string.IsNullOrEmpty(cosmos.Key))
            {
                // Key auth (emulator) may create resources; in Azure, Bicep creates them and the identity can't.
                var client = services.GetRequiredService<CosmosClient>();
                var database = await client.CreateDatabaseIfNotExistsAsync(cosmos.Database, cancellationToken: stoppingToken);
                await database.Database.CreateContainerIfNotExistsAsync(cosmos.FilmsContainer, "/id", cancellationToken: stoppingToken);
            }

            var repository = services.GetRequiredService<IFilmRepository>();
            if ((await repository.GetAllAsync(stoppingToken)).Count > 0)
                return;

            foreach (var film in SampleFilms.All)
                await repository.UpsertAsync(film, stoppingToken);
            logger.LogInformation("Seeded {Count} sample films", SampleFilms.All.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Couldn't seed sample films into Cosmos DB at {Endpoint}", cosmos.Endpoint);
        }
    }
}
