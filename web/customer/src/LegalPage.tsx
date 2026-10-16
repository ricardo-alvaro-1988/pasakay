import { useEffect, useState } from 'react'
import { api, mediaUrl } from './api'
import { applyBrand, type BrandingConfig } from './brand-themes'
import './legal.css'

export function legalPagePath() {
  const path = window.location.pathname.replace(/\/+$/, '').toLowerCase()
  if (path === '/privacy' || path === '/privacy-policy') return 'privacy'
  if (path === '/delete-account' || path === '/account-deletion') return 'delete'
  return null
}

export function LegalLinks() {
  return <nav className="legal-links" aria-label="Privacy and account"><a href="/privacy">Privacy policy</a><a href="/delete-account">Account deletion</a></nav>
}

export function LegalPage({ page }: { page: 'privacy' | 'delete' }) {
  const [brand, setBrand] = useState<BrandingConfig | null>(null)
  const [error, setError] = useState(false)
  const title = page === 'privacy' ? 'Privacy policy' : 'Account deletion'
  const domain = window.location.hostname.replace(/^www\./, '')
  const contact = (import.meta.env.VITE_PRIVACY_CONTACT_EMAIL || (domain.includes('.') && !/^[\d.]+$/.test(domain) ? `support@${domain}` : '')).trim()

  useEffect(() => {
    let cancelled = false
    api.branding().then((data) => {
      if (cancelled) return
      applyBrand(data)
      document.title = `${title} | ${data.brandName}`
      setBrand(data)
    }).catch(() => { if (!cancelled) setError(true) })
    return () => { cancelled = true }
  }, [title])

  if (!brand) return <main className="public-legal"><h1>{title}</h1>{error
    ? <><p role="alert">We could not load this service’s details.</p><button onClick={() => window.location.reload()}>Try again</button></>
    : <p role="status">Loading service details…</p>}</main>

  return <main className="public-legal">
    <header className="public-legal-brand">
      {brand.logoUrl && <img src={mediaUrl(brand.logoUrl)} alt={`${brand.brandName} logo`} width="72" height="72" />}
      <div><a href="/">{brand.brandName}</a><h1>{title}</h1></div>
    </header>
    <LegalLinks />
    {page === 'privacy' ? <>
      <p>This policy describes personal information used by the {brand.brandName} passenger app and customer website on this domain.</p>
      <section><h2>Information we collect and use</h2>
        <ul>
          <li>Account details: your name, email, mobile number, gender, profile photo when provided, and Google sign-in identifier, to identify you and manage your account.</li>
          <li>Location and bookings: pickup and destination locations, trip history, saved places, and location shared while using ride and safety features, to arrange rides, show nearby riders, and handle SOS alerts.</li>
          <li>Service records: booking details, selected payment method, fares, ratings, messages, and support or deletion requests, to fulfil services and resolve issues.</li>
          <li>Session and device information: sign-in tokens and notification registration details, to keep you signed in and deliver service updates.</li>
        </ul>
      </section>
      <section><h2>Sharing information</h2><p>Assigned riders and operators receive the contact and trip details needed to provide your service. Authorized administrators can access account, booking, support, and safety records. Google services support sign-in and maps. Information needed for those features is processed by the respective service providers. We do not sell your personal data.</p></section>
      <section><h2>Your choices and security</h2><p>You can update your profile, manage location and notification permissions in your device settings, and sign out of the service. Disabling permissions may limit related features. Account access uses authentication; passwords or PINs, where used, are stored as hashes.</p></section>
      <section><h2>Account requests and retained records</h2><p>You can <a href="/delete-account">request account deletion online</a>. Requests are reviewed by an administrator. The current account-closure process disables sign-in and retains account and service records; approval does not automatically erase those records. No automatic erasure schedule is currently specified.</p></section>
    </> : <>
      <p>Request deletion of your {brand.brandName} passenger account using this website. You do not need to reinstall the mobile app.</p>
      <section><h2>Submit a request</h2><ol><li>Sign in with the Google account used for {brand.brandName}.</li><li>Review the account deletion screen and enter your request.</li><li>Select “Request deletion”. The screen will show when your request is pending review.</li></ol>
        <a className="legal-action" href="/?account=delete">Sign in to request deletion</a>
        <p>You can also open Account → Account management → Account deletion in the passenger app.</p>
      </section>
      <section><h2>What happens after a request?</h2><p>An administrator reviews your request. You can continue using your account while it is pending. Approval currently disables account access; it does not automatically erase your profile, trip history, messages, or other service records. No automatic erasure schedule or guaranteed processing time is currently specified.</p></section>
    </>}
    <section><h2>Privacy questions{page === 'delete' ? ' and sign-in help' : ''}</h2>
      {contact ? <p>Contact {brand.brandName} at <a href={`mailto:${contact}`}>{contact}</a>. For an account request, include the email associated with your account. Never send your password or sign-in codes.</p>
        : <p>Contact {brand.brandName} using the developer contact details on its Google Play listing for privacy questions or help accessing your account.</p>}
    </section>
    <footer><a href="/">Return to {brand.brandName}</a></footer>
  </main>
}
