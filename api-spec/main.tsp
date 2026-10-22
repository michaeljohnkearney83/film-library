import "@typespec/http";
import "@typespec/openapi";

using Http;
using OpenAPI;

/**
 * Film Library API. The single source of truth for the contract between the .NET API and the React app.
 * Every route requires a Microsoft Entra External ID access token with the `Films.Read` scope.
 */
@service(#{ title: "Film Library API" })
@useAuth(BearerAuth)
namespace FilmLibrary;

/** UK (BBFC) certificate. 12A counts as 12, because the app has no notion of "accompanied by an adult". */
enum Rating {
  U: "U",
  PG: "PG",
  `12A`: "12A",
  `12`: "12",
  `15`: "15",
  `18`: "18",
}

/** A film as shown in the list. */
model FilmSummary {
  id: string;
  title: string;
  year: int32;
  genre: string;
  rating: Rating;

  /** Whether the signed-in viewer is old enough to open the film's details. Decided by the API. */
  canView: boolean;
}

/** Full film content. Only returned to viewers old enough for its rating. */
model FilmDetails {
  id: string;
  title: string;
  year: int32;
  genre: string;
  rating: Rating;
  director: string;
  synopsis: string;
}

/** RFC 9457 problem details, as returned by ASP.NET Core. */
model ProblemDetails {
  type?: string;
  title?: string;
  status?: int32;

  /** Human-readable reason, e.g. "You must be 18 or over to view this film." */
  detail?: string;
}

/** The viewer is too young for the film's rating. */
model AgeRestrictedResponse {
  @statusCode statusCode: 403;
  @header contentType: "application/problem+json";
  @body problem: ProblemDetails;
}

@route("/api/films")
@tag("Films")
interface Films {
  /** Every film, flagged with whether the caller may view it. */
  @get
  @operationId("listFilms")
  list(): FilmSummary[] | UnauthorizedResponse;

  /** One film's full content. */
  @get
  @route("{id}")
  @operationId("getFilm")
  read(@path id: string): FilmDetails | AgeRestrictedResponse | NotFoundResponse | UnauthorizedResponse;
}
