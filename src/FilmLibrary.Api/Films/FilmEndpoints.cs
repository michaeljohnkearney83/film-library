using System.Security.Claims;
using FilmLibrary.Api.Auth;
using Microsoft.Extensions.Options;

namespace FilmLibrary.Api.Films;

/// <summary>Implements the Films interface from api-spec/main.tsp.</summary>
public static class FilmEndpoints
{
    public static IEndpointRouteBuilder MapFilmEndpoints(this IEndpointRouteBuilder app)
    {
        var films = app.MapGroup("/api/films")
            .RequireAuthorization(AuthPolicies.FilmsRead)
            .WithTags("Films");

        films.MapGet("", async (ClaimsPrincipal user, IFilmRepository repository, TimeProvider clock, IOptions<EntraOptions> entra, CancellationToken cancellationToken) =>
            {
                var age = Viewer.Age(user, clock, entra.Value.BirthdateClaim);
                var all = await repository.GetAllAsync(cancellationToken);
                return all
                    .Select(f => new FilmSummary(f.Id, f.Title, f.Year, f.Genre, f.Rating, Viewer.CanView(age, f.Rating)))
                    .ToList();
            })
            .WithName("listFilms")
            .Produces<List<FilmSummary>>();

        films.MapGet("{id}", async (string id, ClaimsPrincipal user, IFilmRepository repository, TimeProvider clock, IOptions<EntraOptions> entra, CancellationToken cancellationToken) =>
            {
                if (await repository.FindAsync(id, cancellationToken) is not { } film)
                    return Results.NotFound();

                if (!Viewer.CanView(Viewer.Age(user, clock, entra.Value.BirthdateClaim), film.Rating))
                    return Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "Age restricted",
                        detail: $"You must be {BbfcRating.MinimumAge(film.Rating)} or over to view this film.");

                return Results.Ok(new FilmDetails(film.Id, film.Title, film.Year, film.Genre, film.Rating, film.Director, film.Synopsis));
            })
            .WithName("getFilm")
            .Produces<FilmDetails>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
