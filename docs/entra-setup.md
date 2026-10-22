# Microsoft Entra External ID setup

One-off setup for sign-in. You need an **external tenant**: Entra admin center → *Overview* → *Manage tenants* → *Create* → **External**.

> **Not verified against a live tenant.** In particular, step 4 (putting the birthdate into the access token) is the part most likely to need adjusting. Check the steps against Microsoft's current External ID documentation. If the claim doesn't appear, decode a token at https://jwt.ms and set `Entra:BirthdateClaim` to whatever name the claim actually has.

## 1. Register the API

*App registrations* → *New registration*, named **Film Library API**, single tenant.

1. *Expose an API* → set the Application ID URI (`api://<api-client-id>`) → *Add a scope*:
   - name `Films.Read`, consent by admins and users.
2. Note the **client id**. This is `Entra:Audience` / `ENTRA_API_CLIENT_ID`.
3. *Manifest* → set `"requestedAccessTokenVersion": 2` and `"acceptMappedClaims": true`. The second is needed for step 4's claim to appear in access tokens.

## 2. Register the React app

*New registration*, named **Film Library Web**.

1. *Authentication* → *Add a platform* → **Single-page application**, with redirect URIs:
   - `http://localhost:5173`
   - each environment's Static Web App URL, after its first deployment.
2. *API permissions* → *Add* → *My APIs* → Film Library API → `Films.Read` → **Grant admin consent**.
3. Note the **client id**. This is `entra.clientId` / `ENTRA_SPA_CLIENT_ID`.

## 3. Collect the date of birth

1. *External Identities* → *Custom user attributes* → add **Birthdate** (String). Users must enter it as `yyyy-MM-dd`; anything else is treated as "age unknown", which allows U and PG only.
2. *User flows* → create a **Sign up and sign in** flow (email + password). Under *User attributes*, collect **Display Name** and **Birthdate**. Under *Applications*, add **Film Library Web**.

The date of birth is **self-declared** at sign-up, like any online age gate. To correct it, edit the user in the admin center (or through Microsoft Graph).

## 4. Put the birthdate in the access token

*Enterprise applications* → **Film Library API** → *Single sign-on* → *Attributes & Claims* → *Add new claim*:

- **Name:** `birthdate`
- **Source attribute:** the Birthdate extension attribute (`user.extension_<b2c-extensions-app-id>_Birthdate`)

The claim name must match `Entra:BirthdateClaim` (default `birthdate`).

## 5. Test users

Sign up through the app three times, or create users in the admin center and set Birthdate:

| User | Birthdate | Can't open |
|---|---|---|
| chloe | 2014-03-15 | 15, 18 |
| ben | 2010-06-01 | 18 |
| alice | 1990-01-20 | — |

## Settings

| Where | Value |
|---|---|
| API `Entra:Authority` / `ENTRA_AUTHORITY` | `https://<tenant-subdomain>.ciamlogin.com/<tenant-id>/v2.0` |
| API `Entra:Audience` / `ENTRA_API_CLIENT_ID` | API client id |
| Web `entra.authority` / `ENTRA_SPA_AUTHORITY` | `https://<tenant-subdomain>.ciamlogin.com/` |
| Web `entra.clientId` / `ENTRA_SPA_CLIENT_ID` | SPA client id |
| Web `entra.apiScope` / `ENTRA_API_SCOPE` | `api://<api-client-id>/Films.Read` |
