# web (React front end)

React + TypeScript + Vite. You sign in with Microsoft Entra External ID (MSAL), then browse films. The lock icon and the "You must be 15 or over…" message come from the API's answer; the app never decides access itself.

## Run

```powershell
copy public\config.example.json public\config.json   # then fill in your Entra ids
npm install
npm run dev                                           # http://localhost:5173
```

The API must be running (see the [root README](../../README.md)), and `http://localhost:5173` must be a registered redirect URI on the SPA app registration ([docs/entra-setup.md](../../docs/entra-setup.md)).

## Scripts

| Script | Does |
|---|---|
| `npm run dev` | Dev server with hot reload |
| `npm test` | Vitest + Testing Library |
| `npm run lint` | oxlint |
| `npm run typecheck` | `tsc -b` |
| `npm run build` | Type-check and build to `dist/` |

## Configuration

Settings are read at runtime from `/config.json`, so one build can be deployed to every environment. The pipeline writes this file per environment.

| Key | Meaning |
|---|---|
| `apiBaseUrl` | The API's address |
| `entra.clientId` | SPA app registration client id |
| `entra.authority` | `https://<tenant>.ciamlogin.com/` |
| `entra.apiScope` | `api://<api-client-id>/Films.Read` |

## Structure

```
src/main.tsx               Loads config, creates MSAL and the API client, renders the app
src/App.tsx                Sign-in page or films page depending on whether you're signed in
src/auth/msal.ts           MSAL setup; gets tokens silently and redirects only when it must
src/api/schema.d.ts        GENERATED from api-spec; don't edit
src/api/client.ts          Typed API client; turns failures into ApiError and a 403 into "restricted"
src/components/            FilmsPage, SignInPage, RatingBadge
public/staticwebapp.config.json   Static Web Apps routing (SPA fallback, no-cache on config.json)
```

To change the API's shape, edit `api-spec/main.tsp` and run `npm run build` there; `schema.d.ts` is regenerated.
