using '../main.bicep'

param environmentName = 'prod'
param appServiceSku = 'P0v3'

// From the pipeline's variable group (or set them in your shell before deploying by hand).
param entraAuthority = readEnvironmentVariable('ENTRA_AUTHORITY')
param entraApiClientId = readEnvironmentVariable('ENTRA_API_CLIENT_ID')
