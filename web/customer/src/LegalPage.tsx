import { useEffect, useState } from 'react'
import { api, mediaUrl } from './api'
import { applyBrand, type BrandingConfig } from './brand-themes'
import './legal.css'
import defaultLogo from './logo-circle.png'
import { DEFAULT_BRAND_NAME } from './brand-themes'

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
  const [logoFailed, setLogoFailed] = useState(false)
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
      {!logoFailed && (brand.logoUrl || brand.brandName === DEFAULT_BRAND_NAME) ? <img src={brand.logoUrl ? mediaUrl(brand.logoUrl) : defaultLogo} alt={`${brand.brandName} logo`} width="88" height="88" onError={() => setLogoFailed(true)} /> : <span className="public-legal-monogram" aria-label={brand.brandName}>{brand.shortName || brand.brandName}</span>}
      <div><p className="public-legal-name">{brand.brandName}</p><h1>{title}</h1></div>
    </header>
    <p className="public-legal-date">Last updated: 16 October 2026</p>
    {page === 'privacy' ? <>
      <p>This Privacy Policy explains how {brand.brandName} collects, uses, and shares personal information when you use our passenger app and customer website. It also explains your choices and how to contact us about your information.</p>
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
      <section><h2>Local storage and permissions</h2><p>The website stores session tokens and preferences on your device to support sign-in and remember your choices. Location access is used for pickup, nearby rider searches, and trip safety features when you allow it. You can change permissions through your browser or device settings and clear locally stored information.</p></section>
      <section><h2>Data retention and account requests</h2><p>You may request account deletion through Account management in the passenger app or by contacting us using the details below. Requests are reviewed by an administrator. Account closure currently disables sign-in and retains account and service records, including profile details, trip history, and messages. Closure does not automatically erase these records, and there is currently no defined automatic erasure period. Contact us to ask about specific information held about you.</p></section>
      <section><h2>Changes to this policy</h2><p>We may update this policy as our services or data practices change. The date above identifies the latest revision. Any updated policy will be published on this page.</p></section>
    </> : <>
      <p>You can request deletion of your {brand.brandName} passenger account and associated personal information by contacting our support team. You do not need to reinstall the app to send a request.</p>
      <section><h2>Request deletion by email</h2><ol><li>Send your request from the email address registered with {brand.brandName}.</li><li>Use the subject “Account and personal data deletion request” and include the name on your account. Do not include passwords or sign-in codes.</li><li>Specify whether you want to delete your account and associated personal information, or request deletion of particular information. We may ask for information needed to verify account ownership.</li></ol>
        {contact && <a className="legal-action" href={`mailto:${contact}?subject=${encodeURIComponent('Account and personal data deletion request')}`}>Email {contact}</a>}
        <p>You can also open Account → Account management → Account deletion in the passenger app.</p>
      </section>
      <section><h2>What happens after a request?</h2><p>An administrator reviews your request. You can continue using your account while it is pending. Approval currently disables account access; it does not automatically erase your profile, trip history, messages, or other service records. No automatic erasure schedule or guaranteed processing time is currently specified.</p></section>
    </>}
    <section><h2>Privacy questions{page === 'delete' ? ' and sign-in help' : ''}</h2>
      {contact ? <p>Contact {brand.brandName} at <a href={`mailto:${contact}`}>{contact}</a>. For an account request, include the email associated with your account. Never send your password or sign-in codes.</p>
        : <p>Contact {brand.brandName} using the developer contact details on its Google Play listing for privacy questions or help accessing your account.</p>}
    </section>
    <footer>{brand.brandName}</footer>
  </main>
}
