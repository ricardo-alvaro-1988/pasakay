import { useEffect, useState } from 'react'

export function PinPad({
  title,
  subtitle,
  error,
  busy,
  actionLabel = 'Continue',
  minLength = 4,
  maxLength = 6,
  autoSubmitAt,
  onSubmit,
  resetToken = 0,
}: {
  title: string
  subtitle: string
  error?: string
  busy?: boolean
  actionLabel?: string
  minLength?: number
  maxLength?: number
  autoSubmitAt?: number
  onSubmit: (pin: string) => void
  resetToken?: number
}) {
  const [value, setValue] = useState('')
  const [shake, setShake] = useState(false)

  useEffect(() => {
    setValue('')
  }, [resetToken])

  useEffect(() => {
    if (!error) return
    setShake(true)
    const t = window.setTimeout(() => setShake(false), 420)
    return () => window.clearTimeout(t)
  }, [error])

  function append(digit: string) {
    if (busy || value.length >= maxLength) return
    const next = value + digit
    setValue(next)
    if (autoSubmitAt && next.length === autoSubmitAt) onSubmit(next)
  }

  function backspace() {
    if (busy || !value) return
    setValue((prev) => prev.slice(0, -1))
  }

  return (
    <div className="pin-pad">
      <div className="pin-pad-lock" aria-hidden="true">
        <svg viewBox="0 0 24 24" width="28" height="28" fill="none">
          <path d="M7 10V8a5 5 0 0 1 10 0v2" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
          <rect x="5" y="10" width="14" height="11" rx="2.5" stroke="currentColor" strokeWidth="2" />
          <circle cx="12" cy="15.5" r="1.4" fill="currentColor" />
        </svg>
      </div>
      <h2>{title}</h2>
      <p className="muted">{subtitle}</p>
      <div className={`pin-slots${shake ? ' shake' : ''}${error ? ' has-error' : ''}`}>
        {Array.from({ length: maxLength }, (_, i) => (
          <span key={i} className={`pin-slot${i < value.length ? ' filled' : ''}${i === value.length ? ' active' : ''}`}>
            {i < value.length ? <i /> : null}
          </span>
        ))}
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="pin-keys">
        {['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', '⌫'].map((key, idx) => {
          if (!key) return <span key={`spacer-${idx}`} className="pin-key spacer" />
          if (key === '⌫') {
            return (
              <button key="del" type="button" className="pin-key" disabled={busy} onClick={backspace} aria-label="Delete">
                ⌫
              </button>
            )
          }
          return (
            <button key={key} type="button" className="pin-key" disabled={busy} onClick={() => append(key)}>
              {key}
            </button>
          )
        })}
      </div>
      <button
        className="primary"
        type="button"
        disabled={busy || value.length < minLength}
        onClick={() => onSubmit(value)}
      >
        {busy ? 'Please wait…' : actionLabel}
      </button>
    </div>
  )
}
