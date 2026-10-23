import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createFilmsApi } from './api/client'
import App from './App'
import { accessTokenProvider, completeSignIn, createSignInClient, displayName } from './auth/oidc'
import { loadConfig } from './config'
import './index.css'

const root = createRoot(document.getElementById('root')!)

async function start() {
  try {
    const config = await loadConfig()
    const signIn = createSignInClient(config.auth)
    const { user, error } = await completeSignIn(signIn)
    const api = createFilmsApi(config.apiBaseUrl, accessTokenProvider(signIn))

    root.render(
      <StrictMode>
        <App
          api={api}
          user={user ? { name: displayName(user) } : null}
          signInError={error}
          onSignIn={() => signIn.signinRedirect()}
          onSignOut={() => void signIn.signoutRedirect()}
        />
      </StrictMode>,
    )
  } catch (error) {
    root.render(
      <main className="signin">
        <h1>Film Library</h1>
        <p role="alert" className="notice error">
          The app couldn't start: {error instanceof Error ? error.message : String(error)}
        </p>
      </main>,
    )
  }
}

void start()
