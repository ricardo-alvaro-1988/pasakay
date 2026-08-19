export type Theme = 'dark' | 'light'

const THEME_KEY = 'yapasakay-theme'
const SIDEBAR_KEY = 'yapasakay-sidebar'

export function readTheme(): Theme {
  const stored = localStorage.getItem(THEME_KEY)
  if (stored === 'dark' || stored === 'light') {
    return stored
  }
  return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
}

export function applyStoredTheme() {
  document.documentElement.dataset.theme = readTheme()
}

export function setTheme(theme: Theme) {
  localStorage.setItem(THEME_KEY, theme)
  document.documentElement.dataset.theme = theme
}

export function readSidebarCollapsed() {
  return localStorage.getItem(SIDEBAR_KEY) === 'collapsed'
}

export function setSidebarCollapsed(collapsed: boolean) {
  localStorage.setItem(SIDEBAR_KEY, collapsed ? 'collapsed' : 'expanded')
}
