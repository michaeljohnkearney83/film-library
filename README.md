# Film Library

A web app for browsing films, with access limited by age and UK (BBFC) rating: a 12-year-old can open U, PG, 12A and 12 films, but not 15 or 18. People sign in with **Microsoft Entra External ID**. Films are stored in **Azure Cosmos DB**, and everything deploys to Azure through an **Azure DevOps** pipeline.

```
React (Static Web Apps) ──sign in──▶ Entra External ID
        │  access token (Films.Read, birthdate claim)
        ▼
.NET API (App Service) ──managed identity──▶ Cosmos DB · Key Vault · Storage
        │
        └──▶ Application Insights / Log Analytics
```

## Repository layout

| Folder | What it is |
|---|---|
| [api-spec/](api-spec/README.md) | **TypeSpec contract**: the single definition of the API, from which the OpenAPI and the React types are generated |
| [src/FilmLibrary.Api/](src/FilmLibrary.Api/README.md) | ASP.NET Core minimal API; enforces the age limits; stores data in Cosmos DB |
| [src/web/](src/web/README.md) | React + Vite + TypeScript front end, signing in with MSAL |
| [tests/FilmLibrary.Api.Tests/](tests/FilmLibrary.Api.Tests/README.md) | API tests, contract tests and Cosmos DB integration tests |
| [infra/](infra/README.md) | Bicep: App Service, Cosmos DB, Key Vault, Storage, managed identity, Static Web App, monitoring |
| [pipelines/](pipelines/README.md) | Azure DevOps deploy template; the main pipeline is [azure-pipelines.yml](azure-pipelines.yml) |
| [docs/entra-setup.md](docs/entra-setup.md) | One-off Microsoft Entra External ID setup |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js 24](https://nodejs.org/)
- Docker Desktop, for the local Cosmos DB emulator
- A Microsoft Entra External ID tenant, set up as in [docs/entra-setup.md](docs/entra-setup.md); sign-in won't work without it
- To deploy: the Azure CLI and an Azure subscription

## Run locally

```powershell
docker compose up -d                                   # Cosmos DB emulator on :8081
dotnet run --project src/FilmLibrary.Api               # API on http://localhost:5080 (seeds sample films)
cd src/web; copy public\config.example.json public\config.json   # fill in your Entra ids
npm install; npm run dev                               # http://localhost:5173
```

Fill in the Entra settings in `src/FilmLibrary.Api/appsettings.Development.json` (`Entra:Authority`, `Entra:Audience`) and in `src/web/public/config.json`.

## Change the API

1. Edit [api-spec/main.tsp](api-spec/main.tsp), then run `npm run build` in `api-spec`. This regenerates `api-spec/generated/openapi.json` and `src/web/src/api/schema.d.ts`.
2. Update the C# endpoints and records to match. The `ContractTests` fail until they do.
3. Commit the generated files. The pipeline fails if they're out of date.

## Test

```powershell
dotnet test FilmLibrary.slnx              # API; Cosmos tests are skipped unless the emulator is set (below)
$env:COSMOS_EMULATOR_ENDPOINT = "http://localhost:8081/"; dotnet test FilmLibrary.slnx
cd src/web; npm test                      # React
```

## Deploy

The pipeline deploys `main` to **Dev**, then to **UAT** and **Prod** after approval; every change starts as a short-lived branch and a pull request. For one-time setup (service connection, variable groups, approvals and branch policies), see [pipelines/README.md](pipelines/README.md). To deploy by hand, see [infra/README.md](infra/README.md).
