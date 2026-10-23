# keycloak (optional local sign-in)

A local **Keycloak** in Docker that can replace Microsoft Entra External ID while developing. It's designed as a shared sign-in service for **three apps**, with Film Library as one of them. Azure environments (Dev, UAT, Prod) always use Entra.

## Design

One realm, **`mediahub`**, holds the users. Each app is a **client** in that realm, and each app's API access is a **client scope** that decides what goes into that app's tokens.

```
realm: mediahub ── users (shared accounts, single sign-on, self-service sign-up)
│
├── film-library-web   React SPA      public + PKCE   :5173   scope Films.Read  → aud film-library-api,  scp, birthdate
├── music-library-web  React SPA      public + PKCE   :5174   scope Music.Read  → aud music-library-api, scp
└── admin-console      server web app confidential    :5175   scopes email, roles → email, roles (staff)
```

| App | Exists? | What's unique to it |
|---|---|---|
| **Film Library** (`film-library-web`) | Yes, this repo | The only app whose tokens carry **`birthdate`**. Date of birth is asked for only when signing in to Film Library. It has its own login theme (`filmlibrary`) |
| **Music Library** (`music-library-web`) | Example | Shares accounts and single sign-on, but never sees the birthdate. Its tokens are for `music-library-api` |
| **Admin Console** (`admin-console`) | Example | Confidential client (has a secret), for a server-side app. Gets the user's **roles**; it should let in only users with the **`staff`** role |

### Decisions

- **Birthdate belongs to Film Library.** The `birthdate` user attribute is tied to the `Films.Read` scope in the realm's user profile:
  - **Shown and required only when `Films.Read` is requested.** Signing up through Music Library doesn't ask for a date of birth. A user without one who opens Film Library is asked once ("Update account information"), and it's in their Film Library tokens from then on.
  - **Mapped only by the `Films.Read` scope.** Other apps' tokens never contain it.
  - The date of birth is self-declared, as with Entra. Users can see and edit it in their account; tighten the attribute's `edit` permission if that matters.
- **Tokens match what each API expects.** Scopes add a hard-coded `scp` claim and the API's audience, the same shape as Entra's tokens, so the Film Library API works unchanged with either provider.
- **Roles for authorisation.** Realm role **`staff`**, put into a `roles` claim by the `roles` scope (only the Admin Console requests it). Apps enforce roles themselves. Keycloak could also block non-staff from signing in to a client with a custom authentication flow, which isn't set up here.
- **Branding:** the realm uses the neutral **`mediahub`** theme. Film Library overrides it per client (`login_theme: filmlibrary`) with a theme that extends `mediahub`, changing only the colours and logo.
- **Security defaults:**
  - password policy (at least 10 characters, not the username or email)
  - brute-force lockout after 5 failures
  - 5-minute access tokens
  - refresh-token rotation (each refresh token can be used once)
  - 30-minute idle and 10-hour maximum sessions
  - PKCE on every client
  - `fullScopeAllowed: false` on the SPAs, so they only get the roles and claims their scopes allow

### Users

| Email | Password | Born | Roles | Film Library can't open |
|---|---|---|---|---|
| `chloe@example.com` | `Film-local-chloe1!` | 2014-03-15 | — | 15, 18 |
| `ben@example.com` | `Film-local-ben1!` | 2010-06-01 | — | 18 |
| `alice@example.com` | `Film-local-alice1!` | 1990-01-20 | **staff** | — |

Anyone can **Register** from any app's login page. The admin console is at http://localhost:8180/admin (`admin` / `admin`), under the **mediahub** realm.

## Files

| File | What it is |
|---|---|
| [realm-mediahub.json](realm-mediahub.json) | The whole realm as code: settings, user profile, roles, client scopes, clients, test users |
| [themes/mediahub/](themes/mediahub) | Base login theme over Keycloak's `keycloak.v2` ([mediahub.css](themes/mediahub/login/resources/css/mediahub.css); colours are `--hub-*` variables) |
| [themes/filmlibrary/](themes/filmlibrary) | Film Library's theme: extends `mediahub` with its colours and logo |
| [config.json](config.json) | The React app's `config.json` for Keycloak |

## Run Film Library against Keycloak

```powershell
docker compose up -d                                                   # Cosmos emulator + Keycloak on http://localhost:8180
dotnet run --project src/FilmLibrary.Api --launch-profile keycloak     # API accepting mediahub tokens
copy keycloak\config.json src\web\public\config.json                   # React app signs in with Keycloak
cd src/web; npm run dev                                                # http://localhost:5173
```

To go back to Entra, restore `src/web/public/config.json` from your Entra settings (see [config.example.json](../src/web/public/config.example.json)) and run the API without `--launch-profile keycloak`.

## Add an app

1. Add a client scope for its API: `scp` plus an audience mapper, plus any claims only that app should get.
2. Add a client: public with PKCE for browser and mobile apps, confidential for server apps. Give it redirect URIs, and list the scope under `optionalClientScopes`.
3. Optionally give it its own login theme (`login_theme` attribute) that extends `mediahub`.
4. Run `docker compose up -d --force-recreate keycloak`.

The app then uses authority `http://localhost:8180/realms/mediahub` with its own client id and scope.

## Change things

- **Theme CSS:** edit the CSS and refresh the login page. Theme caching is off in docker-compose.
- **Realm:** edit [realm-mediahub.json](realm-mediahub.json), then `docker compose up -d --force-recreate keycloak`. A plain restart keeps the old realm, because the import skips realms that already exist. Recreating resets the database, including any users who registered.
- **Fully custom React login pages:** possible with [Keycloakify](https://www.keycloakify.dev/), which builds a Keycloak theme from React components.

This is a development setup: dev mode, an H2 database inside the container, http, `admin`/`admin`, and a placeholder Admin Console secret. Running Keycloak for real users needs a production configuration: PostgreSQL, https, real secrets, backups and security updates.
