import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from '@azure/msal-react'
import type { FilmsApi } from './api/client'
import { FilmsPage } from './components/FilmsPage'
import { SignInPage } from './components/SignInPage'

export default function App({ api, apiScope }: { api: FilmsApi; apiScope: string }) {
  const { instance, accounts } = useMsal()
  const account = instance.getActiveAccount() ?? accounts[0]

  return (
    <>
      <AuthenticatedTemplate>
        <FilmsPage api={api} userName={account?.name ?? account?.username ?? ''} onSignOut={() => instance.logoutRedirect()} />
      </AuthenticatedTemplate>
      <UnauthenticatedTemplate>
        <SignInPage onSignIn={() => instance.loginRedirect({ scopes: [apiScope] })} />
      </UnauthenticatedTemplate>
    </>
  )
}
