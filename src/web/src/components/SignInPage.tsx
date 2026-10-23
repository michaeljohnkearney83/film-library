import { useState } from 'react'

/**
 * Starts the sign-in redirect. If it can't start (e.g. bad config or no network), or the last attempt failed
 * (initialError, e.g. the user cancelled), says so instead of doing nothing.
 */
export function SignInPage({ onSignIn, initialError }: { onSignIn: () => Promise<void>; initialError?: string }) {
  const [error, setError] = useState(initialError && `Sign-in didn't finish: ${initialError}`)

  async function signIn() {
    setError(undefined)
    try {
      await onSignIn()
    } catch (e) {
      setError(`Sign-in couldn't start: ${e instanceof Error ? e.message : String(e)}`)
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
          {error}
        </p>
      )}
    </main>
  )
}
