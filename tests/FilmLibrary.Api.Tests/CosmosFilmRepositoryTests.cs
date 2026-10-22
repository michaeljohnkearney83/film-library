using FilmLibrary.Api.Data;
using FilmLibrary.Api.Films;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FilmLibrary.Api.Tests;

/// <summary>Runs only when COSMOS_EMULATOR_ENDPOINT is set, e.g. http://localhost:8081/ with `docker compose up -d`.</summary>
public sealed class CosmosEmulatorFactAttribute : FactAttribute
{
    public CosmosEmulatorFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COSMOS_EMULATOR_ENDPOINT")))
            Skip = "Set COSMOS_EMULATOR_ENDPOINT to run against the Cosmos DB emulator.";
    }
}

/// <summary>The real repository and seeder against the emulator, each test in its own throwaway database.</summary>
public sealed class CosmosFilmRepositoryTests : IAsyncLifetime
{
    private const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly ServiceProvider _services;
    private readonly CosmosOptions _options;

    public CosmosFilmRepositoryTests()
    {
        _options = new CosmosOptions
        {
            Endpoint = Environment.GetEnvironmentVariable("COSMOS_EMULATOR_ENDPOINT") ?? "http://localhost:8081/",
            Key = EmulatorKey,
            Database = $"test-{Guid.NewGuid():N}",
            SeedSampleData = true,
        };

        var services = new ServiceCollection().AddLogging();
        services.AddCosmosFilms(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddSingleton<IOptions<CosmosOptions>>(Options.Create(_options));
        _services = services.BuildServiceProvider();
    }

    private IFilmRepository Repository => _services.GetRequiredService<IFilmRepository>();

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COSMOS_EMULATOR_ENDPOINT")))
            return;

        var seeder = new SampleDataSeeder(_services, Options.Create(_options), NullLogger<SampleDataSeeder>.Instance);
        await seeder.StartAsync(CancellationToken.None);
        await seeder.ExecuteTask!;
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COSMOS_EMULATOR_ENDPOINT")))
        {
            try { await _services.GetRequiredService<CosmosClient>().GetDatabase(_options.Database).DeleteAsync(); }
            catch (CosmosException) { }
        }
        await _services.DisposeAsync();
    }

    [CosmosEmulatorFact]
    public async Task Seeder_creates_the_container_and_adds_the_sample_films_in_title_order()
    {
        var films = await Repository.GetAllAsync();

        Assert.Equal(SampleFilms.All.Select(f => f.Title).Order(StringComparer.Ordinal), films.Select(f => f.Title));
    }

    [CosmosEmulatorFact]
    public async Task Find_round_trips_every_field_including_the_rating()
    {
        var film = await Repository.FindAsync("the-dark-knight");

        Assert.Equal(SampleFilms.All.Single(f => f.Id == "the-dark-knight"), film);
    }

    [CosmosEmulatorFact]
    public async Task Find_returns_null_for_an_unknown_id() =>
        Assert.Null(await Repository.FindAsync("no-such-film"));

    [CosmosEmulatorFact]
    public async Task Seeder_leaves_an_existing_catalogue_alone()
    {
        await Repository.UpsertAsync(SampleFilms.All[0] with { Title = "Edited" });

        var seeder = new SampleDataSeeder(_services, Options.Create(_options), NullLogger<SampleDataSeeder>.Instance);
        await seeder.StartAsync(CancellationToken.None);
        await seeder.ExecuteTask!;

        Assert.Equal("Edited", (await Repository.FindAsync(SampleFilms.All[0].Id))?.Title);
    }
}
