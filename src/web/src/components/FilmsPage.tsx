import { useEffect, useState } from 'react'
import { isAbort, type FilmDetails, type FilmsApi, type FilmSummary } from '../api/client'
import { LockIcon, RatingBadge } from './RatingBadge'

type ListState = { status: 'loading' } | { status: 'ok'; films: FilmSummary[] } | { status: 'error'; message: string }

type DetailResult = { status: 'ok'; film: FilmDetails } | { status: 'message'; message: string; restricted: boolean }

interface Props {
  api: FilmsApi
  userName: string
  onSignOut: () => void
}

/** Film list and detail. The API decides what the viewer may see; this page only displays its answer. */
export function FilmsPage({ api, userName, onSignOut }: Props) {
  const [list, setList] = useState<ListState>({ status: 'loading' })
  const [reloadCount, setReloadCount] = useState(0)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  // Keyed by film id: while the selected film's reply is outstanding, the pane shows "Loading…".
  const [detail, setDetail] = useState<{ id: string; result: DetailResult } | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    api
      .listFilms(controller.signal)
      .then((films) => {
        setList({ status: 'ok', films })
        setSelectedId((current) => current ?? films[0]?.id ?? null)
      })
      .catch((error: unknown) => {
        if (!isAbort(error)) setList({ status: 'error', message: messageOf(error) })
      })
    return () => controller.abort()
  }, [api, reloadCount])

  useEffect(() => {
    if (!selectedId) return

    const controller = new AbortController()
    api
      .getFilm(selectedId, controller.signal)
      .then((result) => {
        // A reply for a film the viewer has already moved away from must not overwrite the current one.
        if (controller.signal.aborted) return
        if (result.kind === 'ok') setDetail({ id: selectedId, result: { status: 'ok', film: result.film } })
        else if (result.kind === 'restricted')
          setDetail({ id: selectedId, result: { status: 'message', message: result.message, restricted: true } })
        else setDetail({ id: selectedId, result: { status: 'message', message: 'This film no longer exists.', restricted: false } })
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted || isAbort(error)) return
        setDetail({ id: selectedId, result: { status: 'message', message: messageOf(error), restricted: false } })
      })
    return () => controller.abort()
  }, [api, selectedId])

  const films = list.status === 'ok' ? list.films : []
  const selected = films.find((f) => f.id === selectedId)
  const current = detail && detail.id === selectedId ? detail.result : null

  function retry() {
    setList({ status: 'loading' })
    setReloadCount((n) => n + 1)
  }

  return (
    <div className="app">
      <header className="topbar">
        <h1>Films</h1>
        <div className="user">
          <span>
            Signed in as <strong>{userName}</strong>
          </span>
          <button type="button" onClick={onSignOut}>
            Sign out
          </button>
        </div>
      </header>

      <main className="layout">
        <section className="list" aria-label="Films">
          {list.status === 'loading' && <p role="status">Loading films…</p>}
          {list.status === 'error' && (
            <div role="alert" className="error">
              <p>{list.message}</p>
              <button type="button" onClick={retry}>
                Retry
              </button>
            </div>
          )}
          <ul>
            {films.map((film) => (
              <li key={film.id}>
                <button
                  type="button"
                  className="film"
                  aria-pressed={film.id === selectedId}
                  onClick={() => setSelectedId(film.id)}
                >
                  <span className="film-text">
                    <span className="film-title">{film.title}</span>
                    <span className="film-meta">
                      {film.year} · {film.genre}
                    </span>
                  </span>
                  {!film.canView && <LockIcon />}
                  <RatingBadge rating={film.rating} />
                </button>
              </li>
            ))}
          </ul>
        </section>

        <section className="detail" aria-live="polite" aria-label="Film details">
          {!selected ? (
            list.status === 'ok' && <p className="muted">Select a film to see its details.</p>
          ) : (
            <>
              <h2>{selected.title}</h2>
              <p className="detail-meta">
                <RatingBadge rating={selected.rating} /> {selected.year} · {selected.genre}
              </p>
              {!current && <p role="status">Loading…</p>}
              {current?.status === 'ok' && (
                <>
                  <dl>
                    <dt>Director</dt>
                    <dd>{current.film.director}</dd>
                  </dl>
                  <p className="synopsis">{current.film.synopsis}</p>
                </>
              )}
              {current?.status === 'message' && (
                <div className={current.restricted ? 'notice restricted' : 'notice error'} role="alert">
                  {current.restricted && <LockIcon />}
                  <span>{current.message}</span>
                </div>
              )}
            </>
          )}
        </section>
      </main>
    </div>
  )
}

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : 'Something went wrong.'
}
