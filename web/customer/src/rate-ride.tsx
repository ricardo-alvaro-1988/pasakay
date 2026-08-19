import { FormEvent, useEffect, useState } from 'react'
import {
  api,
  CustomerTrip,
  Desk,
  peso,
  kmLabel,
  paymentLabel,
  tripHeadline,
  phWhen,
} from './api'

const STAR_LABELS = ['Tap to rate', 'Poor', 'Fair', 'Good', 'Great', 'Excellent'] as const

function StarGlyph({ filled }: { filled: boolean }) {
  return (
    <svg className="star-glyph" viewBox="0 0 24 24" aria-hidden="true">
      <path
        d="M12 2.5l2.76 5.59 6.17.9-4.46 4.35 1.05 6.14L12 16.9l-5.52 2.58 1.05-6.14-4.46-4.35 6.17-.9L12 2.5z"
        fill={filled ? 'currentColor' : 'none'}
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinejoin="round"
      />
    </svg>
  )
}

/** Shared rate sheet — used on Home and Booking after a completed ride. */
export function RateRidePanel({
  trip,
  onDesk,
  onError,
  onDone,
  allowSkip = true,
}: {
  trip: CustomerTrip
  onDesk: (desk: Desk) => void
  onError?: (text: string) => void
  onDone?: () => void
  allowSkip?: boolean
}) {
  const [stars, setStars] = useState(5)
  const [hover, setHover] = useState(0)
  const [comment, setComment] = useState('')
  const [busy, setBusy] = useState(false)
  const active = hover || stars

  async function submit(e?: FormEvent) {
    e?.preventDefault()
    setBusy(true)
    try {
      onDesk(await api.rate(trip.id, stars, comment.trim() || undefined))
      setComment('')
      setStars(5)
      onDone?.()
    } catch (err) {
      onError?.(err instanceof Error ? err.message : 'Could not save rating.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="trip-panel rate-panel" onSubmit={(e) => void submit(e)}>
      <p className="status-title">Rate your ride</p>
      <p className="muted">{trip.reference} · {trip.operatorName}</p>
      {trip.riderName && (
        <div className="rider" style={{ marginTop: 10 }}>
          {trip.riderPhotoUrl
            ? <img className="avatar lg" src={trip.riderPhotoUrl} alt="" />
            : <div className="avatar lg">{trip.riderName.slice(0, 1)}</div>}
          <div>
            <b>{trip.riderName}</b>
            <div className="muted">{[trip.plateNumber, trip.vehicleModel].filter(Boolean).join(' · ') || trip.vehicleType}</div>
          </div>
        </div>
      )}
      <p className="fareline" style={{ marginTop: 10 }}>
        <b>{peso(trip.fare)}</b>
        {kmLabel(trip.distanceKm) ? ` · ${kmLabel(trip.distanceKm)}` : ''} · {paymentLabel(trip.paymentMethod, trip.paymentMethodOther)}
      </p>
      <div className="star-rating">
        <div
          className="star-row"
          role="radiogroup"
          aria-label="Rate your ride"
          onMouseLeave={() => setHover(0)}
        >
          {[1, 2, 3, 4, 5].map((n) => (
            <button
              key={n}
              type="button"
              role="radio"
              aria-checked={stars === n}
              aria-label={`${n} star${n === 1 ? '' : 's'}`}
              className={`star-btn${n <= active ? ' on' : ''}`}
              onClick={() => setStars(n)}
              onMouseEnter={() => setHover(n)}
            >
              <StarGlyph filled={n <= active} />
            </button>
          ))}
        </div>
        <p className="star-label">{STAR_LABELS[active]}</p>
      </div>
      <label className="field rate-comment">
        <span>Comment (optional)</span>
        <textarea
          rows={2}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          maxLength={200}
          placeholder="Share what went well or what could improve…"
        />
      </label>
      <div className="actions rate-actions">
        {allowSkip && (
          <button className="secondary" type="button" onClick={() => onDone?.()}>Later</button>
        )}
        <button className="primary" type="submit" disabled={busy || stars < 1}>
          {busy ? 'Saving…' : 'Submit rating'}
        </button>
      </div>
    </form>
  )
}

export function usePendingRating(desk: Desk) {
  const pending = desk.pendingRating ?? desk.recent.find((t) => t.canRate) ?? null
  const [openId, setOpenId] = useState<string | null>(null)

  useEffect(() => {
    if (pending?.id) {
      setOpenId(pending.id)
    } else {
      setOpenId(null)
    }
  }, [pending?.id])

  const trip = pending && openId === pending.id ? pending : null
  return {
    trip,
    dismiss: () => setOpenId(null),
  }
}

export function BookingHistoryRating({ trip }: { trip: CustomerTrip }) {
  if (trip.rating == null) return null
  return (
    <p className="muted">
      Your rating: {'★'.repeat(trip.rating)}{'☆'.repeat(5 - trip.rating)}
      {trip.ratingComment ? ` · ${trip.ratingComment}` : ''}
    </p>
  )
}

export function formatTripWhen(trip: CustomerTrip) {
  return trip.scheduledAtUtc ? phWhen(trip.scheduledAtUtc) : phWhen(trip.requestedAtUtc)
}

export function tripStatusTag(trip: CustomerTrip) {
  return <span className={`tag ${String(trip.status).toLowerCase()}`}>{tripHeadline(String(trip.status))}</span>
}
