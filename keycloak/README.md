# keycloak (optional local sign-in)

Run the app locally with **Keycloak** in Docker instead of Microsoft Entra External ID. Keycloak issues tokens with the same shape the API expects (`aud`, `scp: Films.Read`, `birthdate`), so the API's age rules work unchanged. Azure environments (Dev, UAT, Prod) always use Entra.

| File | What it is |
|---|---|
| [realm-filmlibrary.json](realm-filmlibrary.json) | The `filmlibrary` realm: the `film-library-web` client (public, PKCE), the `Films.Read` scope (adds `scp`, the `film-library-api` audience and `birthdate` to access tokens), a sign-up form that asks for display name and date of birth, and three test users |
| [themes/filmlibrary/](themes/filmlibrary) | Login theme: Keycloak's `keycloak.v2` with the app's colours and logo ([filmlibrary.css](themes/filmlibrary/login/resources/css/filmlibrary.css)) |
| [config.json](config.json) | The React app's `config.json` for Keycloak |

## Run

```powershell
docker compose up -d                                                   # Cosmos emulator + Keycloak on http://localhost:8180
dotnet run --project src/FilmLibrary.Api --launch-profile keycloak     # API accepting Keycloak tokens
copy keycloak\config.json src\web\public\config.json                   # React app signs in with Keycloak
cd src/web; npm run dev                                                # http://localhost:5173
```

To go back to Entra, restore `src/web/public/config.json` from your Entra settings (see [config.example.json](../src/web/public/config.example.json)) and run the API without `--launch-profile keycloak`.

## Users

These exist only in your local Keycloak:

| Email | Password | Born | Can't open |
|---|---|---|---|
| `chloe@example.com` | `Film-local-chloe1!` | 2014-03-15 | 15, 18 |
| `ben@example.com` | `Film-local-ben1!` | 2010-06-01 | 18 |
| `alice@example.com` | `Film-local-alice1!` | 1990-01-20 | — |

Or select **Register** on the login page. The admin console is at http://localhost:8180/admin (`admin` / `admin`).

## Change things

- **Theme CSS:** edit [filmlibrary.css](themes/filmlibrary/login/resources/css/filmlibrary.css) and refresh the login page. Theme caching is off in docker-compose.
- **Realm** (clients, users, mappers, sign-up fields): edit [realm-filmlibrary.json](realm-filmlibrary.json), then `docker compose up -d --force-recreate keycloak`. A plain restart keeps the old realm, because the import skips realms that already exist. Recreating resets all local users, including any you registered.
- **Fully custom React login pages:** possible with [Keycloakify](https://www.keycloakify.dev/), which builds a Keycloak theme from React components.

This is a development setup (dev mode, in-memory database, http, admin/admin). Running Keycloak for real users needs a production configuration: a database, https, backups and security updates.
