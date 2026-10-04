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
  reverseGeocode,
  StopResult,
  searchPlaces,
} from './maps'
import { lastKnownGps, readPickupGps } from './gps'
import { vehicleArt, vehicleIsCargo, vehicleLabel, vehicleMaxPassengers } from './vehicle-art'

const PH_TZ = 'Asia/Manila'

type SearchTarget = 'pickup' | 'dropoff' | null

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

function addressLabel(details: string) {
  return details.split(',')[0]?.trim() || details
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

const FALLBACK_VEHICLES: Array<{
  id: VehicleType
  vehicleType: VehicleType
  name: string
  available: boolean
  maxPassengers: number
  isCargo: boolean
}> = [
  { id: 'Motorcycle', vehicleType: 'Motorcycle', name: vehicleLabel('Motorcycle'), available: true, maxPassengers: vehicleMaxPassengers('Motorcycle'), isCargo: vehicleIsCargo('Motorcycle') },
  { id: 'Tricycle', vehicleType: 'Tricycle', name: vehicleLabel('Tricycle'), available: true, maxPassengers: vehicleMaxPassengers('Tricycle'), isCargo: vehicleIsCargo('Tricycle') },
]

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
  const [searchFor, setSearchFor] = useState<SearchTarget>(null)
  const [query, setQuery] = useState('')
  const [hints, setHints] = useState<Prediction[]>([])
  const [geoHits, setGeoHits] = useState<StopResult[]>([])
  const [locating, setLocating] = useState(false)
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
    if (!query.trim() || !searchFor) {
      setHints([])
      setGeoHits([])
      return
    }
    let ignore = false
    const handle = window.setTimeout(() => {
      void (async () => {
        try {
          const { googleMapsBrowserKey } = await api.mapsConfig()
          const maps = await loadGoogleMaps(googleMapsBrowserKey)
          const center = near ?? PH
          const [predictions, geos] = await Promise.all([
            searchPlaces(maps, query, center),
            geocodeText(maps, query, center),
          ])
          if (ignore) return
          setHints(predictions.slice(0, 8))
          setGeoHits(geos.slice(0, 6))
        } catch {
          if (!ignore) {
            setHints([])
            setGeoHits([])
          }
        }
      })()
    }, 180)
    return () => {
      ignore = true
      window.clearTimeout(handle)
    }
  }, [query, searchFor, near?.lat, near?.lng])

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

  function openSearch(target: 'pickup' | 'dropoff') {
    setSearchFor(target)
    setQuery('')
    setHints([])
    setGeoHits([])
    setError('')
  }

  function applyStop(target: 'pickup' | 'dropoff', stop: Stop) {
    if (target === 'pickup') setPickup(stop)
    else setDropoff(stop)
    setSearchFor(null)
    setQuery('')
    setHints([])
    setGeoHits([])
  }

  async function choosePrediction(item: Prediction) {
    if (!searchFor) return
    try {
      const { googleMapsBrowserKey } = await api.mapsConfig()
      const maps = await loadGoogleMaps(googleMapsBrowserKey)
      const place = await placeDetails(maps, item.place_id)
      applyStop(searchFor, {
        label: item.structured_formatting?.main_text || place.address.split(',')[0] || item.description,
        details: place.address,
        lat: place.lat,
        lng: place.lng,
      })
    } catch {
      setError('Could not load that place. Search again.')
    }
  }

  async function confirmTyped() {
    if (!searchFor) return
    const text = query.trim()
    if (!text) return
    try {
      const { googleMapsBrowserKey } = await api.mapsConfig()
      const maps = await loadGoogleMaps(googleMapsBrowserKey)
      const hits = await geocodeText(maps, text, near ?? PH)
      if (hits[0]) applyStop(searchFor, hits[0])
      else setError('No matching place. Try another search.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed.')
    }
  }

  async function useCurrentLocation() {
    setLocating(true)
    setError('')
    try {
      const cached = lastKnownGps(180_000)
      if (cached) {
        applyStop('pickup', {
          label: 'Getting address…',
          details: 'Current location',
          lat: cached.lat,
          lng: cached.lng,
        })
      }
      const pos = await readPickupGps()
      const here = { lat: pos.coords.latitude, lng: pos.coords.longitude }
      applyStop('pickup', {
        label: 'Getting address…',
        details: 'Current location',
        lat: here.lat,
        lng: here.lng,
      })
      const { googleMapsBrowserKey } = await api.mapsConfig()
      const maps = await loadGoogleMaps(googleMapsBrowserKey)
      const details = await reverseGeocode(maps, here.lat, here.lng)
      applyStop('pickup', {
        label: addressLabel(details),
        details,
        lat: here.lat,
        lng: here.lng,
      })
    } catch (err) {
      const cached = lastKnownGps(300_000)
      if (cached) {
        try {
          const { googleMapsBrowserKey } = await api.mapsConfig()
          const maps = await loadGoogleMaps(googleMapsBrowserKey)
          const details = await reverseGeocode(maps, cached.lat, cached.lng)
          applyStop('pickup', {
            label: addressLabel(details),
            details,
            lat: cached.lat,
            lng: cached.lng,
          })
        } catch {
          applyStop('pickup', {
            label: 'Current location',
            details: 'Current location',
            lat: cached.lat,
            lng: cached.lng,
          })
        }
      } else {
        setError(err instanceof Error ? err.message : 'Could not get GPS location.')
      }
    } finally {
      setLocating(false)
    }
  }

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

  const vehicleOptions = noOperator.useOffers
    ? noOperator.availableVehicles
    : (noOperator.availableTypes.length
        ? noOperator.availableTypes.map((t) => ({
            id: t,
            vehicleType: t,
            name: vehicleLabel(t),
            available: true,
            maxPassengers: vehicleMaxPassengers(t),
            isCargo: vehicleIsCargo(t),
          }))
        : FALLBACK_VEHICLES)

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
          <div className="stop">
            <div className="stop-row">
              <span className="pin beat"><span className="dot a" /></span>
              <button
                type="button"
                className={`addr${searchFor === 'pickup' ? ' on' : ''}`}
                onClick={() => openSearch('pickup')}
              >
                <small>Pickup</small>
                {pickup?.label ?? (locating ? 'Waiting for GPS…' : 'Tap to set pickup')}
              </button>
            </div>
            <div className="stop-row">
              <span className="pin beat"><span className="dot b" /></span>
              <button
                type="button"
                className={`addr${searchFor === 'dropoff' ? ' on' : ''}`}
                onClick={() => openSearch('dropoff')}
              >
                <small>Drop-off</small>
                {dropoff?.label ?? 'Tap to set drop-off'}
              </button>
            </div>
          </div>
          <p className="section-title">Vehicle</p>
          <div className="vehicles rental-vehicle-list">
            {vehicleOptions.map((item) => {
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
                    {'name' in item && item.name !== vehicleLabel(type) ? (
                      <small className="muted">{vehicleLabel(type)}</small>
                    ) : null}
                  </span>
                </button>
              )
            })}
          </div>
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
          {error && !searchFor ? <p className="error">{error}</p> : null}
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
      {searchFor ? (
        <div className="picker rental-picker">
          <button
            className="ghost"
            type="button"
            onClick={() => {
              setSearchFor(null)
              setQuery('')
              setHints([])
              setGeoHits([])
            }}
          >
            Back
          </button>
          <h2>{searchFor === 'pickup' ? 'Set pickup' : 'Set drop-off'}</h2>
          <input
            autoFocus
            placeholder="Search a place"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                void confirmTyped()
              }
            }}
          />
          {searchFor === 'pickup' ? (
            <button type="button" className="picker-item" disabled={locating} onClick={() => void useCurrentLocation()}>
              <b>{locating ? 'Getting your location…' : 'Use current location'}</b>
              <div className="muted">{locating ? 'Keep this screen open while GPS locks' : 'GPS pickup'}</div>
            </button>
          ) : null}
          {geoHits.map((item) => (
            <button
              key={`${item.details}-${item.lat}`}
              className="picker-item"
              type="button"
              onClick={() => applyStop(searchFor, item)}
            >
              <b>{item.label}</b>
              <div className="muted">{item.details}</div>
            </button>
          ))}
          {hints.map((item) => (
            <button
              key={item.place_id}
              className="picker-item"
              type="button"
              onClick={() => void choosePrediction(item)}
            >
              <b>{item.structured_formatting?.main_text ?? item.description}</b>
              <div className="muted">{item.structured_formatting?.secondary_text}</div>
            </button>
          ))}
          {error ? <p className="error">{error}</p> : null}
        </div>
      ) : null}
    </div>
  )
}
