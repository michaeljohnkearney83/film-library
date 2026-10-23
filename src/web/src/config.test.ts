import { describe, expect, it } from 'vitest'
import { loadConfig } from './config'

const valid = {
  apiBaseUrl: 'http://localhost:5080',
  auth: { authority: 'http://localhost:8180/realms/mediahub', clientId: 'film-library-web', scope: 'Films.Read' },
}

const serve = (status: number, body: unknown) => async () => new Response(JSON.stringify(body), { status })

describe('loadConfig', () => {
  it('returns the settings from /config.json', async () => {
    await expect(loadConfig(serve(200, valid))).resolves.toEqual(valid)
  })

  it('passes optional telemetry settings through', async () => {
    const withTelemetry = { ...valid, telemetry: { connectionString: 'InstrumentationKey=abc' } }

    await expect(loadConfig(serve(200, withTelemetry))).resolves.toEqual(withTelemetry)
  })

  it('says how to fix a missing config.json', async () => {
    await expect(loadConfig(serve(404, {}))).rejects.toThrow('Copy public/config.example.json to public/config.json.')
  })

  it('rejects an old-style config with an "entra" block instead of "auth"', async () => {
    const old = { apiBaseUrl: valid.apiBaseUrl, entra: { clientId: 'x', authority: 'y', apiScope: 'z' } }

    await expect(loadConfig(serve(200, old))).rejects.toThrow('auth.authority, auth.clientId and auth.scope')
  })
})
