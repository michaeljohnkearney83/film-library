# web (React front end)

React + TypeScript + Vite. You sign in through an OpenID Connect provider (Microsoft Entra External ID in Azure, or the local Keycloak), then browse films. The lock icon and the "You must be 15 or over…" message come from the API's answer; the app never decides access itself.

## Run

```powershell
copy public\config.example.json public\config.json   # then fill in your Entra ids
npm install
npm run dev                                           # http://localhost:5173
```

The API must be running (see the [root README](../../README.md)), and `http://localhost:5173` must be a registered redirect URI on the SPA app registration ([docs/entra-setup.md](../../docs/entra-setup.md)).

To sign in with the local Keycloak instead, copy [keycloak/config.json](../../keycloak/config.json) to `public/config.json` and see [keycloak/README.md](../../keycloak/README.md).

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
| `auth.authority` | The provider's OpenID Connect authority. Entra: `https://<tenant-subdomain>.ciamlogin.com/<tenant-id>/v2.0`. Keycloak: `http://localhost:8180/realms/mediahub` |
| `auth.clientId` | The React app's client id (Entra SPA app registration, or `film-library-web` in Keycloak) |
| `auth.scope` | Scopes besides `openid profile`. Entra: `offline_access api://<api-client-id>/Films.Read`. Keycloak: `Films.Read` |

## Structure

```
src/main.tsx               Loads config, finishes any sign-in redirect, creates the API client, renders the app
src/App.tsx                Sign-in page or films page depending on whether you're signed in
src/auth/oidc.ts           oidc-client-ts: sign-in by redirect (code + PKCE), token renewal with the refresh token
src/api/schema.d.ts        GENERATED from api-spec; don't edit
src/api/client.ts          Typed API client; turns failures into ApiError and a 403 into "restricted"
src/components/            FilmsPage, SignInPage, RatingBadge
public/staticwebapp.config.json   Static Web Apps routing (SPA fallback, no-cache on config.json)
```

To change the API's shape, edit `api-spec/main.tsp` and run `npm run build` there; `schema.d.ts` is regenerated.
