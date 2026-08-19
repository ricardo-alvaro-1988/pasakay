import type { Theme } from './theme'

type Props = {
  theme: Theme
  onChange: (theme: Theme) => void
}

export function ThemeSwitch({ theme, onChange }: Props) {
  const dark = theme === 'dark'
  return (
    <button
      type="button"
      className={`ios-switch${dark ? ' on' : ''}`}
      role="switch"
      aria-checked={dark}
      aria-label={dark ? 'Switch to light mode' : 'Switch to dark mode'}
      onClick={() => onChange(dark ? 'light' : 'dark')}
    >
      <span className="ios-switch-icons" aria-hidden="true">
        <SunIcon />
        <MoonIcon />
      </span>
      <span className="ios-switch-knob" />
    </button>
  )
}

function SunIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2">
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
    </svg>
  )
}

function MoonIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2">
      <path d="M21 14.5A8.5 8.5 0 1 1 9.5 3a6.5 6.5 0 0 0 11.5 11.5z" />
    </svg>
  )
}
