import { UserManager, WebStorageStateStore, type User } from 'oidc-client-ts'
import type { AppConfig } from '../config'

/** The parts of oidc-client-ts's UserManager the app uses, so tests can pass a fake. */
export type SignInClient = Pick<UserManager, 'getUser' | 'signinRedirect' | 'signinRedirectCallback' | 'signinSilent' | 'signoutRedirect'>

/**
 * OpenID Connect sign-in (authorization code + PKCE, by redirect) for any provider: Entra External ID in Azure,
 * or the local Keycloak from docker-compose. Tokens live in sessionStorage, like the previous MSAL setup.
 */
export function createSignInClient(auth: AppConfig['auth'], origin = window.location.origin): UserManager {
  return new UserManager({
    authority: auth.authority,
    client_id: auth.clientId,
    redirect_uri: origin,
    post_logout_redirect_uri: origin,
    response_type: 'code',
    scope: `openid profile ${auth.scope}`,
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    automaticSilentRenew: false,
  })
}

export interface SignInState {
  user: User | null
  /** Why the sign-in redirect failed (e.g. the user cancelled), to show on the sign-in page. */
  error?: string
}

/** Finishes a sign-in redirect if the URL carries one, then returns whoever is signed in. */
export async function completeSignIn(client: SignInClient, location: Location = window.location, history: History = window.history): Promise<SignInState> {
  const params = new URLSearchParams(location.search)
  if (!params.has('state') || !(params.has('code') || params.has('error'))) return { user: await client.getUser() }

  try {
    return { user: await client.signinRedirectCallback(location.href) }
  } catch (error) {
    return { user: null, error: error instanceof Error ? error.message : String(error) }
  } finally {
    // The code is single-use; don't leave it in the address bar or history.
    history.replaceState(history.state, '', location.pathname)
  }
}

/** Refresh a little before expiry so a token doesn't run out on the way to the API. */
const EXPIRY_MARGIN_SECONDS = 30

/**
 * Returns an access token for the API, renewing it with the refresh token when it's about to expire.
 * Calls made while a renewal is in flight share it. If renewal fails (e.g. the session ended), redirects to sign in.
 */
export function accessTokenProvider(client: SignInClient): () => Promise<string> {
  let renewal: Promise<string> | undefined

  async function renew(): Promise<string> {
    try {
      const user = await client.signinSilent()
      if (user) return user.access_token
    } catch {
      // Fall through to an interactive sign-in.
    }
    await client.signinRedirect()
    throw new Error('Your session has ended. Signing you in again…')
  }

  return async () => {
    const user = await client.getUser()
    if (!user) throw new Error('Not signed in.')
    if ((user.expires_in ?? Infinity) > EXPIRY_MARGIN_SECONDS) return user.access_token

    renewal ??= renew().finally(() => (renewal = undefined))
    return renewal
  }
}

/** The name to greet the user by: the token's name, falling back to their username or email. */
export function displayName(user: User): string {
  const { name, preferred_username, email } = user.profile
  return name ?? preferred_username ?? email ?? ''
}
