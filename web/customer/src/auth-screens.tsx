import { FormEvent, useEffect, useRef, useState } from 'react'
import logo from './logo-circle.png'
import { LoginBrandPanel, LoginTrustBar, LoginVehicleCards } from './login-brand-panel'
import { api, Desk, saveToken } from './api'

function loadGoogleScript() {
  return new Promise<void>((resolve, reject) => {
    if (window.google?.accounts?.id) {
      resolve()
      return
    }
    const existing = document.querySelector('script[data-google-gis]')
    if (existing) {
      existing.addEventListener('load', () => resolve())
      existing.addEventListener('error', () => reject(new Error('Could not load Google sign-in.')))
      return
    }
    const script = document.createElement('script')
    script.src = 'https://accounts.google.com/gsi/client'
    script.async = true
    script.defer = true
    script.dataset.googleGis = 'true'
    script.onload = () => resolve()
    script.onerror = () => reject(new Error('Could not load Google sign-in.'))
    document.head.appendChild(script)
  })
}

function GoogleMark() {
  return (
    <svg className="google-signin-mark" viewBox="0 0 48 48" aria-hidden="true">
      <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.3 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 8 3.1l5.7-5.7C34.2 6.1 29.4 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.3-.4-3.5z" />
      <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 16 19 12 24 12c3.1 0 5.8 1.2 8 3.1l5.7-5.7C34.2 6.1 29.4 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
      <path fill="#4CAF50" d="M24 44c5.2 0 10-2 13.6-5.2l-6.3-5.2C29.2 35.3 26.7 36 24 36c-5.3 0-9.7-3.3-11.3-8l-6.5 5C9.6 39.6 16.3 44 24 44z" />
      <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-1.1 3.1-3.5 5.5-6.4 6.6l6.3 5.2C38.9 36.8 44 31.2 44 24c0-1.3-.1-2.3-.4-3.5z" />
    </svg>
  )
}

export function AuthScreen({ onReady }: { onReady: (desk: Desk) => void }) {
  const wrapRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLDivElement>(null)
  const onReadyRef = useRef(onReady)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [missingClient, setMissingClient] = useState(false)

  onReadyRef.current = onReady

  useEffect(() => {
    let cancelled = false

    async function mountGoogleButton() {
      const host = buttonRef.current
      const wrap = wrapRef.current
      if (!host || !wrap) return

      try {
        const { googleClientId } = await api.authConfig()
        if (!googleClientId) {
          if (!cancelled) setMissingClient(true)
          return
        }
        await loadGoogleScript()
        if (cancelled || !buttonRef.current || !window.google?.accounts?.id) return

        window.google.accounts.id.initialize({
          client_id: googleClientId,
          ux_mode: 'popup',
          callback: async (response) => {
            setBusy(true)
            setError('')
            try {
              const auth = await api.googleSignIn(response.credential)
              if (auth.user.role !== 'Customer') throw new Error('Use a customer Google account.')
              saveToken(auth.accessToken)
              onReadyRef.current(await api.desk())
            } catch (err) {
              setError(err instanceof Error ? err.message : 'Google sign-in failed.')
            } finally {
              setBusy(false)
            }
          },
        })

        host.replaceChildren()
        const width = Math.max(240, Math.floor(wrap.clientWidth || 280))
        window.google.accounts.id.renderButton(host, {
          type: 'standard',
          theme: 'outline',
          size: 'large',
          text: 'signin_with',
          shape: 'pill',
          width,
          locale: 'en',
        })
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not start Google sign-in.')
      }
    }

    void mountGoogleButton()
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <div className="login">
      <LoginBrandPanel
        kicker="Motorcycle · Tricycle · Live map"
        title="Go where you need to go."
        description="Book a ride in seconds, watch your rider on the map, and get there with Ya! Pasakay."
        showPoints
        animateCopy
      />
      <div className="login-form">
        <img className="login-form-mark" src={logo} alt="" />
        <h2>Welcome back</h2>
        <p className="lede login-lede-full">Sign in with Google to book a motorcycle or tricycle, or create your Ya! Pasakay profile in one tap.</p>
        <p className="lede login-lede-short">Sign in with Google to book rides and track your driver live.</p>
        {missingClient && (
          <p className="error">Google sign-in is not configured. Add GoogleAuth:ClientId in the API appsettings.</p>
        )}
        <div className="google-btn-wrap" ref={wrapRef}>
          <span className="google-signin">
            <GoogleMark />
            Sign in with Google
          </span>
          <div className="google-btn" ref={buttonRef} />
        </div>
        {busy && <p className="muted">Signing you in…</p>}
        {error && <p className="error">{error}</p>}
        <LoginVehicleCards />
        <p className="login-safe">Your account stays on this device until you sign out.</p>
      </div>
      <LoginTrustBar />
    </div>
  )
}

export function CompleteMobile({ desk, onDesk }: { desk: Desk; onDesk: (desk: Desk) => void }) {
  const [phone, setPhone] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      onDesk(await api.updateMobile(phone))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save mobile number.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="login">
      <LoginBrandPanel
        kicker="Almost there"
        title="Add your mobile number."
        description="Riders and operators use this number to reach you during a trip."
      />
      <form className="login-form" onSubmit={submit}>
        <img className="login-form-mark" src={logo} alt="" />
        <h2>Mobile number</h2>
        <p className="lede">Enter a Philippine mobile number to finish setting up {desk.fullName}.</p>
        <label className="field">
          <span>PHONE</span>
          <input value={phone} onChange={(e) => setPhone(e.target.value)} autoComplete="tel" inputMode="tel" placeholder="09XX XXX XXXX" />
        </label>
        {error && <p className="error">{error}</p>}
        <button className="primary" disabled={busy || phone.trim().length < 10}>
          {busy ? 'Saving…' : 'Continue'}
        </button>
      </form>
    </div>
  )
}
