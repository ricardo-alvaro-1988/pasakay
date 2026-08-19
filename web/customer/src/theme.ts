export type Theme = 'dark' | 'light'

const THEME_KEY = 'yapasakay-customer-theme'

export function readTheme(): Theme {
  const stored = localStorage.getItem(THEME_KEY)
  if (stored === 'dark' || stored === 'light') {
    return stored
  }
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function applyStoredTheme() {
  const theme = readTheme()
  document.documentElement.dataset.theme = theme
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme === 'dark' ? '#0d0f12' : '#eef1f5')
}

export function setTheme(theme: Theme) {
  localStorage.setItem(THEME_KEY, theme)
  document.documentElement.dataset.theme = theme
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme === 'dark' ? '#0d0f12' : '#eef1f5')
}
