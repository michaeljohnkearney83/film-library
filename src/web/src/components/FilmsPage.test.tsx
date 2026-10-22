import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError, type FilmResult, type FilmsApi, type FilmSummary } from '../api/client'
import { FilmsPage } from './FilmsPage'

const films: FilmSummary[] = [
  { id: 'toy-story', title: 'Toy Story', year: 1995, genre: 'Animation', rating: 'U', canView: true },
  { id: 'inception', title: 'Inception', year: 2010, genre: 'Science Fiction', rating: '12', canView: true },
  { id: 'pulp-fiction', title: 'Pulp Fiction', year: 1994, genre: 'Crime', rating: '18', canView: false },
]

function ok(film: FilmSummary): FilmResult {
  return { kind: 'ok', film: { ...film, director: `Director of ${film.title}`, synopsis: `Synopsis of ${film.title}` } }
}

function fakeApi(overrides: Partial<FilmsApi> = {}): FilmsApi {
  return {
    listFilms: vi.fn(async () => films),
    getFilm: vi.fn(async (id: string): Promise<FilmResult> =>
      id === 'pulp-fiction' ? { kind: 'restricted', message: 'You must be 18 or over to view this film.' } : ok(films.find((f) => f.id === id)!),
    ),
    ...overrides,
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((r) => (resolve = r))
  return { promise, resolve }
}

const filmButton = (title: string) => screen.getByRole('button', { name: new RegExp(title) })

describe('FilmsPage', () => {
  it('lists every film and locks the ones the viewer is too young for', async () => {
    render(<FilmsPage api={fakeApi()} userName="Chloe" onSignOut={() => {}} />)

    await screen.findByText('Pulp Fiction')
    expect(within(filmButton('Pulp Fiction')).getByRole('img', { name: 'Age restricted' })).toBeInTheDocument()
    expect(within(filmButton('Toy Story')).queryByRole('img', { name: 'Age restricted' })).not.toBeInTheDocument()
    expect(screen.getByText('Chloe')).toBeInTheDocument()
  })

  it("shows the first film's details", async () => {
    render(<FilmsPage api={fakeApi()} userName="Chloe" onSignOut={() => {}} />)

    expect(await screen.findByText('Synopsis of Toy Story')).toBeInTheDocument()
    expect(filmButton('Toy Story')).toHaveAttribute('aria-pressed', 'true')
  })

  it('shows why a restricted film is hidden instead of its content', async () => {
    render(<FilmsPage api={fakeApi()} userName="Chloe" onSignOut={() => {}} />)
    await screen.findByText('Synopsis of Toy Story')

    await userEvent.click(filmButton('Pulp Fiction'))

    expect(await screen.findByText('You must be 18 or over to view this film.')).toBeInTheDocument()
    expect(screen.queryByText(/Synopsis of/)).not.toBeInTheDocument()
  })

  it("ignores a late reply for a film the viewer already moved away from", async () => {
    const inception = deferred<FilmResult>()
    const api = fakeApi({
      getFilm: vi.fn((id: string) =>
        id === 'inception'
          ? inception.promise
          : Promise.resolve<FilmResult>(id === 'pulp-fiction' ? { kind: 'restricted', message: 'Too young.' } : ok(films[0])),
      ),
    })
    render(<FilmsPage api={api} userName="Chloe" onSignOut={() => {}} />)
    await screen.findByText('Synopsis of Toy Story')

    await userEvent.click(filmButton('Inception')) // reply still pending
    await userEvent.click(filmButton('Pulp Fiction'))
    await screen.findByText('Too young.')
    inception.resolve(ok(films[1])) // Inception's reply arrives late

    await new Promise((r) => setTimeout(r, 0))
    expect(screen.queryByText('Synopsis of Inception')).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('Pulp Fiction')
    expect(screen.getByText('Too young.')).toBeInTheDocument() // the late reply didn't replace the current message
  })

  it('shows a failed load and recovers on retry', async () => {
    const listFilms = vi
      .fn<FilmsApi['listFilms']>()
      .mockRejectedValueOnce(new ApiError("Couldn't reach the films API: Failed to fetch"))
      .mockResolvedValueOnce(films)
    render(<FilmsPage api={fakeApi({ listFilms })} userName="Chloe" onSignOut={() => {}} />)

    expect(await screen.findByText("Couldn't reach the films API: Failed to fetch")).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))

    expect(await screen.findByText('Synopsis of Toy Story')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows an error when a film fails to load', async () => {
    const api = fakeApi({ getFilm: vi.fn(async () => Promise.reject(new ApiError("Couldn't load this film (500)."))) })
    render(<FilmsPage api={api} userName="Chloe" onSignOut={() => {}} />)

    expect(await screen.findByText("Couldn't load this film (500).")).toBeInTheDocument()
  })

  it('signs out', async () => {
    const onSignOut = vi.fn()
    render(<FilmsPage api={fakeApi()} userName="Chloe" onSignOut={onSignOut} />)

    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(onSignOut).toHaveBeenCalledOnce()
  })
})
