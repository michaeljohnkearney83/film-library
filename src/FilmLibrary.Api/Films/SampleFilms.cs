namespace FilmLibrary.Api.Films;

/// <summary>Starter catalogue covering every rating. Seeded into an empty container when Cosmos:SeedSampleData is true.</summary>
public static class SampleFilms
{
    public static IReadOnlyList<Film> All { get; } =
    [
        new("toy-story", "Toy Story", 1995, "Animation", Rating.U, "John Lasseter",
            "A pull-string cowboy doll feels threatened when a flashy space-ranger action figure becomes his owner's new favourite toy."),
        new("paddington", "Paddington", 2014, "Family", Rating.PG, "Paul King",
            "A polite young bear from Peru arrives in London looking for a home and finds one with the Brown family."),
        new("jurassic-park", "Jurassic Park", 1993, "Adventure", Rating.PG, "Steven Spielberg",
            "Visitors to an island theme park of cloned dinosaurs fight to survive when the park's security systems fail."),
        new("the-dark-knight", "The Dark Knight", 2008, "Action", Rating.TwelveA, "Christopher Nolan",
            "Batman, Lieutenant Gordon and District Attorney Harvey Dent take on a criminal mastermind known as the Joker."),
        new("inception", "Inception", 2010, "Science Fiction", Rating.Twelve, "Christopher Nolan",
            "A thief who steals secrets from people's dreams is offered a chance at redemption by planting an idea instead."),
        new("the-matrix", "The Matrix", 1999, "Science Fiction", Rating.Fifteen, "The Wachowskis",
            "A hacker learns that the world he knows is a simulation and joins a rebellion against the machines that run it."),
        new("joker", "Joker", 2019, "Drama", Rating.Fifteen, "Todd Phillips",
            "A struggling party clown in Gotham City slides into madness and violence."),
        new("pulp-fiction", "Pulp Fiction", 1994, "Crime", Rating.Eighteen, "Quentin Tarantino",
            "The lives of two hitmen, a boxer and a gangster's wife intertwine across a series of Los Angeles crimes."),
        new("the-silence-of-the-lambs", "The Silence of the Lambs", 1991, "Thriller", Rating.Eighteen, "Jonathan Demme",
            "An FBI trainee seeks the help of an imprisoned cannibalistic psychiatrist to catch another serial killer."),
    ];
}
