import type { IConfiguration, ITelemetryItem } from '@microsoft/applicationinsights-web'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ROLE_NAME, noTelemetry, startTelemetry } from './telemetry'

const CONNECTION_STRING = 'InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://uksouth-1.in.applicationinsights.azure.com/'
const API = 'https://app-filmlib-api-dev.azurewebsites.net'

/** Stands in for the SDK module, recording what the app asked of it. */
function fakeSdk() {
  const sdk = {
    config: undefined as IConfiguration | undefined,
    initializers: [] as ((item: ITelemetryItem) => void)[],
    pageViews: 0,
    user: undefined as string | undefined,
    exceptions: [] as { exception: Error; properties?: Record<string, string> }[],
  }
  class ApplicationInsights {
    constructor({ config }: { config: IConfiguration }) {
      sdk.config = config
    }
    loadAppInsights() {}
    addTelemetryInitializer(initializer: (item: ITelemetryItem) => void) {
      sdk.initializers.push(initializer)
    }
    trackPageView() {
      sdk.pageViews++
    }
    setAuthenticatedUserContext(id: string) {
      sdk.user = id
    }
    trackException(exception: { exception: Error; properties?: Record<string, string> }) {
      sdk.exceptions.push(exception)
    }
  }
  const load = async () => ({ ApplicationInsights, DistributedTracingModes: { W3C: 2 } }) as never
  return { sdk, load }
}

afterEach(() => vi.restoreAllMocks())

describe('startTelemetry', () => {
  it('does nothing, and never loads the SDK, without a connection string', async () => {
    const load = vi.fn()

    expect(await startTelemetry(undefined, API, load)).toBe(noTelemetry)
    expect(await startTelemetry({ connectionString: '' }, API, load)).toBe(noTelemetry)
    expect(load).not.toHaveBeenCalled()
  })

  it('sends W3C trace headers to the API only, without cookies, and records the first page view', async () => {
    const { sdk, load } = fakeSdk()

    await startTelemetry({ connectionString: CONNECTION_STRING }, API, load)

    expect(sdk.config).toMatchObject({
      connectionString: CONNECTION_STRING,
      distributedTracingMode: 2,
      enableCorsCorrelation: true,
      correlationHeaderDomains: ['app-filmlib-api-dev.azurewebsites.net'],
      disableCookiesUsage: true,
    })
    expect(sdk.pageViews).toBe(1)
  })

  it('labels everything it sends with the web app role, keeping other tags', async () => {
    const { sdk, load } = fakeSdk()
    await startTelemetry({ connectionString: CONNECTION_STRING }, API, load)
    const item: ITelemetryItem = { name: 'PageView', tags: { 'ai.user.id': 'u1' } }

    sdk.initializers.forEach((initializer) => initializer(item))

    expect(item.tags).toEqual({ 'ai.user.id': 'u1', 'ai.cloud.role': ROLE_NAME })
  })

  it('reports exceptions, wrapping anything that is not an Error', async () => {
    const { sdk, load } = fakeSdk()
    const telemetry = await startTelemetry({ connectionString: CONNECTION_STRING }, API, load)

    telemetry.trackException(new TypeError('boom'), { stage: 'startup' })
    telemetry.trackException('plain text')

    expect(sdk.exceptions[0]).toEqual({ exception: new TypeError('boom'), properties: { stage: 'startup' } })
    expect(sdk.exceptions[1].exception).toEqual(new Error('plain text'))
  })

  it('sets the authenticated user id', async () => {
    const { sdk, load } = fakeSdk()
    const telemetry = await startTelemetry({ connectionString: CONNECTION_STRING }, API, load)

    telemetry.setUser('5b0c9d0e-user-sub')

    expect(sdk.user).toBe('5b0c9d0e-user-sub')
  })

  it("keeps the app running when the SDK can't load", async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => {})

    const telemetry = await startTelemetry({ connectionString: CONNECTION_STRING }, API, async () => {
      throw new TypeError('Failed to fetch dynamically imported module')
    })

    expect(telemetry).toBe(noTelemetry)
  })
})
