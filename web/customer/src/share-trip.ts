import { CustomerTrip, kmLabel, paymentLabel, peso, tripHeadline } from './api'

export type TripSharePayload = {
  title: string
  text: string
}

export function formatTripShare(trip: CustomerTrip): TripSharePayload {
  const lines = [
    `Ya! Pasakay — ${trip.reference}`,
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
    `Fare: ${peso(trip.fare)}${kmLabel(trip.distanceKm) ? ` · ${kmLabel(trip.distanceKm)}` : ''} · ${paymentLabel(trip.paymentMethod, trip.paymentMethodOther)}`,
    `Operator: ${trip.operatorName}`,
  ].filter(Boolean) as string[]

  return {
    title: `Ya! Pasakay ${trip.reference}`,
    text: lines.join('\n'),
  }
}

export function whatsAppShareUrl(text: string) {
  return `https://wa.me/?text=${encodeURIComponent(text)}`
}

export async function copyTripShare(trip: CustomerTrip) {
  const { text } = formatTripShare(trip)
  await navigator.clipboard.writeText(text)
}

export async function nativeShareTrip(trip: CustomerTrip) {
  const { title, text } = formatTripShare(trip)
  await navigator.share({ title, text })
}

export function canNativeShare() {
  return typeof navigator.share === 'function'
}
