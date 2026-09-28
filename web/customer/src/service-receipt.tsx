import { kmLabel, passengerLabel, paymentLabel, peso, phWhen } from './api'

type ReceiptTrip = {
  status: string
  reference: string
  pickup: string
  dropoff: string
  fare: number
  customerFare?: number
  distanceKm: number
  passengerCount?: number
  vehicleType: string
  paymentMethod: unknown
  paymentMethodOther?: string | null
  riderName?: string | null
  plateNumber?: string | null
  requestedAtUtc: string
  scheduledAtUtc?: string | null
  completedAtUtc?: string | null
  isPromoSponsored?: boolean
  promoCode?: string | null
  discountPercent?: number | null
  promoDiscountAmount?: number
  fareDiscountLabel?: string | null
  fareDiscountAmount?: number
  customerBoostAmount?: number
  durationMinutes?: number | null
}

function money(value: number) {
  return `₱${Number(value || 0).toFixed(2)}`
}

function paidAmount(trip: ReceiptTrip) {
  if (typeof trip.customerFare === 'number' && trip.customerFare > 0) return trip.customerFare
  return trip.fare
}

export function ServiceReceipt({ trip }: { trip: ReceiptTrip }) {
  if (String(trip.status).toLowerCase() !== 'completed') return null

  const paid = paidAmount(trip)
  const original = trip.fare > paid ? trip.fare : paid
  const gap = Math.max(0, Math.round((original - paid) * 100) / 100)
  const riderOff = Math.min(
    gap,
    (trip.fareDiscountAmount ?? 0) > 0
      ? trip.fareDiscountAmount!
      : trip.fareDiscountLabel
        ? gap
        : 0,
  )
  const promoOff = Math.max(0, Math.round((gap - riderOff) * 100) / 100)
  const boost = Math.max(0, trip.customerBoostAmount ?? 0)
  const serviceFare = boost > 0 && original > boost ? original - boost : original
  const when = trip.completedAtUtc || trip.scheduledAtUtc || trip.requestedAtUtc
  const promoLabel = trip.promoCode
    || (trip.discountPercent != null ? `Save${trip.discountPercent}` : 'Promo')

  return (
    <section className="service-receipt" aria-label="Trip receipt">
      <header>
        <span>Receipt</span>
        <b>{trip.reference}</b>
      </header>
      <p className="service-receipt-when">{phWhen(when)}</p>
      <div className="service-receipt-route">
        <span>Pickup</span>
        <p>{trip.pickup}</p>
        <span>Drop-off</span>
        <p>{trip.dropoff}</p>
      </div>
      <p className="service-receipt-service">
        {trip.vehicleType}
        {trip.plateNumber ? ` · ${trip.plateNumber}` : ''}
        {trip.riderName ? ` · ${trip.riderName}` : ''}
        {' · '}
        {passengerLabel(trip.passengerCount)}
        {kmLabel(trip.distanceKm) ? ` · ${kmLabel(trip.distanceKm)}` : ''}
        {trip.durationMinutes ? ` · ${trip.durationMinutes} min` : ''}
      </p>
      <dl className="service-receipt-lines">
        <div>
          <dt>Fare</dt>
          <dd>{money(serviceFare)}</dd>
        </div>
        {boost > 0 ? (
          <div>
            <dt>Boost</dt>
            <dd>+{money(boost)}</dd>
          </div>
        ) : null}
        {promoOff > 0 ? (
          <div>
            <dt>{promoLabel}</dt>
            <dd>−{money(promoOff)}</dd>
          </div>
        ) : null}
        {riderOff > 0 ? (
          <div>
            <dt>{trip.fareDiscountLabel || 'Discount'}</dt>
            <dd>−{money(riderOff)}</dd>
          </div>
        ) : null}
        <div className="service-receipt-total">
          <dt>Amount paid</dt>
          <dd>{peso(paid)}</dd>
        </div>
      </dl>
      <p className="service-receipt-pay">{paymentLabel(trip.paymentMethod, trip.paymentMethodOther)}</p>
    </section>
  )
}
