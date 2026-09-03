export type BrandThemePreset = {
  id: string
  label: string
  accent: string
  good: string
}

export const DEFAULT_THEME_ID = 'pasakay-red'
export const DEFAULT_BRAND_NAME = 'Ya! Pasakay'
export const DEFAULT_SHORT_NAME = 'Pasakay'

export const BRAND_THEMES: BrandThemePreset[] = [
  { id: 'pasakay-red', label: 'Pasakay Red', accent: '#e30613', good: '#1ea36a' },
  { id: 'crimson-blaze', label: 'Crimson Blaze', accent: '#c41e3a', good: '#2ecc71' },
  { id: 'ruby-night', label: 'Ruby Night', accent: '#9b1b30', good: '#3dd68c' },
  { id: 'sunset-orange', label: 'Sunset Orange', accent: '#ff5a1f', good: '#16a34a' },
  { id: 'amber-ride', label: 'Amber Ride', accent: '#f59e0b', good: '#059669' },
  { id: 'gold-fleet', label: 'Gold Fleet', accent: '#d4a017', good: '#0d9488' },
  { id: 'lime-transit', label: 'Lime Transit', accent: '#84cc16', good: '#15803d' },
  { id: 'forest-green', label: 'Forest Green', accent: '#166534', good: '#22c55e' },
  { id: 'teal-route', label: 'Teal Route', accent: '#0d9488', good: '#65a30d' },
  { id: 'ocean-blue', label: 'Ocean Blue', accent: '#0284c7', good: '#10b981' },
  { id: 'royal-blue', label: 'Royal Blue', accent: '#1d4ed8', good: '#22c55e' },
  { id: 'indigo-drive', label: 'Indigo Drive', accent: '#4f46e5', good: '#14b8a6' },
  { id: 'violet-hail', label: 'Violet Hail', accent: '#7c3aed', good: '#34d399' },
  { id: 'magenta-metro', label: 'Magenta Metro', accent: '#c026d3', good: '#2dd4bf' },
  { id: 'pink-pulse', label: 'Pink Pulse', accent: '#db2777', good: '#4ade80' },
  { id: 'rose-city', label: 'Rose City', accent: '#e11d48', good: '#22c55e' },
  { id: 'slate-steel', label: 'Slate Steel', accent: '#475569', good: '#22c55e' },
  { id: 'charcoal-pro', label: 'Charcoal Pro', accent: '#1e293b', good: '#84cc16' },
  { id: 'cyan-signal', label: 'Cyan Signal', accent: '#06b6d4', good: '#22c55e' },
  { id: 'emerald-go', label: 'Emerald Go', accent: '#059669', good: '#f59e0b' },
  { id: 'navy-dispatch', label: 'Navy Dispatch', accent: '#1e3a8a', good: '#fbbf24' },
  { id: 'copper-road', label: 'Copper Road', accent: '#b45309', good: '#10b981' },
  { id: 'grape-lane', label: 'Grape Lane', accent: '#6d28d9', good: '#f472b6' },
  { id: 'mint-fresh', label: 'Mint Fresh', accent: '#10b981', good: '#3b82f6' },
  { id: 'scarlet-dash', label: 'Scarlet Dash', accent: '#dc2626', good: '#4ade80' },
  { id: 'tangerine-go', label: 'Tangerine Go', accent: '#ea580c', good: '#0ea5e9' },
  { id: 'honey-cab', label: 'Honey Cab', accent: '#ca8a04', good: '#0891b2' },
  { id: 'olive-fleet', label: 'Olive Fleet', accent: '#4d7c0f', good: '#f97316' },
  { id: 'jade-run', label: 'Jade Run', accent: '#047857', good: '#f59e0b' },
  { id: 'aqua-lane', label: 'Aqua Lane', accent: '#0891b2', good: '#eab308' },
  { id: 'skyline-blue', label: 'Skyline Blue', accent: '#0369a1', good: '#84cc16' },
  { id: 'cobalt-ride', label: 'Cobalt Ride', accent: '#1e40af', good: '#fb923c' },
  { id: 'iris-trip', label: 'Iris Trip', accent: '#5b21b6', good: '#34d399' },
  { id: 'orchid-hop', label: 'Orchid Hop', accent: '#a21caf', good: '#22d3ee' },
  { id: 'flamingo-fare', label: 'Flamingo Fare', accent: '#be185d', good: '#67e8f9' },
  { id: 'berry-book', label: 'Berry Book', accent: '#9f1239', good: '#86efac' },
  { id: 'storm-gray', label: 'Storm Gray', accent: '#334155', good: '#38bdf8' },
  { id: 'ink-black', label: 'Ink Black', accent: '#0f172a', good: '#a3e635' },
  { id: 'sand-route', label: 'Sand Route', accent: '#a8a29e', good: '#0f766e' },
  { id: 'coral-bay', label: 'Coral Bay', accent: '#f43f5e', good: '#14b8a6' },
]

export function resolveBrandTheme(themeId?: string | null): BrandThemePreset {
  return BRAND_THEMES.find((t) => t.id === themeId) ?? BRAND_THEMES[0]
}

export function accentSoft(accent: string, alpha = 0.16): string {
  const hex = accent.replace('#', '')
  if (hex.length !== 6) return `rgba(227, 6, 19, ${alpha})`
  const r = parseInt(hex.slice(0, 2), 16)
  const g = parseInt(hex.slice(2, 4), 16)
  const b = parseInt(hex.slice(4, 6), 16)
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}

export type BrandingConfig = {
  brandName: string
  shortName: string
  logoUrl: string | null
  faviconUrl?: string | null
  themeId: string
  accent: string
  good: string
  themes?: BrandThemePreset[]
}

export function applyFavicon(url: string | null | undefined, fallbackHref = '/favicon.png') {
  const href = url && url.trim() ? url : fallbackHref
  let link = document.querySelector<HTMLLinkElement>("link[rel*='icon']")
  if (!link) {
    link = document.createElement('link')
    link.rel = 'icon'
    document.head.appendChild(link)
  }
  link.href = href.includes('?') ? href : `${href}?v=${Date.now()}`
}

export function applyBrand(
  brand: Pick<BrandingConfig, 'accent' | 'good' | 'brandName' | 'shortName' | 'faviconUrl'>,
  options?: { titleSuffix?: string; fallbackFavicon?: string },
) {
  const root = document.documentElement
  root.style.setProperty('--accent', brand.accent)
  root.style.setProperty('--accent-soft', accentSoft(brand.accent, 0.16))
  root.style.setProperty('--good', brand.good)
  root.style.setProperty('--red', brand.accent)
  const suffix = options?.titleSuffix
  const title = suffix ? `${brand.brandName}${suffix}` : brand.brandName
  document.title = title
  const themeMeta = document.querySelector('meta[name="theme-color"]')
  if (themeMeta) themeMeta.setAttribute('content', brand.accent)
  const apple = document.querySelector('meta[name="apple-mobile-web-app-title"]')
  if (apple) apple.setAttribute('content', brand.shortName || brand.brandName)
  setMeta('name', 'description', `Book a motorcycle or tricycle ride with ${brand.brandName}.`)
  setMeta('property', 'og:site_name', brand.brandName)
  setMeta('property', 'og:title', title)
  setMeta('property', 'og:description', `Book a motorcycle or tricycle ride with ${brand.brandName}.`)
  setMeta('name', 'twitter:title', title)
  applyFavicon(brand.faviconUrl, options?.fallbackFavicon ?? '/favicon.png')
}

function setMeta(attr: 'name' | 'property', key: string, content: string) {
  let el = document.head.querySelector<HTMLMetaElement>(`meta[${attr}="${key}"]`)
  if (!el) {
    el = document.createElement('meta')
    el.setAttribute(attr, key)
    document.head.appendChild(el)
  }
  el.content = content
}
