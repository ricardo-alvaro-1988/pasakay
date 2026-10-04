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

function finishWithToken(idToken: string) {
  const scheme = callbackScheme()
  const target = `${scheme}://auth?id_token=${encodeURIComponent(idToken)}`
  window.location.replace(target)
}

function tokenFromLocation() {
  const fromQuery = new URLSearchParams(window.location.search).get('id_token')?.trim()
  if (fromQuery) return fromQuery
  const hash = window.location.hash.replace(/^#/, '')
  if (!hash) return ''
  return new URLSearchParams(hash).get('id_token')?.trim() || ''
}

export function MobileAuthPage() {
  const buttonRef = useRef<HTMLDivElement>(null)
  const [branding, setBranding] = useState<BrandingConfig | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const existing = tokenFromLocation()
    if (existing) {
      setBusy(true)
      finishWithToken(existing)
    }
  }, [])

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
    if (tokenFromLocation()) return
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
          callback: (response) => {
            const credential = response.credential?.trim()
            if (!credential) {
              setError('Google did not return a sign-in token.')
              return
            }
            setBusy(true)
            setError('')
            finishWithToken(credential)
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
        <div style={{ display: 'flex', justifyContent: 'center', marginTop: 8 }} ref={buttonRef} />
        {busy ? <p className="muted">Returning to the app…</p> : null}
        {error ? <p className="rider-dl-error">{error}</p> : null}
      </div>
    </div>
  )
}

export function isMobileAuthPath(pathname = window.location.pathname) {
  const path = pathname.replace(/\/+$/, '') || '/'
  return path === '/mobile-auth'
}
