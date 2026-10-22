using Azure.Monitor.OpenTelemetry.AspNetCore;
using FilmLibrary.Api.Auth;
using FilmLibrary.Api.Data;
using FilmLibrary.Api.Films;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddEntraAuth(builder.Configuration);
builder.Services.AddCosmosFilms(builder.Configuration);

// The React app is served from a different origin (Static Web Apps, or Vite locally).
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .WithMethods("GET")));

// Application Insights, when App Service provides a connection string.
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    builder.Services.AddOpenTelemetry().UseAzureMonitor();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi(); // /openapi/v1.json, compared against api-spec by ContractTests

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok("Healthy")).ExcludeFromDescription(); // App Service health check
app.MapFilmEndpoints();

app.Run();

// Lets the integration tests host the API with WebApplicationFactory<Program>.
public partial class Program;
