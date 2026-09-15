import { useEffect, useMemo, useState } from 'react'
import { api, NO_OPERATOR_EMAIL, NO_OPERATOR_FACEBOOK_URL, NO_OPERATOR_NOTICE_DELAY_MS, Stop, VehicleOffer, VehicleType } from './api'

export function useNoOperatorNotice(
  pickup: Stop | null,
  dropoff: Stop | null,
  enabled: boolean,
  coverageHint = false,
) {
  const [searching, setSearching] = useState(false)
  const [uncovered, setUncovered] = useState(false)
  const [motorcycleAvailable, setMotorcycleAvailable] = useState(true)
  const [tricycleAvailable, setTricycleAvailable] = useState(true)
  const [vehicles, setVehicles] = useState<VehicleOffer[]>([])

  useEffect(() => {
    setSearching(false)
    setUncovered(false)
    setMotorcycleAvailable(true)
    setTricycleAvailable(true)
    setVehicles([])
    // Load offered vehicles from pickup municipality (do not wait for drop-off).
    if (!enabled || !pickup) return

    let cancelled = false
    let timer: number | undefined

    function startWait() {
      setSearching(true)
      timer = window.setTimeout(() => {
        if (cancelled) return
        setSearching(false)
        setUncovered(true)
      }, NO_OPERATOR_NOTICE_DELAY_MS)
    }

    void api.serviceCheck({
      pickupBarangayId: pickup.barangayId,
      pickupDetails: pickup.details,
      pickupLat: pickup.lat,
      pickupLng: pickup.lng,
      dropoffBarangayId: dropoff?.barangayId,
      dropoffDetails: dropoff?.details ?? '',
    }).then((result) => {
      if (cancelled) return
      setMotorcycleAvailable(!!result.motorcycleAvailable)
      setTricycleAvailable(!!result.tricycleAvailable)
      const list = Array.isArray(result.vehicles) ? result.vehicles : []
      setVehicles(list)
      // Only warn about uncovered area once both stops are chosen.
      if (dropoff && !result.municipalityHasOperator) startWait()
    }).catch(() => {
      if (cancelled) return
      if (dropoff && coverageHint) startWait()
    })

    return () => {
      cancelled = true
      if (timer !== undefined) window.clearTimeout(timer)
    }
  }, [
    enabled,
    coverageHint,
    pickup?.barangayId,
    pickup?.details,
    pickup?.lat,
    pickup?.lng,
    dropoff?.barangayId,
    dropoff?.details,
    dropoff?.lat,
    dropoff?.lng,
  ])

  // Memoize so quote effects that depend on these do not re-run every render
  // (new [] identity would keep "Getting fare…" stuck in a loop).
  const listedVehicles = useMemo(() => vehicles, [vehicles])
  const availableVehicles = useMemo(
    () => vehicles.filter((v) => v.available),
    [vehicles],
  )
  const useOffers = listedVehicles.length > 0
  const availableTypes = useMemo(
    () => (useOffers
      ? availableVehicles.map((v) => v.vehicleType as VehicleType)
      : ([
          motorcycleAvailable ? 'Motorcycle' : null,
          tricycleAvailable ? 'Tricycle' : null,
        ].filter(Boolean) as VehicleType[])),
    [availableVehicles, motorcycleAvailable, tricycleAvailable, useOffers],
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
