# pipelines (Azure DevOps)

[azure-pipelines.yml](../azure-pipelines.yml) is the pipeline, and [deploy.yml](deploy.yml) is the deployment template it uses for each environment.

| Stage | Runs | Does |
|---|---|---|
| **Build** | Every PR and every push to `main` | Regenerates the TypeSpec output and fails if it isn't committed; builds and tests the API (with the Cosmos DB emulator); lints, tests and builds React; validates Bicep; publishes the `api`, `web` and `infra` artifacts |
| **Dev** | `main` only | Deploys infrastructure, then the API (and waits for `/healthz`), then the React app with Dev's `config.json` |
| **UAT** | After Dev | Same, for UAT, **after approval** |
| **Prod** | After UAT | Same, for Prod, **after approval**, then tags the commit `prod-<build number>` |

## Release flow

Trunk-based: there are no long-lived `dev` or `uat` branches, and environments are pipeline stages, not branches.

1. Each user story (Boards) gets a short-lived branch off `main`, e.g. `feature/123-age-message`.
2. A pull request into `main` runs the Build stage, needs a reviewer, and links the story (`AB#123` in the title or description).
3. Merging builds the artifacts **once**. Dev deploys automatically; UAT and Prod deploy the **same** artifacts after their approvals, each with its own `config.json`.
4. Only the newest run waits at an approval: a newer run replaces older ones (`lockBehavior: runLatest` with the environments' exclusive lock).
5. Work that isn't ready for Prod goes behind a feature flag rather than staying on a branch.
6. Hotfixes take the same path, with a fast-tracked UAT approval. If `main` has changes that can't ship yet, branch from the latest `prod-*` tag, fix there, then merge back into `main`.
7. Rollback: re-run the Prod stage of the previous successful run.

## One-time setup in Azure DevOps

Create the environments (step 4) **before** the first run; otherwise the pipeline creates them itself, without approvals.

1. **Repo.** Push this repository to Azure Repos, then create a pipeline from `azure-pipelines.yml`.
2. **Service connection.** Project settings → Service connections → *Azure Resource Manager*, using *workload identity federation*, named `filmlibrary-azure`. Give it *Contributor* and *Role Based Access Control Administrator* on the subscription or on the `rg-filmlib-*` resource groups; Bicep creates role assignments, so it needs the second role.
3. **Variable groups** (Pipelines → Library), one per environment: `filmlibrary-dev`, `filmlibrary-uat` and `filmlibrary-prod`:

   | Variable | Example |
   |---|---|
   | `ENTRA_AUTHORITY` | `https://contoso.ciamlogin.com/<tenant-id>/v2.0` (the API trusts this) |
   | `ENTRA_API_CLIENT_ID` | API app registration client id |
   | `ENTRA_SPA_CLIENT_ID` | SPA app registration client id |
   | `ENTRA_SPA_AUTHORITY` | `https://<tenant-id>.ciamlogin.com/<tenant-id>` (use the tenant id, not the subdomain) |
   | `ENTRA_API_SCOPE` | `api://<api-client-id>/Films.Read` |

   Dev and UAT can use the non-production External ID tenant; Prod should have its own tenant, so test accounts never sit beside real customers.

4. **Environments** (Pipelines → Environments): `filmlibrary-dev`, `filmlibrary-uat` and `filmlibrary-prod`. On *uat* and *prod*, add these checks:
   - **Approvals**: testers or the product owner for *uat*, the release owner for *prod*;
   - **Exclusive lock**, so `lockBehavior: runLatest` can replace older waiting runs;
   - optionally **Business hours** on *prod*.
5. **Tag permission.** Project settings → Repositories → the repo → Security → *<project> Build Service (<org>)* → **Create tag**: *Allow*. The Prod stage pushes the `prod-*` tag.
6. **Branch policies** on `main` (Repos → Branches → main → Branch policies):
   - require a minimum of **1 reviewer**;
   - add this pipeline as a **build validation** policy;
   - **require linked work items**, so every change traces back to a user story;
   - require comment resolution.
7. **Redirect URIs.** After the first Dev deployment, add the Static Web App's URL (shown in the pipeline output) as a SPA redirect URI on the SPA app registration ([docs/entra-setup.md](../docs/entra-setup.md)).
