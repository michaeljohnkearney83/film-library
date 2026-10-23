import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createFilmsApi } from './api/client'
import App from './App'
import { accessTokenProvider, completeSignIn, createSignInClient, displayName } from './auth/oidc'
import { loadConfig } from './config'
import { noTelemetry, startTelemetry } from './telemetry'
import './index.css'

let telemetry = noTelemetry

const root = createRoot(document.getElementById('root')!, {
  onUncaughtError: (error, info) => {
    console.error(error)
    telemetry.trackException(error, { componentStack: info.componentStack ?? '' })
  },
})

async function start() {
  try {
    const config = await loadConfig()
    telemetry = await startTelemetry(config.telemetry, config.apiBaseUrl)
    const signIn = createSignInClient(config.auth)
    const { user, error } = await completeSignIn(signIn)
    if (user) telemetry.setUser(user.profile.sub)
    if (error) telemetry.trackException(new Error(error), { stage: 'sign-in callback' })
    const api = createFilmsApi(config.apiBaseUrl, accessTokenProvider(signIn))

    root.render(
      <StrictMode>
        <App
          api={api}
          user={user ? { name: displayName(user) } : null}
          signInError={error}
          onSignIn={() =>
            signIn.signinRedirect().catch((reason: unknown) => {
              telemetry.trackException(reason, { stage: 'sign-in start' })
              throw reason
            })
          }
          onSignOut={() => void signIn.signoutRedirect()}
        />
      </StrictMode>,
    )
  } catch (error) {
    telemetry.trackException(error, { stage: 'startup' })
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
