# FilmLibrary.Api

ASP.NET Core (.NET 10) minimal API that serves films from Cosmos DB and enforces age limits. It implements the contract in [api-spec/main.tsp](../../api-spec/main.tsp).

## Run

```powershell
docker compose up -d                          # from the repo root: Cosmos DB emulator
dotnet run --project src/FilmLibrary.Api      # http://localhost:5080
```

To accept tokens from the local Keycloak instead of Entra, use `dotnet run --project src/FilmLibrary.Api --launch-profile keycloak` (see [keycloak/README.md](../../keycloak/README.md)).

In Development, the API connects to the emulator, creates the `filmlibrary` database and `films` container, and seeds the sample films if the container is empty. It serves its OpenAPI document at `/openapi/v1.json`.

## Endpoints

| Route | Auth | Returns |
|---|---|---|
| `GET /api/films` | `Films.Read` | Every film, each with `canView` for the caller |
| `GET /api/films/{id}` | `Films.Read` | Film details; **403** problem details if the caller is too young; **404** if there's no such film |
| `GET /healthz` | none | `Healthy` if the process is up (App Service's health check) |
| `GET /healthz/ready` | none | `Healthy` if it can also read from Cosmos DB; **503** `Unhealthy` if not. The pipeline waits for it after deploying |

A missing token gets **401**; a token without the `Films.Read` scope gets **403**.

## Age rules

Age is worked out from the access token's birthdate claim (`yyyy-MM-dd`), in UK time:

- U and PG: anyone.
- 12A and 12: 12 or over. 12A counts as 12, because the app can't know whether a child is accompanied.
- 15: 15 or over.
- 18: 18 or over.

A missing or invalid birthdate means U and PG only. The logic is in `Films/Rating.cs` and `Films/Viewer.cs`.

## Configuration

| Key | Meaning |
|---|---|
| `Entra:Authority` | OpenID Connect authority whose tokens are accepted, e.g. `https://contoso.ciamlogin.com/<tenant-id>/v2.0` (or the local Keycloak realm) |
| `Entra:Audience` | Expected token audience: the API app registration client id (`film-library-api` for Keycloak) |
| `Entra:RequiredScope` | Default `Films.Read`, read from the `scp` claim |
| `Entra:BirthdateClaim` | Token claim with the date of birth; default `birthdate` |
| `Entra:RequireHttpsMetadata` | Default `true`. Only the local Keycloak profile turns it off (http) |
| `Cosmos:Endpoint` / `Database` / `FilmsContainer` | Cosmos DB location |
| `Cosmos:Key` | **Emulator only.** Leave empty in Azure |
| `Cosmos:ManagedIdentityClientId` | User-assigned identity used in Azure; if empty, `DefaultAzureCredential` is used (e.g. `az login`) |
| `Cosmos:SeedSampleData` | Seed sample films into an empty container |
| `Cors:AllowedOrigins` | The React app's origin(s) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Sends telemetry to Application Insights; set from Key Vault in Azure |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Without Application Insights, sends telemetry over OTLP instead. The launch profiles set `http://localhost:4317`, the Aspire dashboard from docker-compose |

In Azure, Bicep sets all of these as App Service settings ([infra/main.bicep](../../infra/main.bicep)).

## Telemetry

OpenTelemetry collects requests, logs, exceptions, outgoing HTTP, a span per Cosmos DB operation (with its RU charge), and the `filmlibrary.film.views` metric (by outcome and rating). It never records the birthdate or age. Locally, run `docker compose up -d dashboard` and open http://localhost:18888. See [docs/observability.md](../../docs/observability.md).

## Structure

```
Program.cs          Wiring: auth, Cosmos DB, CORS, OpenAPI, telemetry, endpoints
Auth/EntraAuth.cs   JWT bearer (any OpenID Connect provider; Entra in Azure) and the Films.Read scope policy
Data/Cosmos.cs      Cosmos client (key for the emulator, managed identity in Azure) and the sample-data seeder
Films/              Rating, age rules, the repository, endpoints and sample data
Observability/      OpenTelemetry setup, the film-views metric and the Cosmos DB readiness check
```
