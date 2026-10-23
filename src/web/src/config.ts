/** Runtime settings, read from /config.json so one build can be deployed to every environment. */
export interface AppConfig {
  /** Base URL of the Film Library API, e.g. https://app-filmlib-dev.azurewebsites.net */
  apiBaseUrl: string
  /** OpenID Connect sign-in: Entra External ID in Azure, or the local Keycloak from docker-compose. */
  auth: {
    /**
     * The provider's authority (its metadata is at <authority>/.well-known/openid-configuration), e.g.
     * Entra: https://<tenant-subdomain>.ciamlogin.com/<tenant-id>/v2.0 · Keycloak: http://localhost:8180/realms/mediahub
     */
    authority: string
    /** The React app's client id at the provider. */
    clientId: string
    /** Scopes to request besides openid and profile: the API scope, plus offline_access on Entra for refresh tokens. */
    scope: string
  }
  /** Optional. Application Insights for the browser; written by the pipeline in Azure, usually left out locally. */
  telemetry?: {
    /** Application Insights connection string. It only allows sending telemetry, so it's safe in the browser. */
    connectionString: string
  }
}

export async function loadConfig(fetchImpl: typeof fetch = fetch): Promise<AppConfig> {
  const response = await fetchImpl('/config.json', { cache: 'no-store' })
  if (!response.ok)
    throw new Error(`Couldn't load /config.json (${response.status}). Copy public/config.example.json to public/config.json.`)

  const config = (await response.json()) as Partial<AppConfig>
  const { authority, clientId, scope } = config.auth ?? {}
  if (!config.apiBaseUrl || !authority || !clientId || !scope)
    throw new Error('/config.json needs apiBaseUrl and auth.authority, auth.clientId and auth.scope. See public/config.example.json.')
  return config as AppConfig
}
