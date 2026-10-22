# infra (Bicep)

[main.bicep](main.bicep) defines one environment inside an existing resource group. Parameters per environment are in [env/](env).

| Resource | Purpose |
|---|---|
| User-assigned managed identity | The API's identity for Cosmos DB, Key Vault and Storage; no keys or connection strings |
| App Service plan + Web App (Linux, .NET 10) | The API; health check on `/healthz` |
| Cosmos DB (serverless) | `filmlibrary` database with a `films` container; key access disabled; the identity has the data-plane *Built-in Data Contributor* role |
| Key Vault (RBAC) | Holds the Application Insights connection string, read by the API through a Key Vault reference |
| Storage account | Reserved for assets such as posters; shared-key access disabled |
| Static Web App (Free) | The React app |
| Log Analytics + Application Insights | Telemetry, plus diagnostic settings for the API, Cosmos DB and Key Vault |

## Deploy by hand

```powershell
az login
az group create --name rg-filmlib-dev --location uksouth
$env:ENTRA_AUTHORITY = "https://<tenant>.ciamlogin.com/<tenant-id>/v2.0"
$env:ENTRA_API_CLIENT_ID = "<api-client-id>"
az deployment group create --resource-group rg-filmlib-dev --parameters infra/env/dev.bicepparam
```

The outputs give the API and web URLs. Then deploy the code, as the pipeline does in [pipelines/deploy.yml](../pipelines/deploy.yml).

To validate without deploying: `az bicep build --file infra/main.bicep`, or add `--what-if` to the deployment command.

## Not included yet

- **Private endpoints and VNet integration** for Cosmos DB, Key Vault and Storage. Those resources are currently reachable over the internet, protected by Entra RBAC. This is the next hardening step.
- **Custom domains and Front Door/WAF.**
