using '../main.bicep'

param environmentName = 'prod'
param appServiceSku = 'P0v3'

// From the pipeline's variable group (or set them in your shell before deploying by hand).
param entraAuthority = readEnvironmentVariable('ENTRA_AUTHORITY')
param entraApiClientId = readEnvironmentVariable('ENTRA_API_CLIENT_ID')

// Observability: alerts are created only when ALERT_EMAIL is set (variable group, or your shell).
param logsDailyCapGb = '-1' // no cap: losing production telemetry costs more than the ingestion
param alertEmail = readEnvironmentVariable('ALERT_EMAIL', '')
param enableAvailabilityTests = true // charged per run: 2 targets x 3 regions every 15 minutes
