import type { ITelemetryItem } from '@microsoft/applicationinsights-web'
import type { AppConfig } from './config'

/** What the app reports to Application Insights. A no-op when config.json has no telemetry settings (e.g. locally). */
export interface Telemetry {
  /** Ties later telemetry to the signed-in user's opaque id (the token's `sub`), never their name or email. */
  setUser(id: string): void
  trackException(error: unknown, properties?: Record<string, string>): void
}

export const noTelemetry: Telemetry = {
  setUser: () => {},
  trackException: () => {},
}

/** Application Insights shows this as the cloud role on the application map, next to the API's FilmLibrary.Api. */
export const ROLE_NAME = 'FilmLibrary.Web'

type AppInsightsModule = Pick<typeof import('@microsoft/applicationinsights-web'), 'ApplicationInsights' | 'DistributedTracingModes'>

/**
 * Starts Application Insights when config.json has a connection string. Collects page views, browser exceptions and
 * calls to the API, which carry a W3C traceparent header so each call joins the API's own trace.
 * The SDK is loaded on demand, so builds without telemetry don't download it, and a failure to load it is ignored.
 */
export async function startTelemetry(
  settings: AppConfig['telemetry'],
  apiBaseUrl: string,
  load: () => Promise<AppInsightsModule> = () => import('@microsoft/applicationinsights-web'),
): Promise<Telemetry> {
  if (!settings?.connectionString) return noTelemetry

  try {
    const { ApplicationInsights, DistributedTracingModes } = await load()
    const appInsights = new ApplicationInsights({
      config: {
        connectionString: settings.connectionString,
        distributedTracingMode: DistributedTracingModes.W3C, // traceparent, as OpenTelemetry uses in the API
        enableCorsCorrelation: true,
        correlationHeaderDomains: [new URL(apiBaseUrl).host], // only the API gets trace headers, not the sign-in provider
        enableUnhandledPromiseRejectionTracking: true,
        disableCookiesUsage: true, // no analytics cookies, so no consent banner; users and sessions are counted per page load
      },
    })
    appInsights.loadAppInsights()
    appInsights.addTelemetryInitializer((item: ITelemetryItem) => {
      item.tags = { ...item.tags, 'ai.cloud.role': ROLE_NAME }
    })
    appInsights.trackPageView()

    return {
      setUser: (id) => appInsights.setAuthenticatedUserContext(id, undefined, false),
      trackException: (error, properties) =>
        appInsights.trackException({ exception: error instanceof Error ? error : new Error(String(error)), properties }),
    }
  } catch (error) {
    console.warn("Telemetry couldn't start:", error)
    return noTelemetry
  }
}
