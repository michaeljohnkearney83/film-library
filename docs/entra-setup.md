# Microsoft Entra External ID setup

One-off setup for sign-in. Verified end to end against a live external tenant: a 12-year-old test user gets 403 on 15 and 18 films, an adult opens everything.

Steps 1, 2, 4 and 5 can be done with Azure CLI (`az rest` against Microsoft Graph). Step 3 must be done in the portal, because Azure CLI's token can't manage user flows or custom attributes.

## 0. Create the external tenant

1. You need an Azure subscription (a free account works). The tenant is free up to Microsoft's monthly active user limit; check current pricing.
2. https://entra.microsoft.com → *Entra ID* → *Overview* → *Manage tenants* → *Create* → **External**. Choose the domain name (permanent; it becomes `<name>.onmicrosoft.com` and `<name>.ciamlogin.com`), the location, and a subscription and resource group.
3. Note the **tenant ID**.
4. Before any portal step below, switch to the new tenant: ⚙️ *Settings* → *Directories + subscriptions* → **Switch**. If *Add* buttons on user flows or custom attributes are disabled, you're still in your workforce directory; don't change its self-service sign-up settings.

To use Azure CLI against the tenant:

```powershell
az login --tenant <name>.onmicrosoft.com --use-device-code --allow-no-subscriptions
```

On Windows, `az.cmd` runs through `cmd`, which mangles `(`, `)` and `&` in Graph URLs. Pass request bodies with `--body "@file.json"`, and call `& "C:\Program Files\Microsoft SDKs\Azure\CLI2\python.exe" -IBm azure.cli rest ...` instead of `az rest` for URLs containing `$filter`.

## 1. Register the API

*App registrations* → *New registration*, named **Film Library API**, single tenant.

1. *Expose an API* → set the Application ID URI (`api://<api-client-id>`) → *Add a scope*:
   - name `Films.Read`, consent by admins and users.
2. Note the **client id**. This is `Entra:Audience` / `ENTRA_API_CLIENT_ID`.
3. *Manifest* → set `"requestedAccessTokenVersion": 2` and `"acceptMappedClaims": true`. The second lets step 4's mapped claim appear in access tokens without a custom signing key.

## 2. Register the React app

*New registration*, named **Film Library Web**.

1. *Authentication* → *Add a platform* → **Single-page application**, with redirect URIs:
   - `http://localhost:5173`
   - each environment's Static Web App URL, after its first deployment.
2. *API permissions* → *Add*:
   - *My APIs* → Film Library API → `Films.Read`
   - *Microsoft Graph* → delegated `openid` and `offline_access`
3. **Grant admin consent** for all of them. External tenant users can't consent themselves.
4. Note the **client id**. This is `auth.clientId` / `ENTRA_SPA_CLIENT_ID`.

## 3. Collect the date of birth (portal only)

1. *External Identities* → *Custom user attributes* → *Add*: **Birthdate**, data type String. Entra creates a `b2c-extensions-app` to hold it, and the attribute's full name is `extension_<b2c-extensions-app-client-id-without-dashes>_Birthdate`.
2. *External Identities* → *User flows* → *New user flow*: identity provider **Email with password**; user attributes **Display Name** and **Birthdate** (under *Show more*).
3. Open the flow → *Applications* → *Add application* → **Film Library Web**.
4. Optional: in the flow's *Page layouts*, label Birthdate `Date of birth (yyyy-MM-dd)`. Anything that isn't `yyyy-MM-dd` is treated as "age unknown", which allows U and PG only.

The date of birth is **self-declared** at sign-up, like any online age gate. To correct it, edit the user in the admin center or through Microsoft Graph.

## 4. Put the birthdate in the access token

Create a claims mapping policy and assign it to the **Film Library API** service principal. A policy on the resource's service principal applies to access tokens issued for it.

`policy.json` (replace the extension name):

```json
{
  "displayName": "Film Library birthdate claim",
  "isOrganizationDefault": false,
  "definition": ["{\"ClaimsMappingPolicy\":{\"Version\":1,\"IncludeBasicClaimSet\":\"true\",\"ClaimsSchema\":[{\"Source\":\"user\",\"ExtensionID\":\"extension_<id>_Birthdate\",\"JwtClaimType\":\"birthdate\"}]}}"]
}
```

```powershell
az rest --method post --url https://graph.microsoft.com/v1.0/policies/claimsMappingPolicies --body "@policy.json"
# then, with the returned policy id and the API's service principal object id ("Enterprise applications" → Film Library API):
'{"@odata.id":"https://graph.microsoft.com/v1.0/policies/claimsMappingPolicies/<policy-id>"}' | Set-Content ref.json -Encoding ascii
az rest --method post --url 'https://graph.microsoft.com/v1.0/servicePrincipals/<api-sp-id>/claimsMappingPolicies/$ref' --body "@ref.json"
```

`JwtClaimType` must match `Entra:BirthdateClaim` (default `birthdate`). To check a token, decode it at https://jwt.ms.

## 5. Test users

Sign up through the app, or create users with Microsoft Graph (`POST https://graph.microsoft.com/v1.0/users`):

```json
{
  "accountEnabled": true,
  "displayName": "Chloe",
  "mail": "chloe@example.com",
  "identities": [{ "signInType": "emailAddress", "issuer": "<name>.onmicrosoft.com", "issuerAssignedId": "chloe@example.com" }],
  "passwordProfile": { "password": "<password>", "forceChangePasswordNextSignIn": false },
  "passwordPolicies": "DisablePasswordExpiration",
  "extension_<id>_Birthdate": "2014-03-15"
}
```

| User | Birthdate | Can't open |
|---|---|---|
| chloe | 2014-03-15 | 15, 18 |
| ben | 2010-06-01 | 18 |
| alice | 1990-01-20 | — |

Many sign-ins in quick succession can trigger Entra's throttling; wait a few minutes if the password page stops appearing.

## 6. Brand the sign-in page (portal)

The sign-in page stays Microsoft-hosted (passwords never reach the app), but is styled to match it. Graph's branding API doesn't support personal Microsoft accounts, so do this in the portal: *Company branding* → *Default sign-in* → *Edit*.

| Tab | Setting | Value |
|---|---|---|
| Basics | Page background color | `#F6F6F8` |
| Layout | Template | Full-screen background |
| Layout | Show header / Show footer | On / On |
| Layout | Custom CSS | upload [branding/sign-in.css](branding/sign-in.css) |
| Header | Logo | upload [branding/header-logo.png](branding/header-logo.png) (white, for the blue bar) |
| Sign-in form | Banner logo | upload [branding/banner-logo.png](branding/banner-logo.png) (replaces the "FILM LIBRARY" text on the card) |
| Sign-in form | Username hint text | `Email address` |
| Sign-in form | Sign-in page text | `Films are shown according to your age and their UK BBFC rating.` |

Changes can take a few minutes to appear. The CSS only uses Microsoft's `.ext-*` classes; colours mirror `src/web/src/index.css`. Entra silently drops rules it doesn't accept (the footer link colour, for example), so check the live page after each upload; injecting the CSS locally in a browser won't show which rules were dropped.

## Settings

| Where | Value |
|---|---|
| API `Entra:Authority` and web `auth.authority` / `ENTRA_AUTHORITY` | `https://<name>.ciamlogin.com/<tenant-id>/v2.0` (shared by both) |
| API `Entra:Audience` / `ENTRA_API_CLIENT_ID` | API client id |
| Web `auth.clientId` / `ENTRA_SPA_CLIENT_ID` | SPA client id |
| Web `auth.scope` / `ENTRA_API_SCOPE` | `offline_access api://<api-client-id>/Films.Read` (the pipeline adds `offline_access` to `ENTRA_API_SCOPE`) |
