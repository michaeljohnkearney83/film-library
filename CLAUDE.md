# CLAUDE.md

React + .NET 10 film library on Azure, with age-restricted content by UK BBFC rating. Sign-in uses Microsoft Entra External ID; data lives in Cosmos DB; CI/CD runs in Azure DevOps.

## Layout

`api-spec/` (TypeSpec contract) · `src/FilmLibrary.Api/` (minimal API) · `src/web/` (React + Vite + TS) · `tests/FilmLibrary.Api.Tests/` · `infra/` (Bicep) · `azure-pipelines.yml` + `pipelines/` · `docs/entra-setup.md`

## Commands

- Contract: `npm run build` in `api-spec` regenerates `api-spec/generated/openapi.json` **and** `src/web/src/api/schema.d.ts`.
- API: `dotnet run --project src/FilmLibrary.Api` (http://localhost:5080); `dotnet test FilmLibrary.slnx`.
- Cosmos emulator: `docker compose up -d`; set `COSMOS_EMULATOR_ENDPOINT=http://localhost:8081/` to run the Cosmos integration tests (they're skipped otherwise).
- Web: in `src/web`: `npm run dev`, `npm test`, `npm run lint`, `npm run build` (runs `tsc -b`).
- Bicep: `az bicep build --file infra/main.bicep` (or a standalone `bicep build`).

## Documentation

- When a change affects how to run, configure or use a project, update that project's README.md and this file in the same task.
- Keep fast-changing details (counts, versions, data contents) out of docs; point to the source instead.

## Testing

- When adding or changing logic, add or update tests in the same task: xUnit 2 with hand-written fakes for .NET; Vitest + Testing Library for React. No mocking libraries in .NET.
- Test behaviour and edge cases, not implementation details. For a bug fix, first write a test that fails because of the bug.
- Run `dotnet test FilmLibrary.slnx` and `npm test` (src/web) before finishing, and report failures as they are.
- Never weaken, skip or delete an existing test to make it pass without saying so.

## Architecture rules

- **TypeSpec is the contract.** Change `api-spec/main.tsp` first, regenerate, then make C# match; `ContractTests` compare the API's own OpenAPI with the TypeSpec output. Never hand-edit `generated/` or `schema.d.ts`. React gets types only from `schema.d.ts` via `src/web/src/api/client.ts`.
- **The server enforces age.** Age comes from the token's birthdate claim (`Entra:BirthdateClaim`, yyyy-MM-dd), calculated in UK time (`Viewer.cs`). 12A counts as 12. A missing or invalid birthdate means U/PG only. The client only displays `canView` and the 403 message.
- **Auth.** JWT bearer from Entra External ID; every film route requires the `Films.Read` scope (`AuthPolicies.FilmsRead`). The React app uses MSAL redirects; tokens refresh silently via `acquireTokenSilent`.
- **No secrets.** In Azure the API uses a user-assigned managed identity for Cosmos DB (data-plane RBAC; local auth is disabled), Key Vault and Storage. Only the local emulator uses a key, which is the well-known public emulator key.
- **Failures become messages.** .NET services and `client.ts` turn network errors, timeouts and unreadable replies into one error type (`ApiError` in React). UI code shows them and never crashes. Requests the user cancelled (`AbortError`) are not errors.
- **React async state.** Effects use an `AbortController`. Replies for a film the viewer has moved away from are ignored, and a test guards this. Don't call `setState` synchronously in an effect (the oxlint rule); derive state during render or set it in the event handler.
- **Runtime config.** The web build is environment-agnostic; `/config.json` is written per environment by the pipeline. Never bake environment URLs into the bundle.

## Gotchas

- `openapi-typescript` needs TypeScript 5, but the web app uses TypeScript 6, so type generation lives in `api-spec` (which has TS 5), not `src/web`.
- ASP.NET describes integers as `["integer","string"]` in OpenAPI; `ContractTests` normalise this.
- The Cosmos SDK is configured with System.Text.Json, but it **still loads Newtonsoft.Json at runtime**, so the API must reference it. Don't remove it: the test project pulls Newtonsoft in on its own, which hid this once, and `DeploymentDependencyTests` now guards it. `Rating` is serialized as certificate text through `JsonStringEnumMemberName`.
- With managed identity, the API can't create Cosmos databases or containers; Bicep creates them. The seeder only creates them when using the emulator key.
- The Entra token claims setup (birthdate → access token) is manual and was not verified against a live tenant; see `docs/entra-setup.md`.
