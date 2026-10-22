# FilmLibrary.Api.Tests

xUnit tests for the [API](../../src/FilmLibrary.Api/README.md).

```powershell
dotnet test FilmLibrary.slnx
```

## What's covered

| File | Covers |
|---|---|
| `AgeRulesTests` | Minimum age per rating; age from the birthdate claim, including birthdays falling today or tomorrow and midnight UK time; the configurable claim name; missing or invalid dates; scope matching |
| `FilmEndpointsTests` | The API run in memory: 401 without a token, 403 without `Films.Read`, the health check, lock flags, 200/403 for each age and rating, problem details, ratings serialized as text, 404 |
| `ContractTests` | The API's own OpenAPI against the TypeSpec output (`api-spec/generated/openapi.json`): operations, response codes, fields and types, required fields, `Rating` values |
| `CosmosFilmRepositoryTests` | The real repository and seeder against the **Cosmos DB emulator**: seeding, ordering, field round-trips, not-found, not re-seeding |
| `DeploymentDependencyTests` | The API's own dependency manifest, not the test project's, includes `Newtonsoft.Json`, which the Cosmos SDK loads at runtime |

## How it works

- **`FilmsApiFactory`** (in `TestHost.cs`) hosts the real API with three things swapped:
  - **Sign-in.** A test scheme replaces Entra; `CreateClientFor(birthdate, scopes)` builds a signed-in caller.
  - **Data.** An in-memory repository holding the sample films replaces Cosmos DB.
  - **The clock.** It's fixed at 22 Oct 2026.
- **Cosmos tests** are skipped unless `COSMOS_EMULATOR_ENDPOINT` is set. Each test uses its own temporary database, which is deleted afterwards.

```powershell
docker compose up -d
$env:COSMOS_EMULATOR_ENDPOINT = "http://localhost:8081/"
dotnet test FilmLibrary.slnx
```

The pipeline runs the emulator as a service container, so these tests run in CI too.
