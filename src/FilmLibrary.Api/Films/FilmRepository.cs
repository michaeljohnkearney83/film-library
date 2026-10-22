using System.Net;
using FilmLibrary.Api.Data;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace FilmLibrary.Api.Films;

public interface IFilmRepository
{
    Task<IReadOnlyList<Film>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Film?> FindAsync(string id, CancellationToken cancellationToken = default);

    Task UpsertAsync(Film film, CancellationToken cancellationToken = default);
}

/// <summary>Films in a Cosmos DB container partitioned on <c>/id</c>.</summary>
public sealed class CosmosFilmRepository(CosmosClient client, IOptions<CosmosOptions> options) : IFilmRepository
{
    private Container Container => client.GetContainer(options.Value.Database, options.Value.FilmsContainer);

    public async Task<IReadOnlyList<Film>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var films = new List<Film>();
        using var feed = Container.GetItemQueryIterator<Film>("SELECT * FROM c ORDER BY c.title");
        while (feed.HasMoreResults)
            films.AddRange(await feed.ReadNextAsync(cancellationToken));
        return films;
    }

    public async Task<Film?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            return (await Container.ReadItemAsync<Film>(id, new PartitionKey(id), cancellationToken: cancellationToken)).Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task UpsertAsync(Film film, CancellationToken cancellationToken = default) =>
        Container.UpsertItemAsync(film, new PartitionKey(film.Id), cancellationToken: cancellationToken);
}
