import motorcycleImg from './assets/rider-motorcycle-map.png'
import tricycleImg from './assets/tricycle.png'
import { VehicleType } from './api'

function svgArt(body: string) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 40" width="128" height="80">${body}</svg>`
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`
}

const sedanArt = svgArt(`
  <rect x="6" y="18" width="52" height="12" rx="3" fill="#1f2329"/>
  <path d="M14 18 20 10h24l6 8" fill="#e30613"/>
  <circle cx="18" cy="30" r="5" fill="#444"/><circle cx="46" cy="30" r="5" fill="#444"/>
  <rect x="22" y="12" width="10" height="6" rx="1" fill="#fff" opacity=".85"/>
  <rect x="34" y="12" width="10" height="6" rx="1" fill="#fff" opacity=".85"/>
`)

const mpvArt = svgArt(`
  <rect x="5" y="14" width="54" height="16" rx="4" fill="#1f2329"/>
  <path d="M12 14 18 8h28l6 6" fill="#e30613"/>
  <circle cx="18" cy="30" r="5" fill="#444"/><circle cx="46" cy="30" r="5" fill="#444"/>
  <rect x="20" y="10" width="24" height="7" rx="1" fill="#fff" opacity=".8"/>
`)

const suvArt = svgArt(`
  <rect x="6" y="12" width="52" height="18" rx="3" fill="#1f2329"/>
  <path d="M14 12 20 6h24l6 6" fill="#e30613"/>
  <circle cx="18" cy="30" r="5.5" fill="#444"/><circle cx="46" cy="30" r="5.5" fill="#444"/>
  <rect x="22" y="8" width="20" height="7" rx="1" fill="#fff" opacity=".85"/>
`)

const vanArt = svgArt(`
  <rect x="4" y="10" width="56" height="20" rx="3" fill="#1f2329"/>
  <path d="M8 10h20l4 6H8z" fill="#e30613"/>
  <circle cx="16" cy="30" r="5" fill="#444"/><circle cx="48" cy="30" r="5" fill="#444"/>
  <rect x="30" y="12" width="26" height="12" rx="1" fill="#fff" opacity=".35"/>
`)

const pickupArt = svgArt(`
  <rect x="4" y="16" width="28" height="12" rx="2" fill="#1f2329"/>
  <rect x="30" y="18" width="30" height="10" rx="1" fill="#e30613"/>
  <path d="M8 16 14 9h14l4 7" fill="#e30613"/>
  <circle cx="16" cy="30" r="5" fill="#444"/><circle cx="48" cy="30" r="5" fill="#444"/>
`)

const cargoArt = svgArt(`
  <rect x="4" y="16" width="24" height="12" rx="2" fill="#1f2329"/>
  <rect x="26" y="12" width="34" height="16" rx="2" fill="#6b7280"/>
  <path d="M8 16 13 10h12l3 6" fill="#e30613"/>
  <circle cx="14" cy="30" r="5" fill="#444"/><circle cx="48" cy="30" r="5" fill="#444"/>
  <text x="36" y="23" font-size="8" fill="#fff" text-anchor="middle" font-family="sans-serif">CARGO</text>
`)

export const VEHICLE_ART: Record<VehicleType, string> = {
  Motorcycle: motorcycleImg,
  Tricycle: tricycleImg,
  Sedan: sedanArt,
  Mpv: mpvArt,
  Suv: suvArt,
  Van: vanArt,
  PickupL300: pickupArt,
  PickupCargo: cargoArt,
  Custom: sedanArt,
}

export function vehicleArt(type: string | VehicleType | undefined | null, iconKey?: string | null) {
  if (iconKey) {
    const key = iconKey.toLowerCase()
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
