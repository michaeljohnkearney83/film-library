import type { FilmsApi } from './api/client'
import { FilmsPage } from './components/FilmsPage'
import { SignInPage } from './components/SignInPage'

interface Props {
  api: FilmsApi
  /** The signed-in user, or null when nobody is signed in. */
  user: { name: string } | null
  /** Why the last sign-in redirect failed, if it did. */
  signInError?: string
  onSignIn: () => Promise<void>
  onSignOut: () => void
}

export default function App({ api, user, signInError, onSignIn, onSignOut }: Props) {
  return user ? (
    <FilmsPage api={api} userName={user.name} onSignOut={onSignOut} />
  ) : (
    <SignInPage onSignIn={onSignIn} initialError={signInError} />
  )
}
