namespace FilmLibrary.Api.Films;

/// <summary>A film as stored in Cosmos DB. The id doubles as the partition key.</summary>
public sealed record Film(
    string Id,
    string Title,
    int Year,
    string Genre,
    Rating Rating,
    string Director,
    string Synopsis);

/// <summary>Response shapes. Names and fields must match the models in api-spec/main.tsp (checked by ContractTests).</summary>
public sealed record FilmSummary(string Id, string Title, int Year, string Genre, Rating Rating, bool CanView);

public sealed record FilmDetails(string Id, string Title, int Year, string Genre, Rating Rating, string Director, string Synopsis);
