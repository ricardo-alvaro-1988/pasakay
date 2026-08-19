import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { applyStoredTheme } from './theme'
import App from './App'
import './styles.css'

applyStoredTheme()

if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js')
  })
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
