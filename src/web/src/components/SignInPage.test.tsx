import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SignInPage } from './SignInPage'

const signInButton = () => screen.getByRole('button', { name: 'Sign in' })

describe('SignInPage', () => {
  it('starts sign-in when the button is clicked', async () => {
    const onSignIn = vi.fn(async () => {})
    render(<SignInPage onSignIn={onSignIn} />)

    await userEvent.click(signInButton())

    expect(onSignIn).toHaveBeenCalledOnce()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows a message instead of doing nothing when sign-in cannot start', async () => {
    const onSignIn = vi.fn(async () => {
      throw new Error('endpoints_resolution_error')
    })
    render(<SignInPage onSignIn={onSignIn} />)

    await userEvent.click(signInButton())

    expect(await screen.findByRole('alert')).toHaveTextContent("Sign-in couldn't start: endpoints_resolution_error")
  })

  it('shows why the last sign-in redirect failed', () => {
    render(<SignInPage onSignIn={vi.fn(async () => {})} initialError="access_denied" />)

    expect(screen.getByRole('alert')).toHaveTextContent("Sign-in didn't finish: access_denied")
  })

  it('clears the message when the viewer tries again', async () => {
    const onSignIn = vi
      .fn<() => Promise<void>>()
      .mockRejectedValueOnce(new Error('network down'))
      .mockResolvedValueOnce(undefined)
    render(<SignInPage onSignIn={onSignIn} />)

    await userEvent.click(signInButton())
    await screen.findByRole('alert')
    await userEvent.click(signInButton())

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
