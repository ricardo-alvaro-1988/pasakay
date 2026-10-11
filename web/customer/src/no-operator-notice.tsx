import { useEffect, useMemo, useState } from 'react'
import { api, NO_OPERATOR_EMAIL, NO_OPERATOR_FACEBOOK_URL, NO_OPERATOR_NOTICE_DELAY_MS, Stop, VehicleOffer, VehicleType } from './api'

const SCAN_LINES = [
  'Scanning nearby operators…',
  'Checking motorcycle & tricycle…',
  'Matching fares for this area…',
  'Finding what you can book…',
]

/** Lively placeholder while service-check resolves available vehicles. */
export function VehicleAvailabilityScan({ rows = 4 }: { rows?: number }) {
  const [line, setLine] = useState(0)

  useEffect(() => {
    const id = window.setInterval(() => {
      setLine((n) => (n + 1) % SCAN_LINES.length)
    }, 1400)
    return () => window.clearInterval(id)
  }, [])

  return (
    <div className="vehicle-scan" aria-live="polite" aria-busy="true">
      <div className="vehicle-scan-status">
        <span className="vehicle-scan-radar" aria-hidden>
          <span className="vehicle-scan-radar-ring" />
          <span className="vehicle-scan-radar-ring delay" />
          <span className="vehicle-scan-dot" />
        </span>
        <div className="vehicle-scan-copy">
          <b key={line} className="vehicle-scan-line">{SCAN_LINES[line]}</b>
          <small className="muted">Hang tight — this usually takes a moment.</small>
        </div>
      </div>
      <div className="vehicles vehicles-list vehicles-pick vehicle-scan-skeletons">
        {Array.from({ length: rows }, (_, i) => (
          <div key={i} className="vehicle vehicle-scan-row" style={{ animationDelay: `${i * 90}ms` }}>
            <span className="vehicle-scan-art" aria-hidden />
            <span className="vehicle-scan-text">
              <span className="vehicle-scan-bar wide" />
              <span className="vehicle-scan-bar narrow" />
            </span>
            <span className="vehicle-scan-chevron" aria-hidden />
          </div>
        ))}
      </div>
      <div className="vehicle-scan-track" aria-hidden>
        <span className="vehicle-scan-bike" />
      </div>
    </div>
  )
}

export function useNoOperatorNotice(
  pickup: Stop | null,
  dropoff: Stop | null,
  enabled: boolean,
  coverageHint = false,
) {
  const [searching, setSearching] = useState(false)
  const [uncovered, setUncovered] = useState(false)
  const [motorcycleAvailable, setMotorcycleAvailable] = useState(false)
  const [tricycleAvailable, setTricycleAvailable] = useState(false)
  const [vehicles, setVehicles] = useState<VehicleOffer[]>([])
  const [vehiclesReady, setVehiclesReady] = useState(false)
  const [municipalityHasOperator, setMunicipalityHasOperator] = useState<boolean | null>(null)

  // Availability is pickup-only — do not re-hit service-check when drop-off changes.
  useEffect(() => {
    setSearching(false)
    setUncovered(false)
    setMotorcycleAvailable(false)
    setTricycleAvailable(false)
    setVehicles([])
    setVehiclesReady(false)
    setMunicipalityHasOperator(null)
    if (!enabled || !pickup) return

    let cancelled = false

    void api.serviceCheck({
      pickupBarangayId: pickup.barangayId,
      pickupDetails: pickup.details,
      pickupLat: pickup.lat,
      pickupLng: pickup.lng,
      dropoffBarangayId: undefined,
      dropoffDetails: '',
    }).then((result) => {
      if (cancelled) return
      setMotorcycleAvailable(!!result.motorcycleAvailable)
      setTricycleAvailable(!!result.tricycleAvailable)
      const list = Array.isArray(result.vehicles) ? result.vehicles : []
      setVehicles(list)
      setVehiclesReady(true)
      setMunicipalityHasOperator(!!result.municipalityHasOperator)
    }).catch(() => {
      if (cancelled) return
      setVehiclesReady(true)
      setMunicipalityHasOperator(null)
    })

    return () => {
      cancelled = true
    }
  }, [
    enabled,
    pickup?.barangayId,
    pickup?.details,
    pickup?.lat,
    pickup?.lng,
  ])

  // Uncovered-area notice waits until drop-off is chosen (uses last pickup service-check).
  useEffect(() => {
    setSearching(false)
    setUncovered(false)
    if (!enabled || !pickup || !dropoff) return

    let cancelled = false
    let timer: number | undefined

    if (municipalityHasOperator === false || (municipalityHasOperator === null && coverageHint)) {
      setSearching(true)
      timer = window.setTimeout(() => {
        if (cancelled) return
        setSearching(false)
        setUncovered(true)
      }, NO_OPERATOR_NOTICE_DELAY_MS)
    }

    return () => {
      cancelled = true
      if (timer !== undefined) window.clearTimeout(timer)
    }
  }, [
    enabled,
    coverageHint,
    pickup?.barangayId,
    dropoff?.barangayId,
    dropoff?.details,
    municipalityHasOperator,
  ])

  // Memoize so quote effects that depend on these do not re-run every render
  // (new [] identity would keep "Getting fare…" stuck in a loop).
  const hasPickup = !!pickup
  const listedVehicles = useMemo(() => vehicles, [vehicles])
  const availableVehicles = useMemo(
    () => vehicles.filter((v) => v.available),
    [vehicles],
  )
  const useOffers = listedVehicles.length > 0
  const availableTypes = useMemo(
    () => {
      if (!hasPickup || !vehiclesReady) return []
      return useOffers
        ? availableVehicles.map((v) => v.vehicleType as VehicleType)
        : ([
            motorcycleAvailable ? 'Motorcycle' : null,
            tricycleAvailable ? 'Tricycle' : null,
          ].filter(Boolean) as VehicleType[])
    },
    [availableVehicles, hasPickup, motorcycleAvailable, tricycleAvailable, useOffers, vehiclesReady],
  )

  return {
    searching,
    uncovered,
    motorcycleAvailable,
    tricycleAvailable,
    vehicles,
    listedVehicles,
    availableVehicles,
    useOffers,
    availableTypes,
    vehiclesReady: hasPickup && vehiclesReady,
    isTypeAvailable: (type: VehicleType) => availableTypes.includes(type),
  }
}

export function NoOperatorNotice({ show }: { show: boolean }) {
  const [dismissed, setDismissed] = useState(false)

  useEffect(() => {
    if (show) setDismissed(false)
  }, [show])

  if (!show || dismissed) return null

  return (
    <div className="notice-overlay" role="dialog" aria-modal="true" aria-labelledby="no-operator-title">
      <div className="notice-card">
        <h3 id="no-operator-title">Service not available in this area</h3>
        <p>
          Ya! Pasakay does not currently have an operator or rider serving this municipality.
        </p>
        <p>
          If you would like to become an operator in your municipality, please contact us:
        </p>
        <ul>
          <li>
            Email:{' '}
            <a href={`mailto:${NO_OPERATOR_EMAIL}`}>{NO_OPERATOR_EMAIL}</a>
          </li>
          <li>
            Facebook:{' '}
            <a href={NO_OPERATOR_FACEBOOK_URL} target="_blank" rel="noopener noreferrer">
              Enova Tech Solution
            </a>
          </li>
        </ul>
        <button className="primary" type="button" onClick={() => setDismissed(true)}>Close</button>
      </div>
    </div>
  )
}
