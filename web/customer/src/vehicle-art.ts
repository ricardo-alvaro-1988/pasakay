import { VehicleType } from './api'

function svgArt(body: string) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 56" width="192" height="112">${body}</svg>`
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`
}

const motorcycleArt = svgArt(`
  <defs>
    <linearGradient id="mBody" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#2a2e36"/><stop offset="100%" stop-color="#121418"/></linearGradient>
    <linearGradient id="mRed" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#ff3b45"/><stop offset="100%" stop-color="#b80510"/></linearGradient>
    <radialGradient id="mTire" cx="50%" cy="45%" r="55%"><stop offset="0%" stop-color="#555"/><stop offset="100%" stop-color="#0d0d0d"/></radialGradient>
  </defs>
  <ellipse cx="48" cy="50" rx="38" ry="3.5" fill="#000" opacity=".12"/>
  <circle cx="24" cy="38" r="11" fill="url(#mTire)"/><circle cx="24" cy="38" r="5.5" fill="#c9ccd1"/><circle cx="24" cy="38" r="2.2" fill="#333"/>
  <circle cx="72" cy="38" r="11" fill="url(#mTire)"/><circle cx="72" cy="38" r="5.5" fill="#c9ccd1"/><circle cx="72" cy="38" r="2.2" fill="#333"/>
  <path d="M30 36h28l8-12H42l-6 6H28z" fill="url(#mBody)"/>
  <path d="M42 24h16l5-9H48z" fill="url(#mRed)"/>
  <path d="M58 15h12v4H60z" fill="#1a1d22"/>
  <rect x="36" y="20" width="12" height="7" rx="1.5" fill="#dce7f5" opacity=".9"/>
  <path d="M22 28h9l3 8H24z" fill="url(#mRed)"/>
`)

const tricycleArt = svgArt(`
  <defs>
    <linearGradient id="tCab" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#ff3b45"/><stop offset="100%" stop-color="#b80510"/></linearGradient>
    <linearGradient id="tBody" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#2a2e36"/><stop offset="100%" stop-color="#121418"/></linearGradient>
    <radialGradient id="tTire" cx="50%" cy="45%" r="55%"><stop offset="0%" stop-color="#555"/><stop offset="100%" stop-color="#111"/></radialGradient>
  </defs>
  <ellipse cx="48" cy="50" rx="40" ry="3.5" fill="#000" opacity=".12"/>
  <circle cx="20" cy="38" r="9" fill="url(#tTire)"/><circle cx="52" cy="38" r="9" fill="url(#tTire)"/><circle cx="74" cy="39" r="7.5" fill="url(#tTire)"/>
  <path d="M24 22h34l10 14H18z" fill="url(#tBody)"/>
  <path d="M26 12h30l7 10H22z" fill="url(#tCab)"/>
  <rect x="30" y="15" width="11" height="6" rx="1" fill="#e8f1ff" opacity=".92"/>
  <rect x="44" y="15" width="11" height="6" rx="1" fill="#e8f1ff" opacity=".92"/>
  <path d="M58 24h16l6 12H62z" fill="url(#tCab)"/>
`)

const sedanArt = svgArt(`
  <defs>
    <linearGradient id="sBody" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#2f343d"/><stop offset="100%" stop-color="#14171c"/></linearGradient>
    <linearGradient id="sRoof" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#ff4450"/><stop offset="100%" stop-color="#c00812"/></linearGradient>
    <radialGradient id="sTire" cx="50%" cy="40%" r="55%"><stop offset="0%" stop-color="#666"/><stop offset="100%" stop-color="#111"/></radialGradient>
  </defs>
  <ellipse cx="48" cy="50" rx="42" ry="3.5" fill="#000" opacity=".12"/>
  <path d="M10 30c2-6 8-10 16-12l8-8h30l10 8c8 2 14 6 16 12v6H10z" fill="url(#sBody)"/>
  <path d="M30 18l6-8h24l7 8H30z" fill="url(#sRoof)"/>
  <rect x="34" y="12" width="12" height="7" rx="1.2" fill="#dce9fb" opacity=".95"/>
  <rect x="50" y="12" width="12" height="7" rx="1.2" fill="#dce9fb" opacity=".95"/>
  <circle cx="28" cy="38" r="8" fill="url(#sTire)"/><circle cx="70" cy="38" r="8" fill="url(#sTire)"/>
`)

const mpvArt = svgArt(`
  <defs>
    <linearGradient id="vBody" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#30363f"/><stop offset="100%" stop-color="#12151a"/></linearGradient>
    <linearGradient id="vRoof" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#ff4450"/><stop offset="100%" stop-color="#b80510"/></linearGradient>
    <radialGradient id="vTire" cx="50%" cy="40%" r="55%"><stop offset="0%" stop-color="#666"/><stop offset="100%" stop-color="#111"/></radialGradient>
  </defs>
  <ellipse cx="48" cy="50" rx="42" ry="3.5" fill="#000" opacity=".12"/>
  <path d="M8 28c2-8 10-14 20-16l6-6h30l8 6c10 2 18 8 20 16v8H8z" fill="url(#vBody)"/>
  <path d="M28 12l5-6h30l6 6H28z" fill="url(#vRoof)"/>
  <rect x="32" y="8" width="28" height="8" rx="1.5" fill="#dce9fb" opacity=".92"/>
  <circle cx="26" cy="38" r="8.5" fill="url(#vTire)"/><circle cx="72" cy="38" r="8.5" fill="url(#vTire)"/>
`)

const suvArt = mpvArt
const vanArt = mpvArt

const pickupArt = svgArt(`
  <defs>
    <linearGradient id="pBody" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#2f343d"/><stop offset="100%" stop-color="#14171c"/></linearGradient>
    <linearGradient id="pBed" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#ff4450"/><stop offset="100%" stop-color="#b80510"/></linearGradient>
    <radialGradient id="pTire" cx="50%" cy="40%" r="55%"><stop offset="0%" stop-color="#666"/><stop offset="100%" stop-color="#111"/></radialGradient>
  </defs>
  <ellipse cx="48" cy="50" rx="42" ry="3.5" fill="#000" opacity=".12"/>
  <rect x="8" y="22" width="36" height="14" rx="3" fill="url(#pBody)"/>
  <rect x="42" y="24" width="40" height="12" rx="2" fill="url(#pBed)"/>
  <path d="M12 22l8-10h18l6 10H12z" fill="url(#pBed)"/>
  <circle cx="22" cy="38" r="8" fill="url(#pTire)"/><circle cx="70" cy="38" r="8" fill="url(#pTire)"/>
`)

const tuktukArt = svgArt(`
  <defs>
    <linearGradient id="kCab" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#ff4450"/><stop offset="100%" stop-color="#b80510"/></linearGradient>
    <linearGradient id="kBody" x1="0" y1="0" x2="1" y2="1"><stop offset="0%" stop-color="#2a2e36"/><stop offset="100%" stop-color="#121418"/></linearGradient>
    <radialGradient id="kTire" cx="50%" cy="40%" r="55%"><stop offset="0%" stop-color="#666"/><stop offset="100%" stop-color="#111"/></radialGradient>
  </defs>
  <ellipse cx="48" cy="50" rx="38" ry="3.5" fill="#000" opacity=".12"/>
  <path d="M24 12h34l10 12H18z" fill="url(#kCab)"/>
  <rect x="18" y="22" width="52" height="14" rx="3" fill="url(#kBody)"/>
  <rect x="32" y="14" width="18" height="7" rx="1.2" fill="#e8f1ff" opacity=".92"/>
  <circle cx="28" cy="38" r="8" fill="url(#kTire)"/><circle cx="60" cy="38" r="8" fill="url(#kTire)"/><circle cx="44" cy="40" r="6" fill="url(#kTire)"/>
`)

const cargoArt = svgArt(`
  <defs>
    <linearGradient id="cBody" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#2f343d"/><stop offset="100%" stop-color="#14171c"/></linearGradient>
    <radialGradient id="cTire" cx="50%" cy="40%" r="55%"><stop offset="0%" stop-color="#666"/><stop offset="100%" stop-color="#111"/></radialGradient>
  </defs>
  <ellipse cx="48" cy="50" rx="42" ry="3.5" fill="#000" opacity=".12"/>
  <rect x="8" y="22" width="30" height="14" rx="3" fill="url(#cBody)"/>
  <rect x="36" y="16" width="46" height="20" rx="3" fill="#6b7280"/>
  <path d="M12 22l7-8h16l5 8H12z" fill="#e30613"/>
  <circle cx="20" cy="38" r="8" fill="url(#cTire)"/><circle cx="70" cy="38" r="8" fill="url(#cTire)"/>
  <text x="58" y="30" font-size="8" fill="#fff" text-anchor="middle" font-family="sans-serif">CARGO</text>
`)

export const VEHICLE_ART: Record<VehicleType, string> = {
  Motorcycle: motorcycleArt,
  Tricycle: tricycleArt,
  Sedan: sedanArt,
  Mpv: mpvArt,
  Suv: suvArt,
  Van: vanArt,
  PickupL300: pickupArt,
  PickupCargo: cargoArt,
  Tuktuk: tuktukArt,
  Custom: sedanArt,
}

export function vehicleArt(type: string | VehicleType | undefined | null, iconKey?: string | null) {
  if (iconKey) {
    const key = iconKey.toLowerCase()
    if (key.includes('tuktuk') || key.includes('tuk-tuk') || key.includes('tuk')) return VEHICLE_ART.Tuktuk
    if (key.includes('tricycle')) return VEHICLE_ART.Tricycle
    if (key.includes('cargo')) return VEHICLE_ART.PickupCargo
    if (key.includes('pickup') || key.includes('l300')) return VEHICLE_ART.PickupL300
    if (key.includes('van')) return VEHICLE_ART.Van
    if (key.includes('suv')) return VEHICLE_ART.Suv
    if (key.includes('mpv')) return VEHICLE_ART.Mpv
    if (key.includes('sedan') || key.includes('car') || key.includes('generic')) return VEHICLE_ART.Sedan
    if (key.includes('motor')) return VEHICLE_ART.Motorcycle
  }
  if (!type) return sedanArt
  return (VEHICLE_ART as Record<string, string>)[type] ?? sedanArt
}

export function vehicleMaxPassengers(type: VehicleType, offerMax?: number) {
  if (typeof offerMax === 'number' && offerMax > 0) return offerMax
  switch (type) {
    case 'Motorcycle':
      return 1
    case 'Tricycle':
    case 'Sedan':
      return 4
    case 'Mpv':
    case 'Suv':
      return 6
    case 'Van':
      return 12
    case 'PickupL300':
      return 10
    case 'PickupCargo':
      return 2
    case 'Tuktuk':
      return 5
    case 'Custom':
      return offerMax ?? 4
    default:
      return 1
  }
}

export function vehicleIsCargo(type: VehicleType, offerCargo?: boolean) {
  if (typeof offerCargo === 'boolean') return offerCargo
  return type === 'PickupCargo'
}

export function vehicleLabel(type: VehicleType) {
  switch (type) {
    case 'PickupL300':
      return 'Pickup L300'
    case 'PickupCargo':
      return 'Pickup (Cargo)'
    case 'Mpv':
      return 'MPV'
    case 'Suv':
      return 'SUV'
    case 'Custom':
      return 'Custom'
    default:
      return type
  }
}
