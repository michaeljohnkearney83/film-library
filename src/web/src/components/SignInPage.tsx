import { useState } from 'react'

/** Starts the sign-in redirect. If MSAL can't start it (e.g. bad config or no network), says so instead of doing nothing. */
export function SignInPage({ onSignIn }: { onSignIn: () => Promise<void> }) {
  const [error, setError] = useState<string>()

  async function signIn() {
    setError(undefined)
    try {
      await onSignIn()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  return (
    <main className="signin">
      <h1>Film Library</h1>
      <p className="muted">Sign in with your account to browse films. Films are shown according to your age and their UK rating.</p>
      <button type="button" className="primary" onClick={() => void signIn()}>
        Sign in
      </button>
      {error && (
        <p role="alert" className="notice error">
          Sign-in couldn't start: {error}
        </p>
      )}
    </main>
  )
}
