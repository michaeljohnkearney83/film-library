using FilmLibrary.Api.Auth;
using FilmLibrary.Api.Data;
using FilmLibrary.Api.Films;
using FilmLibrary.Api.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddEntraAuth(builder.Configuration);
builder.Services.AddCosmosFilms(builder.Configuration);
builder.Services.AddFilmLibraryTelemetry(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<FilmStoreHealthCheck>("cosmos", timeout: TimeSpan.FromSeconds(10), tags: ["ready"]);

// The React app is served from a different origin (Static Web Apps, or Vite locally).
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader() // includes traceparent, which joins the browser's telemetry to the API's
    .WithMethods("GET")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi(); // /openapi/v1.json, compared against api-spec by ContractTests
else
    app.UseExceptionHandler(); // logs the exception (so it reaches Application Insights) and returns problem details

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok("Healthy")).ExcludeFromDescription(); // alive: App Service health check
app.MapHealthChecks("/healthz/ready", new() { Predicate = check => check.Tags.Contains("ready") }); // ready: Cosmos DB answers
app.MapFilmEndpoints();

app.Run();

// Lets the integration tests host the API with WebApplicationFactory<Program>.
public partial class Program;
