import {
  BrowserCacheLocation,
  InteractionRequiredAuthError,
  PublicClientApplication,
} from '@azure/msal-browser'
import type { AppConfig } from '../config'

/** Creates the MSAL client for Microsoft Entra External ID and completes any sign-in redirect. */
export async function createMsal(entra: AppConfig['entra']): Promise<PublicClientApplication> {
  const msal = new PublicClientApplication({
    auth: {
      clientId: entra.clientId,
      authority: entra.authority,
      knownAuthorities: [new URL(entra.authority).host],
      redirectUri: window.location.origin,
      postLogoutRedirectUri: window.location.origin,
    },
    cache: { cacheLocation: BrowserCacheLocation.SessionStorage },
  })

  await msal.initialize()
  const result = await msal.handleRedirectPromise()
  const account = result?.account ?? msal.getAllAccounts()[0]
  if (account) msal.setActiveAccount(account)
  return msal
}

/**
 * Returns an access token for the API. MSAL renews expired tokens silently; when it can't
 * (e.g. the session ended), it redirects to sign in again.
 */
export function accessTokenProvider(msal: PublicClientApplication, scope: string): () => Promise<string> {
  return async () => {
    const account = msal.getActiveAccount()
    if (!account) throw new Error('Not signed in.')

    try {
      return (await msal.acquireTokenSilent({ scopes: [scope], account })).accessToken
    } catch (error) {
      if (error instanceof InteractionRequiredAuthError) await msal.acquireTokenRedirect({ scopes: [scope], account })
      throw error
    }
  }
}
