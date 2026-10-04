import { useEffect, useRef, useState } from 'react'
import logo from './logo-circle.png'
import { api } from './api'
import { applyBrand, DEFAULT_BRAND_NAME, type BrandingConfig } from './brand-themes'

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

function callbackScheme() {
  const params = new URLSearchParams(window.location.search)
  const fromQuery = (params.get('scheme') || '').trim()
  if (fromQuery && /^[a-z][a-z0-9+.-]*$/i.test(fromQuery)) return fromQuery
  return 'yapasakay-passenger'
}

function appPackage() {
  const params = new URLSearchParams(window.location.search)
  return (params.get('package') || 'com.yapasakay.passenger').trim() || 'com.yapasakay.passenger'
}

function openAppWithTicket(ticket: string) {
  const scheme = callbackScheme()
  const path = `auth?ticket=${encodeURIComponent(ticket)}`
  const custom = `${scheme}://${path}`
  const intent = `intent://${path}#Intent;scheme=${scheme};package=${appPackage()};end`
  const android = /Android/i.test(navigator.userAgent)
  window.location.href = android ? intent : custom
  window.setTimeout(() => {
    window.location.href = custom
  }, 900)
}

async function createTicket(idToken: string) {
  const response = await fetch('/api/auth/google/mobile-ticket', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ idToken }),
  })
  const body = (await response.json().catch(() => ({}))) as { ticket?: string; message?: string }
  if (!response.ok || !body.ticket) {
    throw new Error(body.message || 'Could not finish Google sign-in.')
  }
  return body.ticket
}

export function MobileAuthPage() {
  const buttonRef = useRef<HTMLDivElement>(null)
  const [branding, setBranding] = useState<BrandingConfig | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [ticket, setTicket] = useState('')

  useEffect(() => {
    let cancelled = false
    api
      .branding()
      .then((brand) => {
        if (cancelled) return
        applyBrand(brand)
        setBranding(brand)
      })
      .catch(() => {})
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    let cancelled = false

    async function mount() {
      const host = buttonRef.current
      if (!host) return
      try {
        const { googleClientId } = await api.authConfig()
        if (!googleClientId) {
          if (!cancelled) setError('Google sign-in is not configured.')
          return
        }
        await loadGoogleScript()
        if (cancelled || !buttonRef.current || !window.google?.accounts?.id) return

        window.google.accounts.id.initialize({
          client_id: googleClientId,
          ux_mode: 'popup',
          callback: async (response) => {
            const credential = response.credential?.trim()
            if (!credential) {
              setError('Google did not return a sign-in token.')
              return
            }
            setBusy(true)
            setError('')
            try {
              const nextTicket = await createTicket(credential)
              setTicket(nextTicket)
              openAppWithTicket(nextTicket)
            } catch (err) {
              setBusy(false)
              setError(err instanceof Error ? err.message : 'Could not finish Google sign-in.')
            }
          },
        })

        host.replaceChildren()
        window.google.accounts.id.renderButton(host, {
          type: 'standard',
          theme: 'outline',
          size: 'large',
          text: 'continue_with',
          shape: 'pill',
          width: 280,
          locale: 'en',
        })
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not start Google sign-in.')
      }
    }

    void mount()
    return () => {
      cancelled = true
    }
  }, [])

  const brandName = branding?.brandName || DEFAULT_BRAND_NAME
  const brandLogo = branding?.logoUrl || logo

  return (
    <div className="rider-dl">
      <div className="rider-dl-shell">
        <header className="rider-dl-brand">
          <img src={brandLogo} alt="" width={64} height={64} />
          <div>
            <p className="rider-dl-kicker">{brandName}</p>
            <h1>Continue with Google</h1>
          </div>
        </header>
        <p className="rider-dl-lead">Sign in to finish opening the passenger app.</p>
        {!ticket ? (
          <div style={{ display: 'flex', justifyContent: 'center', marginTop: 8 }} ref={buttonRef} />
        ) : null}
        {busy || ticket ? (
          <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
            <p className="muted" style={{ margin: 0 }}>
              Sign-in worked. Opening the app…
            </p>
            {ticket ? (
              <button className="rider-dl-cta" type="button" onClick={() => openAppWithTicket(ticket)}>
                Open Ya! Pasakay app
              </button>
            ) : null}
          </div>
        ) : null}
        {error ? <p className="rider-dl-error">{error}</p> : null}
      </div>
    </div>
  )
}

export function isMobileAuthPath(pathname = window.location.pathname) {
  const path = pathname.replace(/\/+$/, '') || '/'
  return path === '/mobile-auth'
}
