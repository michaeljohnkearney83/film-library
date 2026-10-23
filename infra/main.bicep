// Film Library: one environment (dev, uat or prod) in an existing resource group.
// Deploy: az deployment group create -g rg-filmlib-dev --parameters infra/env/dev.bicepparam
targetScope = 'resourceGroup'

@description('Environment name, used in resource names.')
@allowed(['dev', 'uat', 'prod'])
param environmentName string

@description('Region for everything except the Static Web App.')
param location string = resourceGroup().location

@description('Region for Cosmos DB. Cosmos capacity for new accounts varies by region, separately from App Service quota.')
param cosmosLocation string = location

@description('Static Web Apps are only offered in some regions.')
@allowed(['westeurope', 'eastus2', 'centralus', 'westus2', 'eastasia'])
param staticWebAppLocation string = 'eastus2'

@description('Entra External ID authority the API trusts, e.g. https://contoso.ciamlogin.com/<tenant-id>/v2.0')
param entraAuthority string

@description('Client id of the API app registration; Entra puts it in the token audience.')
param entraApiClientId string

@description('Access-token claim that carries the user\'s date of birth (yyyy-MM-dd).')
param birthdateClaim string = 'birthdate'

@description('App Service plan SKU for the API.')
param appServiceSku string = 'B1'

@description('Daily Log Analytics ingestion cap in GB, as text (e.g. \'0.15\'); \'-1\' means no cap. Ingestion stops for the rest of the day once it is reached.')
param logsDailyCapGb string = '-1'

@description('Email address for alerts. Empty creates no alerts (each alert rule has a small monthly charge).')
param alertEmail string = ''

@description('Probe the web app and API from several Azure regions every 15 minutes. Charged per test run.')
param enableAvailabilityTests bool = false

var suffix = take(uniqueString(resourceGroup().id, environmentName), 6)
var tags = {
  app: 'film-library'
  environment: environmentName
}

// Built-in role ids
var keyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
var storageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var cosmosDataContributor = '00000000-0000-0000-0000-000000000002' // Cosmos DB Built-in Data Contributor (data plane)

// ---------------------------------------------------------------------------
// Identity: the API reaches Cosmos DB, Key Vault and Storage as this identity. No keys or connection strings.
// ---------------------------------------------------------------------------
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-filmlib-${environmentName}'
  location: location
  tags: tags
}

// ---------------------------------------------------------------------------
// Monitoring
// ---------------------------------------------------------------------------
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-filmlib-${environmentName}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: json(logsDailyCapGb) }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-filmlib-${environmentName}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

// ---------------------------------------------------------------------------
// Key Vault (RBAC). Holds settings the API reads through Key Vault references.
// ---------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-filmlib-${environmentName}-${suffix}'
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: environmentName == 'prod' ? true : null
  }
}

resource appInsightsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ApplicationInsightsConnectionString'
  properties: {
    value: appInsights.properties.ConnectionString
  }
}

resource identityReadsSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, identity.id, keyVaultSecretsUser)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUser)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// Storage (reserved for film posters and other assets). Shared-key access is off: identity only.
// ---------------------------------------------------------------------------
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'stfilmlib${environmentName}${suffix}'
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
  }
}

resource identityUsesStorage 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, identity.id, storageBlobDataContributor)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributor)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// Cosmos DB (serverless). Local (key) auth is disabled: the API signs in with its managed identity.
// ---------------------------------------------------------------------------
resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: 'cosmos-filmlib-${environmentName}-${suffix}'
  location: cosmosLocation
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [{ locationName: cosmosLocation, failoverPriority: 0 }]
    capabilities: [{ name: 'EnableServerless' }]
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
    disableLocalAuth: true
    minimalTlsVersion: 'Tls12'
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmos
  name: 'filmlibrary'
  properties: {
    resource: { id: 'filmlibrary' }
  }
}

resource filmsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: cosmosDatabase
  name: 'films'
  properties: {
    resource: {
      id: 'films'
      partitionKey: { paths: ['/id'], kind: 'Hash' }
    }
  }
}

resource identityUsesCosmos 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmos
  name: guid(cosmos.id, identity.id, cosmosDataContributor)
  properties: {
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/${cosmosDataContributor}'
    principalId: identity.properties.principalId
    scope: cosmos.id
  }
}

// ---------------------------------------------------------------------------
// React app: Azure Static Web Apps
// ---------------------------------------------------------------------------
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-filmlib-${environmentName}'
  location: staticWebAppLocation
  tags: tags
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

// ---------------------------------------------------------------------------
// .NET API: Linux App Service
// ---------------------------------------------------------------------------
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-filmlib-${environmentName}'
  location: location
  tags: tags
  kind: 'linux'
  sku: { name: appServiceSku }
  properties: {
    reserved: true
  }
}

resource api 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-filmlib-api-${environmentName}-${suffix}'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: identity.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServiceSku != 'F1'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: appServiceSku != 'F1' ? '/healthz' : null // App Service health checks need a paid tier; the pipeline still polls /healthz
      appSettings: [
        { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
        { name: 'Cosmos__Endpoint', value: cosmos.properties.documentEndpoint }
        { name: 'Cosmos__Database', value: cosmosDatabase.name }
        { name: 'Cosmos__FilmsContainer', value: filmsContainer.name }
        { name: 'Cosmos__ManagedIdentityClientId', value: identity.properties.clientId }
        { name: 'Cosmos__SeedSampleData', value: string(environmentName != 'prod') }
        { name: 'Entra__Authority', value: entraAuthority }
        { name: 'Entra__Audience', value: entraApiClientId }
        { name: 'Entra__BirthdateClaim', value: birthdateClaim }
        { name: 'Cors__AllowedOrigins__0', value: 'https://${staticWebApp.properties.defaultHostname}' }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=${appInsightsSecret.name})'
        }
      ]
    }
  }
  // The Key Vault reference only resolves once the identity can read secrets.
  dependsOn: [identityReadsSecrets]
}

// ---------------------------------------------------------------------------
// Diagnostic settings → Log Analytics
// ---------------------------------------------------------------------------
resource apiDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: api
  name: 'to-log-analytics'
  properties: {
    workspaceId: logs.id
    logs: [{ categoryGroup: 'allLogs', enabled: true }]
    metrics: [{ category: 'AllMetrics', enabled: true }]
  }
}

resource cosmosDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: cosmos
  name: 'to-log-analytics'
  properties: {
    workspaceId: logs.id
    logs: [{ categoryGroup: 'allLogs', enabled: true }]
  }
}

resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: keyVault
  name: 'to-log-analytics'
  properties: {
    workspaceId: logs.id
    logs: [{ categoryGroup: 'allLogs', enabled: true }]
  }
}

// ---------------------------------------------------------------------------
// Dashboard, availability tests and alerts
// ---------------------------------------------------------------------------
resource workbook 'Microsoft.Insights/workbooks@2023-06-01' = {
  name: guid(resourceGroup().id, 'filmlibrary-overview')
  location: location
  tags: tags
  kind: 'shared'
  properties: {
    displayName: 'Film Library (${environmentName})'
    category: 'workbook'
    sourceId: appInsights.id
    serializedData: loadTextContent('workbook.json')
  }
}

// The web app's home page, and the API's readiness check (which reads from Cosmos DB).
var availabilityTargets = ['web', 'api']

resource availabilityTests 'Microsoft.Insights/webtests@2022-06-15' = [
  for target in availabilityTargets: if (enableAvailabilityTests) {
    name: 'avail-filmlib-${target}-${environmentName}'
    location: location
    // Links the test to Application Insights, where its results appear under Availability.
    tags: union(tags, { 'hidden-link:${appInsights.id}': 'Resource' })
    kind: 'standard'
    properties: {
      SyntheticMonitorId: 'avail-filmlib-${target}-${environmentName}'
      Name: 'Film Library ${target} (${environmentName})'
      Kind: 'standard'
      Enabled: true
      Frequency: 900
      Timeout: 30
      RetryEnabled: true
      // The ids are historical and don't match the names: UK South, UK West, North Europe.
      Locations: [{ Id: 'emea-ru-msa-edge' }, { Id: 'emea-se-sto-edge' }, { Id: 'emea-gb-db3-azr' }]
      Request: {
        RequestUrl: target == 'web' ? 'https://${staticWebApp.properties.defaultHostname}/' : 'https://${api.properties.defaultHostName}/healthz/ready'
        HttpVerb: 'GET'
        ParseDependentRequests: false
      }
      ValidationRules: { ExpectedHttpStatusCode: 200, SSLCheck: true, SSLCertRemainingLifetimeCheck: 7 }
    }
  }
]

var alertsEnabled = !empty(alertEmail)

resource alertEmailGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (alertsEnabled) {
  name: 'ag-filmlib-${environmentName}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'filmlib-${take(environmentName, 4)}'
    enabled: true
    emailReceivers: [{ name: 'team', emailAddress: alertEmail, useCommonAlertSchema: true }]
  }
}

// Metric alerts over the last 15 minutes, checked every 5.
var metricAlerts = [
  {
    name: 'api-server-errors'
    description: 'The API returned 5xx responses (App Service front end, so it also fires when the app can\'t start).'
    severity: 1
    scope: api.id
    metricNamespace: 'Microsoft.Web/sites'
    metricName: 'Http5xx'
    timeAggregation: 'Total'
    operator: 'GreaterThan'
    threshold: 5
    dimensions: []
  }
  {
    name: 'api-slow'
    description: 'API requests averaged over 2 seconds.'
    severity: 3
    scope: appInsights.id
    metricNamespace: 'microsoft.insights/components'
    metricName: 'requests/duration'
    timeAggregation: 'Average'
    operator: 'GreaterThan'
    threshold: 2000
    dimensions: []
  }
  {
    name: 'browser-exceptions'
    description: 'The React app reported unhandled exceptions or failed sign-ins.'
    severity: 2
    scope: appInsights.id
    metricNamespace: 'microsoft.insights/components'
    metricName: 'exceptions/browser'
    timeAggregation: 'Count'
    operator: 'GreaterThan'
    threshold: 5
    dimensions: []
  }
  {
    name: 'cosmos-throttled'
    description: 'Cosmos DB throttled requests (429).'
    severity: 2
    scope: cosmos.id
    metricNamespace: 'Microsoft.DocumentDB/databaseAccounts'
    metricName: 'TotalRequests'
    timeAggregation: 'Count'
    operator: 'GreaterThan'
    threshold: 0
    dimensions: [{ name: 'StatusCode', operator: 'Include', values: ['429'] }]
  }
]

resource alerts 'Microsoft.Insights/metricAlerts@2018-03-01' = [
  for alert in metricAlerts: if (alertsEnabled) {
    name: 'alert-filmlib-${alert.name}-${environmentName}'
    location: 'global'
    tags: tags
    properties: {
      description: alert.description
      severity: alert.severity
      enabled: true
      scopes: [alert.scope]
      evaluationFrequency: 'PT5M'
      windowSize: 'PT15M'
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
        allOf: [
          {
            criterionType: 'StaticThresholdCriterion'
            name: alert.name
            metricNamespace: alert.metricNamespace
            metricName: alert.metricName
            timeAggregation: alert.timeAggregation
            operator: alert.operator
            threshold: alert.threshold
            dimensions: alert.dimensions
          }
        ]
      }
      actions: [{ actionGroupId: alertEmailGroup.id }]
    }
  }
]

resource availabilityAlerts 'Microsoft.Insights/metricAlerts@2018-03-01' = [
  for (target, i) in availabilityTargets: if (alertsEnabled && enableAvailabilityTests) {
    name: 'alert-filmlib-${target}-down-${environmentName}'
    location: 'global'
    tags: union(tags, { 'hidden-link:${availabilityTests[i].id}': 'Resource', 'hidden-link:${appInsights.id}': 'Resource' })
    properties: {
      description: 'Film Library ${target} failed its availability test from 2 or more regions.'
      severity: 1
      enabled: true
      scopes: [availabilityTests[i].id, appInsights.id]
      evaluationFrequency: 'PT5M'
      windowSize: 'PT15M'
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
        webTestId: availabilityTests[i].id
        componentId: appInsights.id
        failedLocationCount: 2
      }
      actions: [{ actionGroupId: alertEmailGroup.id }]
    }
  }
]

output apiName string = api.name
output apiUrl string = 'https://${api.properties.defaultHostName}'
output staticWebAppName string = staticWebApp.name
output webUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output cosmosEndpoint string = cosmos.properties.documentEndpoint
output keyVaultName string = keyVault.name
// Ingestion-only, and the browser needs it in config.json, so it isn't treated as a secret.
output appInsightsConnectionString string = appInsights.properties.ConnectionString
