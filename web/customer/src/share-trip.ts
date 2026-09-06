import { CustomerTrip, kmLabel, paymentLabel, peso, tripHeadline } from './api'
import { DEFAULT_BRAND_NAME } from './brand-themes'

export type TripSharePayload = {
  title: string
  text: string
}

export function formatTripShare(trip: CustomerTrip, brandName = DEFAULT_BRAND_NAME): TripSharePayload {
  const brand = brandName.trim() || DEFAULT_BRAND_NAME
  const lines = [
    `${brand} — ${trip.reference}`,
    tripHeadline(String(trip.status)),
    '',
    trip.riderName ? `Rider: ${trip.riderName}` : null,
    trip.riderPhone ? `Phone: ${trip.riderPhone}` : null,
    [trip.plateNumber, trip.vehicleModel || trip.vehicleType].filter(Boolean).length
      ? `Vehicle: ${[trip.plateNumber, trip.vehicleModel || trip.vehicleType].filter(Boolean).join(' · ')}`
      : null,
    '',
    `Pickup: ${trip.pickup}`,
    `Drop-off: ${trip.dropoff}`,
    '',
    `Fare: ${peso(trip.customerFare && trip.customerFare > 0 ? trip.customerFare : trip.fare)}${trip.isPromoSponsored && trip.fare > (trip.customerFare ?? trip.fare) ? ` (was ${peso(trip.fare)})` : ''}${kmLabel(trip.distanceKm) ? ` · ${kmLabel(trip.distanceKm)}` : ''} · ${Math.max(1, trip.passengerCount || 1)} passenger${Math.max(1, trip.passengerCount || 1) === 1 ? '' : 's'} · ${paymentLabel(trip.paymentMethod, trip.paymentMethodOther)}`,
    `Operator: ${trip.operatorName}`,
  ].filter(Boolean) as string[]

  return {
    title: `${brand} ${trip.reference}`,
    text: lines.join('\n'),
  }
}

export function whatsAppShareUrl(text: string) {
  return `https://wa.me/?text=${encodeURIComponent(text)}`
}

export async function copyTripShare(trip: CustomerTrip, brandName = DEFAULT_BRAND_NAME) {
  const { text } = formatTripShare(trip, brandName)
  await navigator.clipboard.writeText(text)
}

export async function nativeShareTrip(trip: CustomerTrip, brandName = DEFAULT_BRAND_NAME) {
  const { title, text } = formatTripShare(trip, brandName)
  await navigator.share({ title, text })
}

export function canNativeShare() {
  return typeof navigator.share === 'function'
}
