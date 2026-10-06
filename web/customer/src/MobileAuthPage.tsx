import { useEffect, useMemo, useState } from 'react'
import logo from './logo-circle.png'
import { api } from './api'
import { applyBrand, DEFAULT_BRAND_NAME, type BrandingConfig } from './brand-themes'

type AuthState = {
  scheme: string
  packageName: string
  nonce: string
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

function randomNonce() {
  const bytes = new Uint8Array(16)
  crypto.getRandomValues(bytes)
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')
}

function encodeState(state: AuthState) {
  return btoa(JSON.stringify(state)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

function decodeState(raw: string | null): AuthState | null {
  if (!raw) return null
  try {
    const padded = raw.replace(/-/g, '+').replace(/_/g, '/')
    const json = atob(padded + '='.repeat((4 - (padded.length % 4)) % 4))
    const parsed = JSON.parse(json) as Partial<AuthState>
    if (!parsed.scheme || !parsed.packageName || !parsed.nonce) return null
    return {
      scheme: String(parsed.scheme),
      packageName: String(parsed.packageName),
      nonce: String(parsed.nonce),
    }
  } catch {
    return null
  }
}

function readHashParams() {
  const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash
  return new URLSearchParams(hash)
}

function openAppWithTicket(ticket: string, scheme = callbackScheme(), packageName = appPackage()) {
  // Custom scheme first so FlutterWebAuth2 Custom Tabs can complete the handshake.
  const path = `auth?ticket=${encodeURIComponent(ticket)}`
  const custom = `${scheme}://${path}`
  const intent = `intent://${path}#Intent;scheme=${scheme};package=${packageName};end`
  const android = /Android/i.test(navigator.userAgent)
  window.location.replace(custom)
  if (android) {
    window.setTimeout(() => {
      window.location.href = intent
    }, 700)
  }
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

function googleAuthorizeUrl(clientId: string, state: AuthState) {
  // Must match an Authorized redirect URI exactly (currently https://yapasakay.com).
  // Do not append /mobile-auth — Google rejects that as invalid_request.
  const redirectUri = window.location.origin
  const params = new URLSearchParams({
    client_id: clientId,
    redirect_uri: redirectUri,
    response_type: 'id_token',
    scope: 'openid email profile',
    nonce: state.nonce,
    state: encodeState(state),
  })
  return `https://accounts.google.com/o/oauth2/v2/auth?${params.toString()}`
}

/** Google returns to origin#id_token=…; move that onto /mobile-auth so the ticket page can finish. */
export function captureMobileAuthOAuthReturn() {
  const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash
  if (!hash) return
  const params = new URLSearchParams(hash)
  if (!(params.get('id_token') || params.get('error'))) return
  if (!decodeState(params.get('state'))) return
  if (isMobileAuthPath()) return
  const search = window.location.search || ''
  window.location.replace(`/mobile-auth${search}${window.location.hash}`)
}

export function MobileAuthPage() {
  const [branding, setBranding] = useState<BrandingConfig | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [ticket, setTicket] = useState('')
  const [authorizeHref, setAuthorizeHref] = useState('')
  const [scheme, setScheme] = useState(callbackScheme())
  const [packageName, setPackageName] = useState(appPackage())

  const state = useMemo<AuthState>(
    () => ({
      scheme: callbackScheme(),
      packageName: appPackage(),
      nonce: randomNonce(),
    }),
    [],
  )

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

    async function boot() {
      try {
        const hash = readHashParams()
        const idToken = (hash.get('id_token') || '').trim()
        const oauthError = (hash.get('error') || '').trim()
        const returnedState = decodeState(hash.get('state'))

        if (oauthError) {
          setError(oauthError === 'access_denied' ? 'Google sign-in was cancelled.' : `Google sign-in failed (${oauthError}).`)
          window.history.replaceState({}, '', `${window.location.pathname}${window.location.search}`)
          return
        }

        if (idToken) {
          if (returnedState) {
            setScheme(returnedState.scheme)
            setPackageName(returnedState.packageName)
          }
          setBusy(true)
          setError('')
          try {
            const nextTicket = await createTicket(idToken)
            if (cancelled) return
            setTicket(nextTicket)
            window.history.replaceState({}, '', `${window.location.pathname}${window.location.search}`)
            openAppWithTicket(
              nextTicket,
              returnedState?.scheme || callbackScheme(),
              returnedState?.packageName || appPackage(),
            )
          } catch (err) {
            if (!cancelled) {
              setBusy(false)
              setError(err instanceof Error ? err.message : 'Could not finish Google sign-in.')
            }
          }
          return
        }

        const { googleClientId: clientId } = await api.authConfig()
        if (cancelled) return
        if (!clientId) {
          setError('Google sign-in is not configured.')
          return
        }
        const href = googleAuthorizeUrl(clientId, state)
        setAuthorizeHref(href)
        // Full-page OAuth works inside Custom Tabs (GIS popups do not).
        window.location.replace(href)
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not start Google sign-in.')
      }
    }

    void boot()
    return () => {
      cancelled = true
    }
  }, [state])

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

        {!ticket && !busy && authorizeHref ? (
          <a className="rider-dl-cta" href={authorizeHref}>
            Continue with Google
          </a>
        ) : null}

        {busy || ticket ? (
          <div style={{ marginTop: 16, display: 'grid', gap: 12 }}>
            <p className="muted" style={{ margin: 0 }}>
              Sign-in worked. Opening the app…
            </p>
            {ticket ? (
              <button
                className="rider-dl-cta"
                type="button"
                onClick={() => openAppWithTicket(ticket, scheme, packageName)}
              >
                Open Ya! Pasakay app
              </button>
            ) : null}
          </div>
        ) : null}

        {error ? <p className="rider-dl-error">{error}</p> : null}
        {!ticket && !busy && !error ? (
          <p className="muted" style={{ marginTop: 14, fontSize: 12 }}>
            Redirecting to Google…
          </p>
        ) : null}
      </div>
    </div>
  )
}

export function isMobileAuthPath(pathname = window.location.pathname) {
  const path = pathname.replace(/\/+$/, '') || '/'
  return path === '/mobile-auth'
}
