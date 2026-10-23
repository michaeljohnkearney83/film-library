import { User } from 'oidc-client-ts'
import { describe, expect, it, vi } from 'vitest'
import { accessTokenProvider, completeSignIn, displayName, type SignInClient } from './oidc'

function user(accessToken: string, expiresInSeconds: number, profile: Record<string, unknown> = {}) {
  return new User({
    access_token: accessToken,
    token_type: 'Bearer',
    profile: { sub: 'user-1', iss: 'https://issuer', aud: 'web', exp: 0, iat: 0, ...profile },
    expires_at: Math.floor(Date.now() / 1000) + expiresInSeconds,
  })
}

function fakeClient(overrides: Partial<SignInClient> = {}): SignInClient {
  return {
    getUser: vi.fn(async () => null),
    signinRedirect: vi.fn(async () => {}),
    signinRedirectCallback: vi.fn(async () => user('from-callback', 300)),
    signinSilent: vi.fn(async () => user('renewed', 300)),
    signoutRedirect: vi.fn(async () => {}),
    ...overrides,
  } as SignInClient
}

function fakeLocation(url: string) {
  const parsed = new URL(url)
  return { href: parsed.href, search: parsed.search, pathname: parsed.pathname } as Location
}

function fakeHistory() {
  return { state: null, replaceState: vi.fn() } as unknown as History
}

describe('completeSignIn', () => {
  it('returns the stored user when the URL is not a sign-in redirect', async () => {
    const stored = user('stored', 300)
    const client = fakeClient({ getUser: vi.fn(async () => stored) })
    const history = fakeHistory()

    const result = await completeSignIn(client, fakeLocation('http://localhost:5173/'), history)

    expect(result).toEqual({ user: stored })
    expect(client.signinRedirectCallback).not.toHaveBeenCalled()
    expect(history.replaceState).not.toHaveBeenCalled()
  })

  it('finishes a sign-in redirect and removes the code from the address bar', async () => {
    const client = fakeClient()
    const history = fakeHistory()

    const result = await completeSignIn(client, fakeLocation('http://localhost:5173/?code=abc&state=xyz'), history)

    expect(result.user?.access_token).toBe('from-callback')
    expect(client.signinRedirectCallback).toHaveBeenCalledWith('http://localhost:5173/?code=abc&state=xyz')
    expect(history.replaceState).toHaveBeenCalledWith(null, '', '/')
  })

  it('turns a failed redirect (e.g. the user cancelled) into a message instead of crashing', async () => {
    const client = fakeClient({ signinRedirectCallback: vi.fn(async () => Promise.reject(new Error('access_denied'))) })
    const history = fakeHistory()

    const result = await completeSignIn(client, fakeLocation('http://localhost:5173/?error=access_denied&state=xyz'), history)

    expect(result).toEqual({ user: null, error: 'access_denied' })
    expect(history.replaceState).toHaveBeenCalledWith(null, '', '/')
  })
})

describe('accessTokenProvider', () => {
  it('fails when nobody is signed in', async () => {
    await expect(accessTokenProvider(fakeClient())()).rejects.toThrow('Not signed in.')
  })

  it('returns the current token while it has time left', async () => {
    const client = fakeClient({ getUser: vi.fn(async () => user('current', 300)) })

    await expect(accessTokenProvider(client)()).resolves.toBe('current')
    expect(client.signinSilent).not.toHaveBeenCalled()
  })

  it('renews a token that is about to expire', async () => {
    const client = fakeClient({ getUser: vi.fn(async () => user('old', 10)) })

    await expect(accessTokenProvider(client)()).resolves.toBe('renewed')
  })

  it('shares one renewal between calls made at the same time', async () => {
    const client = fakeClient({ getUser: vi.fn(async () => user('old', 10)) })
    const getToken = accessTokenProvider(client)

    const tokens = await Promise.all([getToken(), getToken()])

    expect(tokens).toEqual(['renewed', 'renewed'])
    expect(client.signinSilent).toHaveBeenCalledTimes(1)
  })

  it('signs in again when renewal fails', async () => {
    const client = fakeClient({
      getUser: vi.fn(async () => user('old', -60)),
      signinSilent: vi.fn(async () => Promise.reject(new Error('refresh token expired'))),
    })

    await expect(accessTokenProvider(client)()).rejects.toThrow('Your session has ended')
    expect(client.signinRedirect).toHaveBeenCalledOnce()
  })
})

describe('displayName', () => {
  it('prefers the name, then the username, then the email', () => {
    expect(displayName(user('t', 300, { name: 'Chloe', preferred_username: 'chloe1', email: 'c@example.com' }))).toBe('Chloe')
    expect(displayName(user('t', 300, { preferred_username: 'chloe1', email: 'c@example.com' }))).toBe('chloe1')
    expect(displayName(user('t', 300, { email: 'c@example.com' }))).toBe('c@example.com')
    expect(displayName(user('t', 300))).toBe('')
  })
})
