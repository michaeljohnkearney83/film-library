# api-spec (TypeSpec contract)

[main.tsp](main.tsp) is the single definition of the Film Library API: its routes, models, the `Rating` enum and its error responses. Everything else follows from it.

```powershell
npm install
npm run build
```

`npm run build` writes:

| File | Used by |
|---|---|
| `generated/openapi.json` | The API's `ContractTests`, and anyone who wants an OpenAPI document |
| `../src/web/src/api/schema.d.ts` | The React app's typed API client (via `openapi-typescript`) |

Both generated files are committed. The pipeline regenerates them and fails if they differ from what's in the repo, so a contract change can't be merged without them.

## Changing the contract

1. Edit `main.tsp`, then run `npm run build`.
2. Update the C# records and endpoints in `src/FilmLibrary.Api/Films` to match. `ContractTests` compare the two OpenAPI documents (operations, response codes, fields and types, enum values) and fail until they agree.
3. Fix any React type errors (`npm run typecheck` in `src/web`).

Type generation runs here, not in `src/web`, because `openapi-typescript` requires TypeScript 5 and the web app uses TypeScript 6.
