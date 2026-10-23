# Observability

Film Library sends traces, metrics, logs and exceptions from the browser, the API and Cosmos DB to one **Application Insights** resource per environment. Each user action is joined into a single trace: browser → API → Cosmos DB.

```
React app (FilmLibrary.Web)                     API (FilmLibrary.Api)                    Azure platform
  page views, load times, exceptions,  ──traceparent──▶  requests, logs, exceptions,       App Service, Cosmos DB,
  calls to the API (as users saw them)                    Cosmos DB spans (with RU),        Key Vault logs and metrics
        │  Application Insights JS SDK                    filmlibrary.film.views metric           │ diagnostic settings
        └──────────────────────────────▶  Application Insights  ◀── OpenTelemetry ──┘              ▼
                                                 (workspace-based) ──────────────────▶  Log Analytics workspace
```

## What's collected

| Source | What | How |
|---|---|---|
| **Browser** | Page views and load times; unhandled errors and promise rejections; React render errors; failed sign-ins; every call to the API, with its status and duration | [src/web/src/telemetry.ts](../src/web/src/telemetry.ts), using the Application Insights JS SDK. It only runs when `config.json` has `telemetry.connectionString`. Only the API receives the `traceparent` header, not the sign-in provider |
| **API** | Requests, logs, unhandled exceptions, outgoing HTTP | OpenTelemetry with the Azure Monitor distro ([TelemetrySetup.cs](../src/FilmLibrary.Api/Observability/TelemetrySetup.cs)). `/healthz` probes aren't traced |
| **Cosmos DB** | One span per operation, with status, request charge (RU), row count and region | The Cosmos SDK's distributed tracing (`Azure.Cosmos.Operation`) |
| **Business metric** | `filmlibrary.film.views`: requests for a film's details by `outcome` (`shown`, `age_restricted`, `not_found`), `film.rating` and `viewer.birthdate_known`. The request's trace also gets `film.id`, `film.rating` and `filmlibrary.outcome` | [FilmMetrics.cs](../src/FilmLibrary.Api/Observability/FilmMetrics.cs) |
| **Health** | `/healthz` = the process is up; `/healthz/ready` = it can read from Cosmos DB (network, managed identity and data-plane role). The pipeline waits for *ready* after each deployment | [FilmStoreHealthCheck.cs](../src/FilmLibrary.Api/Observability/FilmStoreHealthCheck.cs) |
| **Platform** | App Service HTTP and console logs, Cosmos DB data-plane requests, Key Vault audit events, App Service metrics | Diagnostic settings to Log Analytics ([infra/main.bicep](../infra/main.bicep)) |

**Privacy.** No birthdate, age, name or email is sent. Users are identified only by the token's opaque `sub`. The browser SDK sets no cookies, so the site needs no consent banner; in exchange, users and sessions are counted per page load.

## Where to look in Azure

Portal → resource group `rg-filmlib-<env>` → **appi-filmlib-<env>** (Application Insights):

| Page | Use it for |
|---|---|
| **Application map** | The browser, the API and Cosmos DB as boxes, with call rates and failures between them |
| **Failures** / **Performance** | Failing or slow operations. Select one and open a sample for the whole trace, from the browser call to the Cosmos DB read |
| **Transaction search** | Find one request, page view or exception |
| **Live metrics** | Requests and failures as they happen (not stored, and free) |
| **Workbooks → Film Library (<env>)** | The project dashboard: requests and failures, film views by rating and outcome, API operations, dependencies, browser calls and exceptions ([infra/workbook.json](../infra/workbook.json)) |
| **Logs** | KQL queries, examples below |

Telemetry takes a few minutes to appear. In Application Insights a request is *failed* only for 5xx. The API's 403 (too young) and 404 are expected answers. A browser call with result code `0` was cancelled or never reached the API.

### Useful queries (Logs)

```kusto
// Age-restricted views by rating
customMetrics
| where name == 'filmlibrary.film.views' and customDimensions['outcome'] == 'age_restricted'
| summarize Refused = sum(valueSum) by Rating = tostring(customDimensions['film.rating'])

// Everything in one user action, browser and API, in order (paste an operation id)
union requests, dependencies, pageViews, exceptions, traces
| where operation_Id == '<operation id>'
| project timestamp, cloud_RoleName, itemType, name, resultCode, duration
| order by timestamp asc

// Most expensive Cosmos DB operations
dependencies
| where customDimensions has 'azure.cosmosdb.request.request_charge'
| extend RU = todouble(customDimensions['azure.cosmosdb.request.request_charge'])
| summarize Calls = count(), AvgRU = avg(RU), P95ms = percentile(duration, 95) by name
| order by AvgRU desc
```

## Alerts

Alerts are created only when the environment's variable group has **`ALERT_EMAIL`** (or it's set in your shell when deploying by hand). Each alert rule has a small monthly charge, so Dev has none unless you add it.

| Alert | Fires when (15-minute window) | Severity |
|---|---|---|
| `api-server-errors` | More than 5 HTTP 5xx from App Service, including when the app can't start | 1 |
| `api-slow` | API requests average over 2 seconds | 3 |
| `browser-exceptions` | More than 5 exceptions in the browser | 2 |
| `cosmos-throttled` | Any Cosmos DB request throttled (429) | 2 |
| `web-down`, `api-down` | The availability test fails from 2 or more regions (only where availability tests are on) | 1 |

**Availability tests** (Prod only, `enableAvailabilityTests`) request the web app's home page and the API's `/healthz/ready` from three regions every 15 minutes. They're charged per run.

Application Insights also turns on **smart detection** by itself, which emails the subscription's owners about unusual failure rates.

## Cost

| Item | Dev | UAT | Prod |
|---|---|---|---|
| Log Analytics ingestion | Capped at 0.15 GB a day, within the monthly free allowance | Capped at 0.5 GB a day | No cap |
| Alert rules | Only with `ALERT_EMAIL` | Only with `ALERT_EMAIL` | Only with `ALERT_EMAIL` |
| Availability tests | Off | Off | On |
| Workbook, application map, live metrics | Free | Free | Free |

Once the daily cap is reached, Log Analytics **stops collecting for the rest of the day** (until the reset at a fixed UTC time). The caps are `logsDailyCapGb` in [infra/env/](../infra/env). For current prices, see Azure Monitor pricing.

## Locally

`docker compose up -d` also starts the **Aspire dashboard** at http://localhost:18888. The API's launch profiles send OpenTelemetry to it (`OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`), so you get the same traces, Cosmos DB spans, logs and metrics as in Azure, kept in memory only. Without the dashboard running, nothing is exported.

Browser telemetry is off locally, because `config.json` has no `telemetry` section. To try it, add `"telemetry": { "connectionString": "<Dev's connection string>" }`. Dev's connection string is on the Application Insights resource's **Overview** page.
