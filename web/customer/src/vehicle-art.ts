import { VehicleType } from './api'

/** Same transparent cartoon assets as the passenger APK. */
function png(name: string) {
  return `/vehicles/${name}.png`
}

export const VEHICLE_ART: Record<VehicleType, string> = {
  Motorcycle: png('motorcycle'),
  Tricycle: png('tricycle'),
  Sedan: png('sedan'),
  Mpv: png('mpv'),
  Suv: png('suv'),
  Van: png('van'),
  PickupL300: png('pickup_l300'),
  PickupCargo: png('pickup_cargo'),
  Tuktuk: png('tuktuk'),
  Custom: png('sedan'),
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
  if (!type) return VEHICLE_ART.Sedan
  return (VEHICLE_ART as Record<string, string>)[type] ?? VEHICLE_ART.Sedan
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
