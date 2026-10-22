import { MsalProvider } from '@azure/msal-react'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createFilmsApi } from './api/client'
import App from './App'
import { accessTokenProvider, createMsal } from './auth/msal'
import { loadConfig } from './config'
import './index.css'

const root = createRoot(document.getElementById('root')!)

async function start() {
  try {
    const config = await loadConfig()
    const msal = await createMsal(config.entra)
    const api = createFilmsApi(config.apiBaseUrl, accessTokenProvider(msal, config.entra.apiScope))

    root.render(
      <StrictMode>
        <MsalProvider instance={msal}>
          <App api={api} apiScope={config.entra.apiScope} />
        </MsalProvider>
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
