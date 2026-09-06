const LIGHT_THEME_COLOR = '#eef1f5'

/** Always light mode — dark toggle removed. */
export function applyStoredTheme() {
  document.documentElement.dataset.theme = 'light'
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', LIGHT_THEME_COLOR)
  try {
    localStorage.removeItem('yapasakay-customer-theme')
  } catch {
    /* ignore */
  }
}
