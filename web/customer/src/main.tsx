import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { applyStoredTheme } from './theme'
import { captureMobileAuthOAuthReturn } from './MobileAuthPage'
import App from './App'
import './styles.css'

applyStoredTheme()
captureMobileAuthOAuthReturn()

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
