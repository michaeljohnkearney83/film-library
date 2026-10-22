using '../main.bicep'

param environmentName = 'dev'
param appServiceSku = 'F1' // free tier: no always-on, 60 CPU minutes a day, cold starts

// From the pipeline's variable group (or set them in your shell before deploying by hand).
param entraAuthority = readEnvironmentVariable('ENTRA_AUTHORITY')
param entraApiClientId = readEnvironmentVariable('ENTRA_API_CLIENT_ID')
