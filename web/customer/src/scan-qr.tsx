import { useEffect, useState } from 'react'
import QRCode from 'qrcode'

export function customerQrPayload(customerId: string) {
  return `yapasakay:customer:${customerId}`
}

export function ShowQrButton({ onClick, disabled }: { onClick: () => void; disabled?: boolean }) {
  return (
    <button type="button" className="scan-fab" disabled={disabled} onClick={onClick} title="Show QR to rider" aria-label="Show QR to rider">
      <QrMark />
      <span>{disabled ? '…' : 'Show QR'}</span>
    </button>
  )
}

export function ShowQrOverlay({
  customerId,
  onClose,
}: {
  customerId: string
  onClose: () => void
}) {
  const [src, setSrc] = useState('')
  const [hint, setHint] = useState('Preparing your QR…')

  useEffect(() => {
    let alive = true
    QRCode.toDataURL(customerQrPayload(customerId), {
      margin: 1,
      width: 280,
      errorCorrectionLevel: 'M',
      color: { dark: '#16181d', light: '#ffffff' },
    })
      .then((url) => {
        if (!alive) return
        setSrc(url)
        setHint('Let the rider scan this. Then set pickup and drop-off.')
      })
      .catch(() => {
        if (alive) setHint('Could not draw the QR. Close and try again.')
      })
    return () => { alive = false }
  }, [customerId])

  return (
    <div className="scan-overlay qr-show">
      <div className="qr-card">
        <p className="qr-title">Show this to the rider</p>
        {src ? <img src={src} alt="Customer hail QR" /> : <div className="qr-wait" />}
        <p>{hint}</p>
        <button type="button" className="ghost scan-close" onClick={onClose}>Close</button>
      </div>
    </div>
  )
}

function QrMark() {
  return (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M7 4H5a1 1 0 0 0-1 1v2M17 4h2a1 1 0 0 1 1 1v2M4 17v2a1 1 0 0 0 1 1h2M20 17v2a1 1 0 0 1-1 1h-2" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" />
      <path d="M7 8h4v4H7zM13 8h4v2h-4zM13 12h2v4h-2zM7 14h4v2H7z" fill="currentColor" />
    </svg>
  )
}
