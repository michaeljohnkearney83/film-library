/** Runtime settings, read from /config.json so one build can be deployed to every environment. */
export interface AppConfig {
  /** Base URL of the Film Library API, e.g. https://app-filmlib-dev.azurewebsites.net */
  apiBaseUrl: string
  entra: {
    /** The React app's (SPA) app registration client id. */
    clientId: string
    /** External ID tenant authority by tenant id: https://<tenant-id>.ciamlogin.com/<tenant-id> */
    authority: string
    /** The API's delegated scope, e.g. api://<api-client-id>/Films.Read */
    apiScope: string
  }
}

export async function loadConfig(fetchImpl: typeof fetch = fetch): Promise<AppConfig> {
  const response = await fetchImpl('/config.json', { cache: 'no-store' })
  if (!response.ok)
    throw new Error(`Couldn't load /config.json (${response.status}). Copy public/config.example.json to public/config.json.`)
  return (await response.json()) as AppConfig
}
