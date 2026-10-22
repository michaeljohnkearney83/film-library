# pipelines (Azure DevOps)

[azure-pipelines.yml](../azure-pipelines.yml) is the pipeline, and [deploy.yml](deploy.yml) is the deployment template it uses for each environment.

| Stage | Runs | Does |
|---|---|---|
| **Build** | Every PR and every push to `main` | Regenerates the TypeSpec output and fails if it isn't committed; builds and tests the API (with the Cosmos DB emulator); lints, tests and builds React; validates Bicep; publishes the `api`, `web` and `infra` artifacts |
| **Dev** | `main` only | Deploys infrastructure, then the API (and waits for `/healthz`), then the React app with Dev's `config.json` |
| **Test** | After Dev | Same, for Test, **after approval** |
| **Prod** | After Test | Same, for Prod, **after approval** |

## One-time setup in Azure DevOps

1. **Repo.** Push this repository to Azure Repos, then create a pipeline from `azure-pipelines.yml`.
2. **Service connection.** Project settings → Service connections → *Azure Resource Manager*, using *workload identity federation*, named `filmlibrary-azure`. Give it *Contributor* and *Role Based Access Control Administrator* on the subscription or on the `rg-filmlib-*` resource groups; Bicep creates role assignments, so it needs the second role.
3. **Variable groups** (Pipelines → Library), one per environment: `filmlibrary-dev`, `filmlibrary-test` and `filmlibrary-prod`:

   | Variable | Example |
   |---|---|
   | `ENTRA_AUTHORITY` | `https://contoso.ciamlogin.com/<tenant-id>/v2.0` (the API trusts this) |
   | `ENTRA_API_CLIENT_ID` | API app registration client id |
   | `ENTRA_SPA_CLIENT_ID` | SPA app registration client id |
   | `ENTRA_SPA_AUTHORITY` | `https://contoso.ciamlogin.com/` |
   | `ENTRA_API_SCOPE` | `api://<api-client-id>/Films.Read` |

4. **Environments** (Pipelines → Environments): `filmlibrary-dev`, `filmlibrary-test` and `filmlibrary-prod`. Add an **Approvals** check to *test* and *prod*.
5. **Branch policies** on `main` (Repos → Branches → main → Branch policies):
   - require a minimum of **1 reviewer**;
   - add this pipeline as a **build validation** policy;
   - require linked work items and comment resolution, if you want them.
6. **Redirect URIs.** After the first Dev deployment, add the Static Web App's URL (shown in the pipeline output) as a SPA redirect URI on the SPA app registration ([docs/entra-setup.md](../docs/entra-setup.md)).
