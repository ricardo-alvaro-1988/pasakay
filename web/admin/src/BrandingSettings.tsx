import { FormEvent, useEffect, useState } from 'react'
import { api } from './api'
import {
  applyBrand,
  BRAND_THEMES,
  DEFAULT_BRAND_NAME,
  DEFAULT_SHORT_NAME,
  DEFAULT_THEME_ID,
  resolveBrandTheme,
  type BrandingConfig,
} from './brand-themes'
import logoCircle from './asset/logo-circle.png'

type Props = {
  onApplied: (brand: BrandingConfig) => void
}

export function BrandingSettingsPage({ onApplied }: Props) {
  const [brandName, setBrandName] = useState(DEFAULT_BRAND_NAME)
  const [shortName, setShortName] = useState(DEFAULT_SHORT_NAME)
  const [themeId, setThemeId] = useState(DEFAULT_THEME_ID)
  const [logoUrl, setLogoUrl] = useState<string | null>(null)
  const [faviconUrl, setFaviconUrl] = useState<string | null>(null)
  const [themes, setThemes] = useState(BRAND_THEMES)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)

  const theme = resolveBrandTheme(themeId)
  const previewLogo = logoUrl || logoCircle

  useEffect(() => {
    let cancelled = false
    api
      .getBranding()
      .then((data) => {
        if (cancelled) return
        setBrandName(data.brandName)
        setShortName(data.shortName)
        setThemeId(data.themeId)
        setLogoUrl(data.logoUrl)
        setFaviconUrl(data.faviconUrl)
        if (data.themes?.length) {
          // Prefer the fuller local catalog if the running API is an older build.
          setThemes(data.themes.length >= BRAND_THEMES.length ? data.themes : BRAND_THEMES)
        }
        applyBrand(data, { titleSuffix: ' Admin' })
        onApplied(data)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load branding.')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [onApplied])

  useEffect(() => {
    applyBrand(
      {
        brandName,
        shortName,
        accent: theme.accent,
        good: theme.good,
        faviconUrl,
      },
      { titleSuffix: ' Admin' },
    )
  }, [brandName, shortName, theme.accent, theme.good, faviconUrl])

  async function save(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const data = await api.updateBranding({
        brandName: brandName.trim(),
        shortName: shortName.trim(),
        themeId,
      })
      setBrandName(data.brandName)
      setShortName(data.shortName)
      setThemeId(data.themeId)
      setLogoUrl(data.logoUrl)
      setFaviconUrl(data.faviconUrl)
      if (data.themes?.length) setThemes(data.themes)
      applyBrand(data, { titleSuffix: ' Admin' })
      onApplied(data)
      setNotice('Branding saved. Customer app will pick this up on refresh.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save branding.')
    } finally {
      setBusy(false)
    }
  }

  async function onLogo(file: File | null) {
    if (!file) return
    setBusy(true)
    setError('')
    try {
      const data = await api.uploadBrandLogo(file)
      setLogoUrl(data.logoUrl)
      setFaviconUrl(data.faviconUrl)
      applyBrand(data, { titleSuffix: ' Admin' })
      onApplied(data)
      setNotice('Logo uploaded.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not upload logo.')
    } finally {
      setBusy(false)
    }
  }

  async function onFavicon(file: File | null) {
    if (!file) return
    setBusy(true)
    setError('')
    try {
      const data = await api.uploadBrandFavicon(file)
      setLogoUrl(data.logoUrl)
      setFaviconUrl(data.faviconUrl)
      applyBrand(data, { titleSuffix: ' Admin' })
      onApplied(data)
      setNotice('Favicon uploaded.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not upload favicon.')
    } finally {
      setBusy(false)
    }
  }

  async function clearLogo() {
    setBusy(true)
    setError('')
    try {
      const data = await api.updateBranding({
        brandName: brandName.trim(),
        shortName: shortName.trim(),
        themeId,
        clearLogo: true,
      })
      setLogoUrl(data.logoUrl)
      onApplied(data)
      setNotice('Logo reset to default.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not clear logo.')
    } finally {
      setBusy(false)
    }
  }

  async function clearFavicon() {
    setBusy(true)
    setError('')
    try {
      const data = await api.updateBranding({
        brandName: brandName.trim(),
        shortName: shortName.trim(),
        themeId,
        clearFavicon: true,
      })
      setFaviconUrl(data.faviconUrl)
      applyBrand(data, { titleSuffix: ' Admin' })
      onApplied(data)
      setNotice('Favicon reset to default.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not clear favicon.')
    } finally {
      setBusy(false)
    }
  }

  async function resetDefaults() {
    setBusy(true)
    setError('')
    try {
      const data = await api.updateBranding({
        brandName: DEFAULT_BRAND_NAME,
        shortName: DEFAULT_SHORT_NAME,
        themeId: DEFAULT_THEME_ID,
        clearLogo: true,
        clearFavicon: true,
      })
      setBrandName(data.brandName)
      setShortName(data.shortName)
      setThemeId(data.themeId)
      setLogoUrl(data.logoUrl)
      setFaviconUrl(data.faviconUrl)
      applyBrand(data, { titleSuffix: ' Admin' })
      onApplied(data)
      setNotice('Reset to Ya! Pasakay defaults.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not reset branding.')
    } finally {
      setBusy(false)
    }
  }

  if (loading) {
    return (
      <div className="card">
        <p className="muted">Loading branding…</p>
      </div>
    )
  }

  return (
    <div className="form-sections">
      <form className="card" onSubmit={save}>
        <h2 style={{ marginTop: 0 }}>Branding</h2>
        <p className="muted">
          Name, logo, favicon, and color theme for Admin and Customer. Changes apply without rebuilding the apps.
        </p>
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}

        <div className="brand-preview" style={{ borderColor: theme.accent }}>
          <div className="brand-preview-bar" style={{ background: theme.accent }}>
            <img src={previewLogo} alt="" />
            <strong>{brandName || DEFAULT_BRAND_NAME}</strong>
          </div>
          <div className="brand-preview-body">
            <button type="button" className="btn" style={{ background: theme.accent, borderColor: theme.accent, maxWidth: 180 }}>
              Primary action
            </button>
            <span className="muted">Accent {theme.accent} · Good {theme.good}</span>
          </div>
        </div>

        <div className="form-grid">
          <label className="field">
            <span>Brand name</span>
            <input value={brandName} onChange={(e) => setBrandName(e.target.value)} maxLength={80} required />
          </label>
          <label className="field">
            <span>Short name</span>
            <input value={shortName} onChange={(e) => setShortName(e.target.value)} maxLength={40} placeholder="Pasakay" />
          </label>
        </div>

        <div className="grid-2" style={{ marginTop: 12 }}>
          <div className="field">
            <span>Logo</span>
            <div className="brand-asset-row">
              <img className="brand-mark" src={previewLogo} alt="Logo preview" />
              <div>
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/webp,image/svg+xml"
                  onChange={(e) => void onLogo(e.target.files?.[0] ?? null)}
                  disabled={busy}
                />
                {logoUrl ? (
                  <button className="btn ghost" type="button" onClick={() => void clearLogo()} disabled={busy}>
                    Use default logo
                  </button>
                ) : null}
              </div>
            </div>
          </div>
          <div className="field">
            <span>Favicon</span>
            <div className="brand-asset-row">
              <img className="brand-favicon-preview" src={faviconUrl || '/favicon.png'} alt="Favicon preview" />
              <div>
                <input
                  type="file"
                  accept="image/png,image/jpeg,image/webp,image/x-icon,image/vnd.microsoft.icon,.ico"
                  onChange={(e) => void onFavicon(e.target.files?.[0] ?? null)}
                  disabled={busy}
                />
                {faviconUrl ? (
                  <button className="btn ghost" type="button" onClick={() => void clearFavicon()} disabled={busy}>
                    Use default favicon
                  </button>
                ) : null}
              </div>
            </div>
          </div>
        </div>

        <h3 style={{ marginTop: 20 }}>Theme ({themes.length} presets)</h3>
        <div className="brand-theme-grid">
          {themes.map((item) => (
            <button
              key={item.id}
              type="button"
              className={`brand-theme-chip${themeId === item.id ? ' on' : ''}`}
              onClick={() => setThemeId(item.id)}
              title={item.label}
            >
              <span className="brand-theme-swatch" style={{ background: item.accent }} />
              <span>{item.label}</span>
            </button>
          ))}
        </div>

        <div className="row-actions" style={{ marginTop: 18, gap: 10, display: 'flex', flexWrap: 'wrap' }}>
          <button className="btn" type="submit" disabled={busy} style={{ maxWidth: 200 }}>
            {busy ? 'Saving…' : 'Save branding'}
          </button>
          <button className="btn ghost" type="button" disabled={busy} onClick={() => void resetDefaults()}>
            Reset to Ya! Pasakay defaults
          </button>
        </div>
      </form>
    </div>
  )
}
