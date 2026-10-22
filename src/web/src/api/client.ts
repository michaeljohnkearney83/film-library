import createClient, { type Middleware } from 'openapi-fetch'
import type { components, paths } from './schema'

// Types come from schema.d.ts, generated from api-spec/main.tsp. Never edit them by hand.
export type FilmSummary = components['schemas']['FilmSummary']
export type FilmDetails = components['schemas']['FilmDetails']
export type Rating = components['schemas']['Rating']
type ProblemDetails = components['schemas']['ProblemDetails']

export type FilmResult =
  | { kind: 'ok'; film: FilmDetails }
  | { kind: 'restricted'; message: string }
  | { kind: 'notFound' }

/** Any failure talking to the API: unreachable, unexpected status, or an unreadable reply. */
export class ApiError extends Error {
  override name = 'ApiError'
}

export interface FilmsApi {
  listFilms(signal?: AbortSignal): Promise<FilmSummary[]>
  getFilm(id: string, signal?: AbortSignal): Promise<FilmResult>
}

const RESTRICTED_FALLBACK = "You're not old enough to view this film."

export function createFilmsApi(baseUrl: string, getAccessToken: () => Promise<string>, fetchImpl?: typeof fetch): FilmsApi {
  const client = createClient<paths>({ baseUrl, fetch: fetchImpl })

  const bearerToken: Middleware = {
    async onRequest({ request }) {
      request.headers.set('Authorization', `Bearer ${await getAccessToken()}`)
      return request
    },
  }
  client.use(bearerToken)

  return {
    listFilms: (signal) =>
      call(async () => {
        const { data, response } = await client.GET('/api/films', { signal })
        if (!response.ok || !data) throw new ApiError(`Couldn't load films (${response.status}).`)
        return data
      }),

    getFilm: (id, signal) =>
      call(async () => {
        const { data, error, response } = await client.GET('/api/films/{id}', { params: { path: { id } }, signal })
        if (response.status === 403) return { kind: 'restricted', message: problemDetail(error) ?? RESTRICTED_FALLBACK }
        if (response.status === 404) return { kind: 'notFound' }
        if (!response.ok || !data) throw new ApiError(`Couldn't load this film (${response.status}).`)
        return { kind: 'ok', film: data }
      }),
  }
}

export function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

/** Turns network failures and unreadable replies into ApiError; lets aborts through untouched. */
async function call<T>(work: () => Promise<T>): Promise<T> {
  try {
    return await work()
  } catch (error) {
    if (isAbort(error) || error instanceof ApiError) throw error
    if (error instanceof SyntaxError) throw new ApiError("The films API sent a reply the app couldn't read.")
    throw new ApiError(`Couldn't reach the films API: ${error instanceof Error ? error.message : String(error)}`)
  }
}

function problemDetail(error: unknown): string | undefined {
  return typeof error === 'object' && error !== null ? (error as ProblemDetails).detail : undefined
}
