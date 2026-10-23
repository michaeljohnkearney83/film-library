import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { FilmsApi } from './api/client'
import App from './App'

const api: FilmsApi = { listFilms: vi.fn(async () => []), getFilm: vi.fn() }

describe('App', () => {
  it('asks a signed-out viewer to sign in', () => {
    render(<App api={api} user={null} onSignIn={vi.fn(async () => {})} onSignOut={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(api.listFilms).not.toHaveBeenCalled()
  })

  it('shows the films to a signed-in viewer, greeting them by name', async () => {
    render(<App api={api} user={{ name: 'Chloe' }} onSignIn={vi.fn(async () => {})} onSignOut={vi.fn()} />)

    expect(await screen.findByText(/Chloe/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Sign in' })).not.toBeInTheDocument()
  })

  it('passes a failed sign-in redirect on to the sign-in page', () => {
    render(<App api={api} user={null} signInError="access_denied" onSignIn={vi.fn(async () => {})} onSignOut={vi.fn()} />)

    expect(screen.getByRole('alert')).toHaveTextContent('access_denied')
  })
})
