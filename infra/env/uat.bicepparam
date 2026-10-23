using '../main.bicep'

param environmentName = 'uat'
param location = 'ukwest' // the free-trial subscription has no App Service quota in UK South
param cosmosLocation = 'uksouth' // UK West turned down new Cosmos DB accounts (high demand)
param appServiceSku = 'B1'

// From the pipeline's variable group (or set them in your shell before deploying by hand).
param entraAuthority = readEnvironmentVariable('ENTRA_AUTHORITY')
param entraApiClientId = readEnvironmentVariable('ENTRA_API_CLIENT_ID')

// Observability: alerts are created only when ALERT_EMAIL is set (variable group, or your shell).
param logsDailyCapGb = '0.5'
param alertEmail = readEnvironmentVariable('ALERT_EMAIL', '')
