import { describe, expect, it, vi } from 'vitest'
import { ApiError, createFilmsApi } from './client'

const BASE = 'http://api.test'

function json(status: number, body: unknown, contentType = 'application/json') {
  return new Response(typeof body === 'string' ? body : JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

function apiWith(respond: (request: Request) => Response | Promise<Response>) {
  const requests: Request[] = []
  const fetchImpl = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = input instanceof Request ? input : new Request(input, init)
    requests.push(request)
    return respond(request)
  })
  return { api: createFilmsApi(BASE, async () => 'token-123', fetchImpl as typeof fetch), requests }
}

const toyStory = { id: 'toy-story', title: 'Toy Story', year: 1995, genre: 'Animation', rating: 'U', canView: true }

describe('listFilms', () => {
  it('calls the contract route with the bearer token', async () => {
    const { api, requests } = apiWith(() => json(200, [toyStory]))

    const films = await api.listFilms()

    expect(films).toEqual([toyStory])
    expect(new URL(requests[0].url).pathname).toBe('/api/films')
    expect(requests[0].headers.get('Authorization')).toBe('Bearer token-123')
  })

  it('reports an error status as ApiError', async () => {
    const { api } = apiWith(() => json(500, { title: 'boom' }))

    await expect(api.listFilms()).rejects.toThrow(new ApiError("Couldn't load films (500)."))
  })

  it('reports an unreadable reply as ApiError', async () => {
    const { api } = apiWith(() => json(200, '<html>Bad gateway</html>', 'application/json'))

    await expect(api.listFilms()).rejects.toThrow("couldn't read")
  })

  it('reports a network failure as ApiError', async () => {
    const { api } = apiWith(() => Promise.reject(new TypeError('Failed to fetch')))

    await expect(api.listFilms()).rejects.toThrow("Couldn't reach the films API: Failed to fetch")
  })

  it('lets a cancelled request through as an abort, not an error', async () => {
    const { api } = apiWith(() => Promise.reject(new DOMException('Aborted', 'AbortError')))

    await expect(api.listFilms()).rejects.toMatchObject({ name: 'AbortError' })
  })
})

describe('getFilm', () => {
  it('returns the film', async () => {
    const film = { ...toyStory, director: 'John Lasseter', synopsis: 'Toys.' }
    const { api, requests } = apiWith(() => json(200, film))

    expect(await api.getFilm('toy-story')).toEqual({ kind: 'ok', film })
    expect(new URL(requests[0].url).pathname).toBe('/api/films/toy-story')
  })

  it("turns a 403 into 'restricted' with the server's reason", async () => {
    const { api } = apiWith(() =>
      json(403, { status: 403, title: 'Age restricted', detail: 'You must be 18 or over to view this film.' }, 'application/problem+json'),
    )

    expect(await api.getFilm('pulp-fiction')).toEqual({ kind: 'restricted', message: 'You must be 18 or over to view this film.' })
  })

  it('still treats a 403 without problem details as restricted', async () => {
    const { api } = apiWith(() => json(403, 'Forbidden', 'text/plain'))

    expect(await api.getFilm('pulp-fiction')).toEqual({ kind: 'restricted', message: "You're not old enough to view this film." })
  })

  it("turns a 404 into 'notFound'", async () => {
    const { api } = apiWith(() => new Response(null, { status: 404 }))

    expect(await api.getFilm('nope')).toEqual({ kind: 'notFound' })
  })
})
