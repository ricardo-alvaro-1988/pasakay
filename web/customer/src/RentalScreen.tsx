import { FormEvent, useEffect, useMemo, useState } from 'react'
import {
  api,
  PLATFORM_VEHICLE_TYPES,
  VehicleType,
} from './api'
import {
  geocodeText,
  loadGoogleMaps,
  placeDetails,
  Prediction,
  reverseGeocode,
  searchPlaces,
  StopResult,
} from './maps'
import { lastKnownGps, readPickupGps } from './gps'
import { vehicleArt, vehicleLabel, vehicleMaxPassengers } from './vehicle-art'

type Mode = 'chooser' | 'rent' | 'list' | 'done'
type DayKey = 'mon' | 'tue' | 'wed' | 'thu' | 'fri' | 'sat' | 'sun'

const DAYS: { key: DayKey; label: string; form: string }[] = [
  { key: 'mon', label: 'Mon', form: 'availableMonday' },
  { key: 'tue', label: 'Tue', form: 'availableTuesday' },
  { key: 'wed', label: 'Wed', form: 'availableWednesday' },
  { key: 'thu', label: 'Thu', form: 'availableThursday' },
  { key: 'fri', label: 'Fri', form: 'availableFriday' },
  { key: 'sat', label: 'Sat', form: 'availableSaturday' },
  { key: 'sun', label: 'Sun', form: 'availableSunday' },
]

function tomorrowLocalIso() {
  const d = new Date()
  d.setHours(0, 0, 0, 0)
  d.setDate(d.getDate() + 1)
  return d.toISOString().slice(0, 10)
}

function VehiclePicker({
  value,
  onChange,
  filter,
}: {
  value: VehicleType
  onChange: (type: VehicleType) => void
  filter: string
}) {
  const q = filter.trim().toLowerCase()
  const options = PLATFORM_VEHICLE_TYPES.filter((type) => {
    if (!q) return true
    return vehicleLabel(type).toLowerCase().includes(q) || type.toLowerCase().includes(q)
  })
  return (
    <div className="rental-vehicles">
      {options.map((type) => (
        <button
          key={type}
          type="button"
          className={value === type ? 'on' : ''}
          onClick={() => onChange(type)}
        >
          <span className="icon">
            <img src={vehicleArt(type)} alt="" />
          </span>
          <span>{vehicleLabel(type)}</span>
        </button>
      ))}
      {options.length === 0 ? <p className="muted">No vehicle matches.</p> : null}
    </div>
  )
}

function LocationPicker({
  location,
  onLocation,
}: {
  location: StopResult | null
  onLocation: (stop: StopResult | null) => void
}) {
  const [query, setQuery] = useState('')
  const [hints, setHints] = useState<Prediction[]>([])
  const [geoHits, setGeoHits] = useState<StopResult[]>([])
  const [locating, setLocating] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (query.trim().length < 2) {
      setHints([])
      setGeoHits([])
      return
    }
    let cancelled = false
    const handle = window.setTimeout(() => {
      void (async () => {
        try {
          const maps = await loadGoogleMaps((await api.mapsConfig()).googleMapsBrowserKey)
          const [places, geos] = await Promise.all([
            searchPlaces(maps, query.trim()),
            geocodeText(maps, query.trim()),
          ])
          if (cancelled) return
          setHints(places)
          setGeoHits(geos)
        } catch {
          if (!cancelled) setError('Could not search places.')
        }
      })()
    }, 280)
    return () => {
      cancelled = true
      window.clearTimeout(handle)
    }
  }, [query])

  async function useCurrent() {
    setLocating(true)
    setError('')
    try {
      const maps = await loadGoogleMaps((await api.mapsConfig()).googleMapsBrowserKey)
      let lat: number
      let lng: number
      try {
        const pos = await readPickupGps()
        lat = pos.coords.latitude
        lng = pos.coords.longitude
      } catch {
        const cached = lastKnownGps(600_000)
        if (!cached) throw new Error('Could not get GPS. Allow location and try again.')
        lat = cached.lat
        lng = cached.lng
      }
      const details = await reverseGeocode(maps, lat, lng)
      onLocation({
        label: details.split(',')[0]?.trim() || 'Current location',
        details,
        lat,
        lng,
      })
      setQuery('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not use current location.')
    } finally {
      setLocating(false)
    }
  }

  return (
    <div className="rental-location">
      {location ? (
        <div className="rental-loc-card">
          <strong>{location.label}</strong>
          <div className="muted">{location.details}</div>
          <button type="button" className="ghost" onClick={() => onLocation(null)}>
            Change
          </button>
        </div>
      ) : null}
      {!location ? (
        <>
          <button type="button" className="picker-item" disabled={locating} onClick={() => void useCurrent()}>
            <b>{locating ? 'Getting location…' : 'Use current location'}</b>
            <div className="muted">GPS pin</div>
          </button>
          <input
            placeholder="Search a place"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
          {geoHits.map((item) => (
            <button key={`${item.details}-${item.lat}`} className="picker-item" type="button" onClick={() => onLocation(item)}>
              <b>{item.label}</b>
              <div className="muted">{item.details}</div>
            </button>
          ))}
          {hints.map((item) => (
            <button
              key={item.place_id}
              className="picker-item"
              type="button"
              onClick={() => {
                void (async () => {
                  try {
                    const maps = await loadGoogleMaps((await api.mapsConfig()).googleMapsBrowserKey)
                    const stop = await placeDetails(maps, item.place_id)
                    onLocation({
                      label: stop.address.split(',')[0]?.trim() || item.description,
                      details: stop.address,
                      lat: stop.lat,
                      lng: stop.lng,
                    })
                  } catch {
                    setError('Could not load that place.')
                  }
                })()
              }}
            >
              <b>{item.structured_formatting?.main_text ?? item.description}</b>
              <div className="muted">{item.structured_formatting?.secondary_text}</div>
            </button>
          ))}
        </>
      ) : null}
      {error ? <p className="error">{error}</p> : null}
    </div>
  )
}

export function RentalScreen({
  deskMobile,
  mapLat,
  mapLng,
}: {
  deskMobile?: string | null
  mapLat?: number | null
  mapLng?: number | null
}) {
  const [mode, setMode] = useState<Mode>('chooser')
  const [vehicleFilter, setVehicleFilter] = useState('')
  const [vehicle, setVehicle] = useState<VehicleType>('Motorcycle')
  const [from, setFrom] = useState(tomorrowLocalIso())
  const [to, setTo] = useState(tomorrowLocalIso())
  const [location, setLocation] = useState<StopResult | null>(null)
  const [notes, setNotes] = useState('')
  const [mobile, setMobile] = useState(deskMobile || '')
  const [plate, setPlate] = useState('')
  const [seater, setSeater] = useState(1)
  const [days, setDays] = useState<Record<DayKey, boolean>>({
    mon: true,
    tue: true,
    wed: true,
    thu: true,
    fri: true,
    sat: true,
    sun: true,
  })
  const [front, setFront] = useState<File | null>(null)
  const [back, setBack] = useState<File | null>(null)
  const [left, setLeft] = useState<File | null>(null)
  const [right, setRight] = useState<File | null>(null)
  const [inside, setInside] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [doneMessage, setDoneMessage] = useState('')

  const maxSeats = vehicleMaxPassengers(vehicle)
  const minDate = tomorrowLocalIso()

  useEffect(() => {
    setSeater((n) => Math.min(Math.max(1, n), maxSeats))
  }, [maxSeats])

  useEffect(() => {
    if (location || mapLat == null || mapLng == null) return
    let cancelled = false
    void (async () => {
      try {
        const maps = await loadGoogleMaps((await api.mapsConfig()).googleMapsBrowserKey)
        const details = await reverseGeocode(maps, mapLat, mapLng)
        if (cancelled) return
        setLocation({
          label: details.split(',')[0]?.trim() || 'Nearby',
          details,
          lat: mapLat,
          lng: mapLng,
        })
      } catch {
        /* optional prefill */
      }
    })()
    return () => {
      cancelled = true
    }
  }, [mapLat, mapLng, location])

  const weekdayHint = useMemo(() => {
    const on = DAYS.filter((d) => days[d.key]).map((d) => d.label)
    if (on.length === 7) return 'Available every day'
    if (on.length === 0) return 'Pick at least one day'
    if (days.mon && days.tue && days.wed && days.thu && days.fri && !days.sat && !days.sun) {
      return 'Weekdays only'
    }
    if (!days.mon && !days.tue && !days.wed && !days.thu && !days.fri && days.sat && days.sun) {
      return 'Weekends only'
    }
    return on.join(' · ')
  }, [days])

  function resetForms() {
    setError('')
    setVehicleFilter('')
    setVehicle('Motorcycle')
    setFrom(minDate)
    setTo(minDate)
    setNotes('')
    setMobile(deskMobile || '')
    setPlate('')
    setSeater(1)
    setDays({ mon: true, tue: true, wed: true, thu: true, fri: true, sat: true, sun: true })
    setFront(null)
    setBack(null)
    setLeft(null)
    setRight(null)
    setInside(null)
  }

  async function submitRent(e: FormEvent) {
    e.preventDefault()
    if (!location?.details || !location.lat || !location.lng) {
      setError('Set a location on the map search.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const data = new FormData()
      data.append('vehicleType', vehicle)
      data.append('scheduleFrom', from)
      data.append('scheduleTo', to)
      data.append('locationDetails', location.details)
      data.append('locationLat', String(location.lat))
      data.append('locationLng', String(location.lng))
      if (notes.trim()) data.append('notes', notes.trim())
      data.append('mobileNumber', mobile.trim())
      const res = await api.rentalInquire(data)
      setDoneMessage(res.message)
      setMode('done')
      resetForms()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not submit.')
    } finally {
      setBusy(false)
    }
  }

  async function submitList(e: FormEvent) {
    e.preventDefault()
    if (!front || !back || !left || !right || !inside) {
      setError('Upload all 5 photos.')
      return
    }
    setBusy(true)
    setError('')
    try {
      let pin = location
      if (!pin) {
        try {
          const maps = await loadGoogleMaps((await api.mapsConfig()).googleMapsBrowserKey)
          const pos = await readPickupGps()
          const details = await reverseGeocode(maps, pos.coords.latitude, pos.coords.longitude)
          pin = {
            label: details.split(',')[0]?.trim() || 'Current location',
            details,
            lat: pos.coords.latitude,
            lng: pos.coords.longitude,
          }
        } catch {
          if (mapLat != null && mapLng != null) {
            pin = { label: 'Nearby', details: 'Nearby', lat: mapLat, lng: mapLng }
          }
        }
      }
      if (!pin) {
        throw new Error('Turn on location so we can find your local operator.')
      }

      const data = new FormData()
      data.append('vehicleType', vehicle)
      data.append('plate', plate.trim())
      data.append('seater', String(seater))
      data.append('front', front)
      data.append('back', back)
      data.append('left', left)
      data.append('right', right)
      data.append('inside', inside)
      for (const day of DAYS) data.append(day.form, String(days[day.key]))
      data.append('locationDetails', pin.details)
      data.append('locationLat', String(pin.lat))
      data.append('locationLng', String(pin.lng))
      const res = await api.rentalListCar(data)
      setDoneMessage(res.message)
      setMode('done')
      resetForms()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not submit.')
    } finally {
      setBusy(false)
    }
  }

  if (mode === 'chooser') {
    return (
      <section className="panel page-panel rental-panel">
        <h2>Rental</h2>
        <p className="muted">Rent a vehicle or list yours with the local operator.</p>
        <div className="rental-chooser">
          <button type="button" className="rental-choice" onClick={() => { resetForms(); setMode('rent') }}>
            <strong>Rental Car</strong>
            <span className="muted">Request a vehicle for your schedule</span>
          </button>
          <button type="button" className="rental-choice" onClick={() => { resetForms(); setMode('list') }}>
            <strong>List Your Car</strong>
            <span className="muted">Offer your vehicle for rent</span>
          </button>
        </div>
      </section>
    )
  }

  if (mode === 'done') {
    return (
      <section className="panel page-panel rental-panel">
        <h2>Thank you</h2>
        <p className="rental-done">{doneMessage || 'We will come back to you soonest.'}</p>
        <button type="button" className="btn" onClick={() => setMode('chooser')}>
          Back to Rental
        </button>
      </section>
    )
  }

  if (mode === 'rent') {
    return (
      <section className="panel page-panel rental-panel">
        <button type="button" className="ghost" onClick={() => setMode('chooser')}>← Back</button>
        <h2>Rental Car</h2>
        <form className="rental-form" onSubmit={(e) => void submitRent(e)}>
          <label className="field">
            <span>Find vehicle type</span>
            <input value={vehicleFilter} onChange={(e) => setVehicleFilter(e.target.value)} placeholder="Motorcycle, Tricycle…" />
          </label>
          <VehiclePicker value={vehicle} onChange={setVehicle} filter={vehicleFilter} />
          <div className="rental-dates">
            <label className="field">
              <span>From</span>
              <input type="date" min={minDate} value={from} onChange={(e) => {
                setFrom(e.target.value)
                if (to < e.target.value) setTo(e.target.value)
              }} required />
            </label>
            <label className="field">
              <span>To</span>
              <input type="date" min={from || minDate} value={to} onChange={(e) => setTo(e.target.value)} required />
            </label>
          </div>
          <p className="muted rental-hint">Schedule must start at least 1 day from today.</p>
          <label className="field">
            <span>Location</span>
          </label>
          <LocationPicker location={location} onLocation={setLocation} />
          <label className="field">
            <span>Notes</span>
            <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={3} maxLength={1000} placeholder="Optional details" />
          </label>
          <label className="field">
            <span>Mobile number</span>
            <input value={mobile} onChange={(e) => setMobile(e.target.value)} required inputMode="tel" placeholder="09xxxxxxxxx" />
          </label>
          {error ? <p className="error">{error}</p> : null}
          <button className="btn" type="submit" disabled={busy}>{busy ? 'Submitting…' : 'Submit'}</button>
        </form>
      </section>
    )
  }

  return (
    <section className="panel page-panel rental-panel">
      <button type="button" className="ghost" onClick={() => setMode('chooser')}>← Back</button>
      <h2>List Your Car</h2>
      <form className="rental-form" onSubmit={(e) => void submitList(e)}>
        <label className="field">
          <span>Find vehicle type</span>
          <input value={vehicleFilter} onChange={(e) => setVehicleFilter(e.target.value)} placeholder="Motorcycle, Tricycle…" />
        </label>
        <VehiclePicker value={vehicle} onChange={setVehicle} filter={vehicleFilter} />
        <label className="field">
          <span>Plate</span>
          <input value={plate} onChange={(e) => setPlate(e.target.value)} required maxLength={40} placeholder="ABC 1234" />
        </label>
        <label className="field">
          <span>Seater</span>
          <input
            type="number"
            min={1}
            max={maxSeats}
            value={seater}
            onChange={(e) => setSeater(Number(e.target.value) || 1)}
            required
          />
          <small className="muted">Max {maxSeats} for {vehicleLabel(vehicle)}</small>
        </label>
        <div className="rental-photos">
          <p>Images (Front / Back / Left / Right / Inside)</p>
          {(
            [
              ['Front', front, setFront],
              ['Back', back, setBack],
              ['Left', left, setLeft],
              ['Right', right, setRight],
              ['Inside', inside, setInside],
            ] as const
          ).map(([label, file, setFile]) => (
            <label key={label} className="field">
              <span>{label}</span>
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp"
                required
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
              {file ? (
                <img className="rental-thumb" src={URL.createObjectURL(file)} alt="" />
              ) : null}
            </label>
          ))}
        </div>
        <div className="rental-days">
          <p>Availability</p>
          <p className="muted rental-hint">{weekdayHint}. Tap days to toggle.</p>
          <div className="chips">
            {DAYS.map((day) => (
              <button
                key={day.key}
                type="button"
                className={days[day.key] ? 'on' : ''}
                onClick={() => setDays((prev) => ({ ...prev, [day.key]: !prev[day.key] }))}
              >
                {day.label}
              </button>
            ))}
          </div>
          <div className="chips" style={{ marginTop: 8 }}>
            <button type="button" className="ghost" onClick={() => setDays({ mon: true, tue: true, wed: true, thu: true, fri: true, sat: false, sun: false })}>
              Weekdays
            </button>
            <button type="button" className="ghost" onClick={() => setDays({ mon: false, tue: false, wed: false, thu: false, fri: false, sat: true, sun: true })}>
              Weekends
            </button>
            <button type="button" className="ghost" onClick={() => setDays({ mon: true, tue: true, wed: true, thu: true, fri: true, sat: true, sun: true })}>
              Every day
            </button>
          </div>
        </div>
        {error ? <p className="error">{error}</p> : null}
        <button className="btn" type="submit" disabled={busy}>{busy ? 'Submitting…' : 'Submit'}</button>
      </form>
    </section>
  )
}