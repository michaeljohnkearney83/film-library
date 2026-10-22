import type { Rating } from '../api/client'

const ratingClass: Record<Rating, string> = {
  U: 'rating-u',
  PG: 'rating-pg',
  '12A': 'rating-12',
  '12': 'rating-12',
  '15': 'rating-15',
  '18': 'rating-18',
}

/** BBFC certificate badge. */
export function RatingBadge({ rating }: { rating: Rating }) {
  return (
    <span className={`rating ${ratingClass[rating]}`} aria-label={`Rated ${rating}`}>
      {rating}
    </span>
  )
}

export function LockIcon() {
  return (
    <svg className="lock" viewBox="0 0 16 16" width="14" height="14" role="img" aria-label="Age restricted">
      <path
        fill="currentColor"
        d="M8 1a3.5 3.5 0 0 0-3.5 3.5V6H4a1.5 1.5 0 0 0-1.5 1.5v6A1.5 1.5 0 0 0 4 15h8a1.5 1.5 0 0 0 1.5-1.5v-6A1.5 1.5 0 0 0 12 6h-.5V4.5A3.5 3.5 0 0 0 8 1Zm2 5H6V4.5a2 2 0 1 1 4 0V6Z"
      />
    </svg>
  )
}
