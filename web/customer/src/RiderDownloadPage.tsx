import { useEffect, useState } from 'react'
import logo from './logo-circle.png'
import { api, mediaUrl } from './api'
import { applyBrand, DEFAULT_BRAND_NAME, type BrandingConfig } from './brand-themes'

type Release = {
  version: string
  downloadUrl: string
  releasedAtUtc: string
  notes: string | null
}

function formatReleasedAt(value: string) {
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value))
  } catch {
    return value
  }
}

export function RiderDownloadPage() {
  const [branding, setBranding] = useState<BrandingConfig | null>(null)
  const [latest, setLatest] = useState<Release | null>(null)
  const [history, setHistory] = useState<Release[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false
    Promise.all([
      api.branding().catch(() => null),
      api.riderAppLatest().catch((err: unknown) => {
        if (err instanceof Error && /404|not found|no rider/i.test(err.message)) return null
        throw err
      }),
      api.riderAppReleases().catch(() => [] as Release[]),
    ])
      .then(([brand, release, releases]) => {
        if (cancelled) return
        if (brand) {
          applyBrand(brand)
          setBranding(brand)
        }
        setLatest(release)
        setHistory(releases.filter((row) => !release || row.version !== release.version).slice(0, 8))
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load the rider app.')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  const brandName = branding?.brandName || DEFAULT_BRAND_NAME
  const brandLogo = branding?.logoUrl || logo
  const downloadHref = latest ? mediaUrl(latest.downloadUrl) : null

  return (
    <div className="rider-dl">
      <div className="rider-dl-shell">
        <header className="rider-dl-brand">
          <img src={brandLogo} alt="" width={64} height={64} />
          <div>
            <p className="rider-dl-kicker">{brandName}</p>
            <h1>Rider app</h1>
          </div>
        </header>

        <p className="rider-dl-lead">
          Download the latest Android APK for drivers. Install from this page when your operator asks you to update.
        </p>

        {loading ? (
          <p className="muted">Checking for the latest build…</p>
        ) : error ? (
          <p className="rider-dl-error">{error}</p>
        ) : !latest || !downloadHref ? (
          <div className="rider-dl-empty">
            <h2>No build published yet</h2>
            <p className="muted">Ask your Super Admin to upload the rider APK from the admin portal.</p>
          </div>
        ) : (
          <section className="rider-dl-latest">
            <p className="rider-dl-version">
              Version <strong>{latest.version}</strong>
            </p>
            <p className="muted">Published {formatReleasedAt(latest.releasedAtUtc)}</p>
            {latest.notes ? <p className="rider-dl-notes">{latest.notes}</p> : null}
            <a className="rider-dl-cta" href={downloadHref} download={`pasakay-rider-${latest.version}.apk`}>
              Download APK
            </a>
          </section>
        )}

        {history.length > 0 ? (
          <section className="rider-dl-history">
            <h2>Previous versions</h2>
            <ul>
              {history.map((row) => (
                <li key={row.version}>
                  <div>
                    <strong>{row.version}</strong>
                    <span className="muted">{formatReleasedAt(row.releasedAtUtc)}</span>
                  </div>
                  <a href={mediaUrl(row.downloadUrl)} download={`pasakay-rider-${row.version}.apk`}>
                    APK
                  </a>
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </div>
    </div>
  )
}

export function isRiderDownloadPath(pathname = window.location.pathname) {
  const path = pathname.replace(/\/+$/, '') || '/'
  return path === '/rider/download'
}
