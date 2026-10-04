import { FormEvent, useEffect, useState } from 'react'
import {
  api,
  BookBody,
  Desk,
  PaymentMethod,
  Quote,
  Stop,
  VehicleType,
} from './api'
import { PaymentBar } from './account-screens'
import { NoOperatorNotice, useNoOperatorNotice } from './no-operator-notice'
import {
  geocodeText,
  loadGoogleMaps,
  PH,
  placeDetails,
  Prediction,
  searchPlaces,
  StopResult,
} from './maps'
import { lastKnownGps } from './gps'
import { vehicleArt, vehicleIsCargo, vehicleLabel, vehicleMaxPassengers } from './vehicle-art'

const PH_TZ = 'Asia/Manila'

function peso(n: number) {
  return `₱${n.toLocaleString('en-PH', { maximumFractionDigits: 0 })}`
}

function kmLabel(km: number) {
  if (!Number.isFinite(km) || km <= 0) return ''
  return `${km.toFixed(1)} km`
}

function isOperatorCoverageError(message: string) {
  return /no operator|not covered|outside|municipality/i.test(message)
}

function fromPhInput(value: string) {
  const raw = value.trim()
  if (!raw) return null
  const normalized = raw.length === 16 ? `${raw}:00` : raw
  const stamp = new Date(`${normalized}+08:00`)
  return Number.isNaN(stamp.getTime()) ? null : stamp.toISOString()
}

function toPhInput(value: string | null | undefined) {
  if (!value) return ''
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: PH_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(value))
  const get = (type: string) => parts.find((p) => p.type === type)?.value ?? ''
  return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`
}

function bookBody(
  vehicle: VehicleType,
  pickup: Stop,
  dropoff: Stop,
  payment: PaymentMethod,
  refNo = '',
  passengerCount = 1,
  categoryId?: string | null,
  maxPassengers?: number,
  isCargo?: boolean,
): BookBody {
  const max = vehicleMaxPassengers(vehicle, maxPassengers)
  const cargo = vehicleIsCargo(vehicle, isCargo)
  return {
    vehicleType: vehicle,
    vehicleCategoryId: categoryId ?? undefined,
    pickupBarangayId: pickup.barangayId,
    pickupDetails: pickup.details || pickup.label,
    pickupLat: pickup.lat,
    pickupLng: pickup.lng,
    dropoffBarangayId: dropoff.barangayId,
    dropoffDetails: dropoff.details || dropoff.label,
    dropoffLat: dropoff.lat,
    dropoffLng: dropoff.lng,
    paymentMethod: payment,
    paymentMethodOther: payment === 'Cash' ? undefined : (refNo.trim() || undefined),
    passengerCount: cargo || max <= 1 ? 1 : Math.min(max, Math.max(1, passengerCount)),
  }
}

function stopFromResult(hit: StopResult): Stop {
  return {
    label: hit.label,
    details: hit.details,
    lat: hit.lat,
    lng: hit.lng,
  }
}

function StopSearch({
  label,
  value,
  near,
  onPick,
}: {
  label: string
  value: Stop | null
  near?: { lat: number; lng: number } | null
  onPick: (stop: Stop | null) => void
}) {
  const [query, setQuery] = useState('')
  const [hints, setHints] = useState<Prediction[]>([])
  const [geoHits, setGeoHits] = useState<StopResult[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function runSearch() {
    const q = query.trim()
    if (q.length < 2) return
    setBusy(true)
    setError('')
    try {
      const { googleMapsBrowserKey } = await api.mapsConfig()
      const maps = await loadGoogleMaps(googleMapsBrowserKey)
      const center = near ?? lastKnownGps(300_000) ?? PH
      const [predictions, geos] = await Promise.all([
        searchPlaces(maps, q, center),
        geocodeText(maps, q, center),
      ])
      setHints(predictions.slice(0, 6))
      setGeoHits(geos.slice(0, 4))
      if (!predictions.length && !geos.length) setError('No places found.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed.')
    } finally {
      setBusy(false)
    }
  }

  async function pickPrediction(item: Prediction) {
    setBusy(true)
    setError('')
    try {
      const { googleMapsBrowserKey } = await api.mapsConfig()
      const maps = await loadGoogleMaps(googleMapsBrowserKey)
      const details = await placeDetails(maps, item.place_id)
      onPick({
        label: item.structured_formatting?.main_text || details.address.split(',')[0] || item.description,
        details: details.address,
        lat: details.lat,
        lng: details.lng,
      })
      setQuery('')
      setHints([])
      setGeoHits([])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load place.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="rental-stop-search">
      <label className="field">
        <span>{label}</span>
        {value ? (
          <div className="rental-stop-picked">
            <strong>{value.label}</strong>
            <small className="muted">{value.details}</small>
            <button type="button" className="ghost tiny" onClick={() => onPick(null)}>
              Change
            </button>
          </div>
        ) : (
          <>
            <div className="rental-stop-row">
              <input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault()
                    void runSearch()
                  }
                }}
                placeholder={`Search ${label.toLowerCase()}`}
              />
              <button type="button" className="secondary" disabled={busy || query.trim().length < 2} onClick={() => void runSearch()}>
                {busy ? '…' : 'Search'}
              </button>
            </div>
            {error ? <p className="error">{error}</p> : null}
            {hints.length || geoHits.length ? (
              <div className="rental-suggest">
                {hints.map((item) => (
                  <button key={item.place_id} type="button" onClick={() => void pickPrediction(item)}>
                    <b>{item.structured_formatting?.main_text || item.description}</b>
                    <small className="muted">{item.structured_formatting?.secondary_text || item.description}</small>
                  </button>
                ))}
                {geoHits.map((hit) => (
                  <button key={`${hit.lat},${hit.lng},${hit.details}`} type="button" onClick={() => { onPick(stopFromResult(hit)); setQuery(''); setHints([]); setGeoHits([]) }}>
                    <b>{hit.label}</b>
                    <small className="muted">{hit.details}</small>
                  </button>
                ))}
              </div>
            ) : null}
          </>
        )}
      </label>
    </div>
  )
}

export function RentalScreen({
  desk,
  onDesk,
  onGoBooking,
}: {
  desk: Desk
  onDesk: (desk: Desk) => void
  onGoBooking?: () => void
}) {
  const [pickup, setPickup] = useState<Stop | null>(null)
  const [dropoff, setDropoff] = useState<Stop | null>(null)
  const [vehicle, setVehicle] = useState<VehicleType>('Motorcycle')
  const [vehicleCategoryId, setVehicleCategoryId] = useState<string | null>(null)
  const [passengers, setPassengers] = useState(1)
  const [payment, setPayment] = useState<PaymentMethod>('Cash')
  const [paymentRef, setPaymentRef] = useState('')
  const [when, setWhen] = useState(() => toPhInput(new Date(Date.now() + 60 * 60 * 1000).toISOString()))
  const [quote, setQuote] = useState<Quote | null>(null)
  const [quoting, setQuoting] = useState(false)
  const [error, setError] = useState('')
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [coverageHint, setCoverageHint] = useState(false)
  const noOperator = useNoOperatorNotice(pickup, dropoff, true, coverageHint)

  const selectedOffer = noOperator.listedVehicles.find((v) => v.id === vehicleCategoryId)
    ?? noOperator.listedVehicles.find((v) => v.vehicleType === vehicle && v.available)
    ?? noOperator.availableVehicles.find((v) => v.vehicleType === vehicle)
    ?? null
  const selectedMaxPassengers = vehicleMaxPassengers(vehicle, selectedOffer?.maxPassengers)
  const selectedIsCargo = vehicleIsCargo(vehicle, selectedOffer?.isCargo)
  const showPassengerPicker = !!pickup && !selectedIsCargo && selectedMaxPassengers > 1
  const near = pickup ?? (desk.mapLat != null && desk.mapLng != null ? { lat: desk.mapLat, lng: desk.mapLng } : lastKnownGps(300_000))

  useEffect(() => {
    if (!pickup || !dropoff) {
      setQuote(null)
      setQuoting(false)
      setCoverageHint(false)
      return
    }
    let ignore = false
    void (async () => {
      setQuoting(true)
      try {
        const next = await api.quote(bookBody(vehicle, pickup, dropoff, payment, paymentRef, passengers, vehicleCategoryId, selectedMaxPassengers, selectedIsCargo))
        if (ignore) return
        setQuote(next)
        setCoverageHint(false)
        setError('')
      } catch (err) {
        if (ignore) return
        setQuote(null)
        const message = err instanceof Error ? err.message : 'Could not quote fare.'
        if (isOperatorCoverageError(message)) {
          setCoverageHint(true)
          setError('')
        } else {
          setError(message)
        }
      } finally {
        if (!ignore) setQuoting(false)
      }
    })()
    return () => { ignore = true }
  }, [pickup, dropoff, vehicle, vehicleCategoryId, payment, paymentRef, passengers, selectedMaxPassengers, selectedIsCargo])

  useEffect(() => {
    if (noOperator.useOffers) {
      const available = noOperator.availableVehicles
      const current = available.find((v) => v.id === vehicleCategoryId)
      if (current) return
      const first = available[0]
      if (first) {
        setVehicleCategoryId(first.id)
        setVehicle(first.vehicleType as VehicleType)
      }
      return
    }
    setVehicleCategoryId(null)
    setVehicle((current) => {
      const types = noOperator.availableTypes
      if (types.length && !types.includes(current)) return types[0] ?? current
      return current
    })
  }, [noOperator.availableTypes, noOperator.availableVehicles, noOperator.useOffers, vehicleCategoryId])

  useEffect(() => {
    if (!showPassengerPicker) {
      setPassengers(1)
      return
    }
    setPassengers((n) => Math.min(selectedMaxPassengers, Math.max(1, n)))
  }, [showPassengerPicker, selectedMaxPassengers, vehicle, vehicleCategoryId])

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!pickup?.lat || !dropoff?.lat) {
      setError('Set pickup and drop-off first.')
      return
    }
    const scheduledAtUtc = fromPhInput(when)
    if (!scheduledAtUtc) {
      setError('Choose a valid date and time.')
      return
    }
    if (new Date(scheduledAtUtc).getTime() < Date.now() + 10 * 60 * 1000) {
      setError('Schedule the booking at least 10 minutes from now.')
      return
    }
    if (!quote || quoting) {
      setError('Wait for the fare quote before scheduling.')
      return
    }
    setBusy(true)
    setError('')
    setNote('')
    try {
      onDesk(await api.book({
        ...bookBody(vehicle, pickup, dropoff, payment, paymentRef, passengers, vehicleCategoryId, selectedMaxPassengers, selectedIsCargo),
        scheduledAtUtc,
      }))
      setNote('Scheduled. Riders are notified about an hour before pickup.')
      setWhen(toPhInput(new Date(Date.now() + 60 * 60 * 1000).toISOString()))
      onGoBooking?.()
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Could not schedule.'
      if (isOperatorCoverageError(message)) {
        setCoverageHint(true)
        setError('')
      } else {
        setError(message)
      }
    } finally {
      setBusy(false)
    }
  }

  const quotePay = quote ? (quote.customerFare ?? quote.fare) : 0
  const quoteOriginal = quote?.originalFare && quote.originalFare > quotePay ? quote.originalFare : null
  const canSubmit = !!pickup?.lat && !!dropoff?.lat && !!quote && !quoting && !busy && !noOperator.searching && !noOperator.uncovered
    && (payment !== 'Other' || !!paymentRef.trim())

  return (
    <div className="rental-sheet">
      <div className="rental-sheet-body">
        <form className="rental-form" onSubmit={(e) => void submit(e)}>
          <h2>Rental</h2>
          <p className="muted">
            Schedule a ride for later (Philippine time). This creates a scheduled booking for your local operator.
          </p>
          <label className="field">
            <span>When (Philippines)</span>
            <input
              type="datetime-local"
              value={when}
              min={toPhInput(new Date(Date.now() + 10 * 60 * 1000).toISOString())}
              onChange={(e) => setWhen(e.target.value)}
              required
            />
          </label>
          <StopSearch
            label="Pickup"
            value={pickup}
            near={near}
            onPick={setPickup}
          />
          <StopSearch
            label="Drop-off"
            value={dropoff}
            near={near}
            onPick={setDropoff}
          />
          {pickup?.lat ? (
            <>
              <p className="section-title">Vehicle</p>
              <div className="vehicles rental-vehicle-list">
                {(noOperator.useOffers
                  ? noOperator.availableVehicles
                  : noOperator.availableTypes.map((t) => ({
                      id: t,
                      vehicleType: t,
                      name: vehicleLabel(t),
                      available: true,
                      maxPassengers: vehicleMaxPassengers(t),
                      isCargo: vehicleIsCargo(t),
                    }))
                ).map((item) => {
                  const type = item.vehicleType as VehicleType
                  const categoryId = 'id' in item && item.id !== type ? item.id : null
                  const canSelect = !('available' in item) || item.available !== false
                  const selected = canSelect && (categoryId ? vehicleCategoryId === categoryId : vehicle === type && !vehicleCategoryId)
                  const max = 'maxPassengers' in item && typeof item.maxPassengers === 'number'
                    ? item.maxPassengers
                    : vehicleMaxPassengers(type)
                  const cargo = 'isCargo' in item && typeof item.isCargo === 'boolean'
                    ? item.isCargo
                    : vehicleIsCargo(type)
                  const iconKey = 'iconKey' in item ? (item as { iconKey?: string }).iconKey : undefined
                  return (
                    <button
                      key={categoryId ?? type}
                      type="button"
                      disabled={!canSelect}
                      className={`vehicle${selected ? ' on' : ''}${!canSelect ? ' dim' : ''}`}
                      onClick={() => {
                        if (!canSelect) return
                        setVehicle(type)
                        setVehicleCategoryId(categoryId)
                        setPassengers(cargo || max <= 1 ? 1 : Math.min(passengers, max))
                      }}
                    >
                      <span className={`icon${type === 'Motorcycle' ? ' moto' : ''}`}>
                        <img src={vehicleArt(type, iconKey)} alt="" />
                      </span>
                      <span className="copy">
                        <b>{'name' in item ? item.name : vehicleLabel(type)}</b>
                      </span>
                    </button>
                  )
                })}
              </div>
            </>
          ) : null}
          {showPassengerPicker ? (
            <div className="passenger-picker" role="group" aria-label="Number of passengers">
              <span className="passenger-label">Passengers (max {selectedMaxPassengers})</span>
              <div className="passenger-controls">
                <button type="button" className="passenger-btn" disabled={passengers <= 1} onClick={() => setPassengers((n) => Math.max(1, n - 1))}>−</button>
                <input
                  className="passenger-input"
                  type="number"
                  min={1}
                  max={selectedMaxPassengers}
                  value={passengers}
                  onChange={(e) => {
                    const next = Math.floor(Number(e.target.value))
                    setPassengers(!Number.isFinite(next) || next < 1 ? 1 : Math.min(selectedMaxPassengers, next))
                  }}
                />
                <button type="button" className="passenger-btn" disabled={passengers >= selectedMaxPassengers} onClick={() => setPassengers((n) => Math.min(selectedMaxPassengers, n + 1))}>+</button>
              </div>
            </div>
          ) : null}
          <p className="section-title">Payment</p>
          <PaymentBar
            payment={payment}
            refNo={paymentRef}
            onPayment={(method) => {
              setPayment(method)
              if (method === 'Cash') setPaymentRef('')
            }}
            onRefNo={setPaymentRef}
          />
          {quoting ? (
            <p className="muted">Getting fare…</p>
          ) : quote ? (
            <p className="fareline">
              {quoteOriginal ? (
                <>
                  <b>{peso(quotePay)}</b>
                  <span className="promo-was"> was {peso(quoteOriginal)}</span>
                </>
              ) : (
                <b>{peso(quotePay)}</b>
              )}
              {kmLabel(quote.distanceKm) ? ` · ${kmLabel(quote.distanceKm)}` : ''}
            </p>
          ) : pickup?.lat && dropoff?.lat ? (
            <p className="muted">No fare available for this route yet.</p>
          ) : null}
          {error ? <p className="error">{error}</p> : null}
          <NoOperatorNotice show={noOperator.uncovered} />
          {note ? <p className="muted">{note}</p> : null}
          <button className="primary rental-submit" type="submit" disabled={!canSubmit}>
            {busy || noOperator.searching
              ? 'Scheduling…'
              : quoting
                ? 'Getting fare…'
                : quote
                  ? `Schedule · ${peso(quotePay)}`
                  : 'Schedule booking'}
          </button>
        </form>
      </div>
    </div>
  )
}
