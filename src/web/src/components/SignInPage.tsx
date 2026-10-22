export function SignInPage({ onSignIn }: { onSignIn: () => void }) {
  return (
    <main className="signin">
      <h1>Film Library</h1>
      <p className="muted">Sign in with your account to browse films. Films are shown according to your age and their UK rating.</p>
      <button type="button" className="primary" onClick={onSignIn}>
        Sign in
      </button>
    </main>
  )
}
