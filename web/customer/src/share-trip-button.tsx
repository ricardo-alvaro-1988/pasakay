import { useMemo, useRef, useState } from 'react'
import type { CustomerTrip } from './api'
import { canNativeShare, copyTripShare, formatTripShare, nativeShareTrip, whatsAppShareUrl } from './share-trip'

type Props = {
  trip: CustomerTrip
  onNote: (message: string) => void
  compact?: boolean
}

export function ShareTripButton({ trip, onNote, compact }: Props) {
  const [open, setOpen] = useState(false)
  const wrapRef = useRef<HTMLDivElement>(null)
  const payload = useMemo(() => formatTripShare(trip), [trip])

  async function shareNative() {
    try {
      await nativeShareTrip(trip)
      onNote('')
      setOpen(false)
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') {
        return
      }
      onNote('Could not open share.')
    }
  }

  async function shareCopy() {
    try {
      await copyTripShare(trip)
      onNote('Copied! Paste in Messenger or any chat app.')
      setOpen(false)
    } catch {
      onNote('Could not copy trip details.')
    }
  }

  function shareWhatsApp() {
    window.open(whatsAppShareUrl(payload.text), '_blank', 'noopener,noreferrer')
    setOpen(false)
    onNote('')
  }

  async function onClick() {
    if (canNativeShare()) {
      await shareNative()
      return
    }
    setOpen((value) => !value)
  }

  return (
    <div className={`share-wrap${compact ? ' compact' : ''}`} ref={wrapRef}>
      <button type="button" className="share icon-btn" aria-label="Share trip" title="Share" onClick={() => void onClick()}>
        <ShareIcon />
      </button>
      {open && !canNativeShare() && (
        <>
          <button type="button" className="share-backdrop" aria-label="Close share menu" onClick={() => setOpen(false)} />
          <div className="share-menu" role="menu">
            <button type="button" role="menuitem" onClick={() => void shareCopy()}>
              Copy for Messenger
            </button>
            <button type="button" role="menuitem" onClick={shareWhatsApp}>
              WhatsApp
            </button>
          </div>
        </>
      )}
    </div>
  )
}

function ShareIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="18" cy="5.5" r="2.4" stroke="currentColor" strokeWidth="1.8" />
      <circle cx="6" cy="12" r="2.4" stroke="currentColor" strokeWidth="1.8" />
      <circle cx="18" cy="18.5" r="2.4" stroke="currentColor" strokeWidth="1.8" />
      <path d="M8.2 10.7 15.8 6.6M8.2 13.3 15.8 17.4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  )
}
