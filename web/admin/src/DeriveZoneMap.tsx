import { FormEvent, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { loadGoogleMaps } from './FleetMap'

export type DeriveMapPoint = { lat: number; lng: number }

type Props = {
  points: DeriveMapPoint[]
  onChange: (points: DeriveMapPoint[]) => void
  height?: number
}

type GMaps = {
  Map: new (el: HTMLElement, opts: Record<string, unknown>) => GMap
  Marker: new (opts: Record<string, unknown>) => GMarker
  Polygon: new (opts: Record<string, unknown>) => GPolygon
  LatLngBounds: new () => GBounds
  Geocoder: new () => GGeocoder
  event: {
    addListener: (
      target: unknown,
      name: string,
      handler: (...args: never[]) => void,
    ) => void
    trigger: (target: unknown, name: string) => void
  }
  places?: {
    Autocomplete: new (
      input: HTMLInputElement,
      opts?: Record<string, unknown>,
    ) => GAutocomplete
  }
}

type GMap = {
  setCenter: (p: DeriveMapPoint) => void
  setZoom: (z: number) => void
  fitBounds: (bounds: GBounds, padding?: number) => void
  getBounds: () => GBounds | undefined
}

type GMarker = { setMap: (map: GMap | null) => void; setPosition?: (p: DeriveMapPoint) => void }
type GPolygon = { setMap: (map: GMap | null) => void }
type GBounds = {
  extend: (p: DeriveMapPoint) => void
  getNorthEast?: () => { lat: () => number; lng: () => number }
  getSouthWest?: () => { lat: () => number; lng: () => number }
}

type GLatLng = { lat: () => number; lng: () => number }
type GGeocoder = {
  geocode: (
    req: Record<string, unknown>,
    cb: (results: Array<{ geometry?: { location?: GLatLng } }> | null, status: string) => void,
  ) => void
}
type GAutocomplete = {
  addListener: (name: string, handler: () => void) => void
  getPlace: () => {
    geometry?: { location?: GLatLng; viewport?: GBounds }
    formatted_address?: string
    name?: string
  }
}

const DEFAULT_CENTER = { lat: 13.4115, lng: 121.1803 }

export function DeriveZoneMap({ points, onChange, height = 360 }: Props) {
  const host = useRef<HTMLDivElement>(null)
  const searchRef = useRef<HTMLInputElement>(null)
  const mapRef = useRef<GMap | null>(null)
  const gmapsRef = useRef<GMaps | null>(null)
  const polygonRef = useRef<GPolygon | null>(null)
  const markersRef = useRef<GMarker[]>([])
  const searchMarkerRef = useRef<GMarker | null>(null)
  const pointsRef = useRef(points)
  pointsRef.current = points
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange
  const [searchError, setSearchError] = useState('')
  const [searchBusy, setSearchBusy] = useState(false)
  const [fullscreen, setFullscreen] = useState(false)

  function goToLocation(gmaps: GMaps, map: GMap, lat: number, lng: number, viewport?: GBounds) {
    const target = { lat, lng }
    if (viewport?.getNorthEast && viewport?.getSouthWest) {
      map.fitBounds(viewport, 48)
    } else {
      map.setCenter(target)
      map.setZoom(16)
    }
    searchMarkerRef.current?.setMap(null)
    searchMarkerRef.current = new gmaps.Marker({
      map,
      position: target,
      title: 'Search result',
      opacity: 0.85,
    })
  }

  function geocodeQuery(gmaps: GMaps, map: GMap, text: string) {
    const trimmed = text.trim()
    if (!trimmed) {
      setSearchError('Enter a place or address to search.')
      return
    }
    setSearchBusy(true)
    setSearchError('')
    const geocoder = new gmaps.Geocoder()
    const bounds = map.getBounds?.()
    geocoder.geocode(
      {
        address: trimmed,
        componentRestrictions: { country: 'PH' },
        ...(bounds ? { bounds } : {}),
      },
      (results, status) => {
        setSearchBusy(false)
        if (status !== 'OK' || !results?.[0]?.geometry?.location) {
          setSearchError('No matching place found. Try a clearer address.')
          return
        }
        const loc = results[0].geometry.location
        const viewport = (results[0].geometry as { viewport?: GBounds }).viewport
        goToLocation(gmaps, map, loc.lat(), loc.lng(), viewport)
      },
    )
  }

  useEffect(() => {
    let cancelled = false
    async function boot() {
      if (!host.current) return
      const { googleMapsBrowserKey: key } = await api.mapsConfig()
      if (!key || cancelled) return
      const gmaps = await loadGoogleMaps(key) as unknown as GMaps
      if (cancelled || !host.current) return
      gmapsRef.current = gmaps
      const map = new gmaps.Map(host.current, {
        center: pointsRef.current[0] ?? DEFAULT_CENTER,
        zoom: pointsRef.current.length > 0 ? 14 : 12,
        mapTypeControl: false,
        streetViewControl: false,
        fullscreenControl: false,
      })
      mapRef.current = map
      gmaps.event.addListener(map, 'click', ((e: { latLng?: GLatLng }) => {
        if (!e.latLng) return
        const next = [...pointsRef.current, { lat: e.latLng.lat(), lng: e.latLng.lng() }]
        onChangeRef.current(next)
      }) as (...args: never[]) => void)
      draw(gmaps, map, pointsRef.current)

      const input = searchRef.current
      if (input && gmaps.places?.Autocomplete) {
        const autocomplete = new gmaps.places.Autocomplete(input, {
          fields: ['geometry', 'name', 'formatted_address'],
          componentRestrictions: { country: 'ph' },
        })
        autocomplete.addListener('place_changed', () => {
          const place = autocomplete.getPlace()
          const loc = place.geometry?.location
          if (!loc) {
            setSearchError('Could not locate that place.')
            return
          }
          setSearchError('')
          goToLocation(gmaps, map, loc.lat(), loc.lng(), place.geometry?.viewport)
        })
      }
    }
    void boot().catch(() => {})
    return () => {
      cancelled = true
      markersRef.current.forEach((m) => m.setMap(null))
      markersRef.current = []
      searchMarkerRef.current?.setMap(null)
      searchMarkerRef.current = null
      polygonRef.current?.setMap(null)
      polygonRef.current = null
      mapRef.current = null
      gmapsRef.current = null
    }
  }, [])

  useEffect(() => {
    const map = mapRef.current
    const gmaps = gmapsRef.current
    if (!map || !gmaps) return
    draw(gmaps, map, points)
    if (points.length >= 1) {
      const bounds = new gmaps.LatLngBounds()
      points.forEach((p) => bounds.extend(p))
      if (points.length === 1) {
        map.setCenter(points[0])
        map.setZoom(15)
      } else {
        map.fitBounds(bounds, 48)
      }
    }
  }, [points])

  useEffect(() => {
    const previous = document.body.style.overflow
    if (fullscreen) {
      document.body.style.overflow = 'hidden'
    }
    const map = mapRef.current
    const gmaps = gmapsRef.current
    const timer = window.setTimeout(() => {
      if (!map || !gmaps) return
      gmaps.event.trigger(map, 'resize')
      if (pointsRef.current.length >= 2) {
        const bounds = new gmaps.LatLngBounds()
        pointsRef.current.forEach((p) => bounds.extend(p))
        map.fitBounds(bounds, 48)
      }
    }, 80)
    return () => {
      document.body.style.overflow = previous
      window.clearTimeout(timer)
    }
  }, [fullscreen])

  useEffect(() => {
    if (!fullscreen) return
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') setFullscreen(false)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [fullscreen])

  function draw(gmaps: GMaps, map: GMap, ring: DeriveMapPoint[]) {
    markersRef.current.forEach((m) => m.setMap(null))
    markersRef.current = ring.map((p, index) => new gmaps.Marker({
      map,
      position: p,
      label: { text: String(index + 1), color: '#fff', fontSize: '11px', fontWeight: '700' },
    }))
    polygonRef.current?.setMap(null)
    polygonRef.current = null
    if (ring.length >= 3) {
      polygonRef.current = new gmaps.Polygon({
        map,
        paths: ring,
        strokeColor: '#047857',
        strokeOpacity: 0.95,
        strokeWeight: 2,
        fillColor: '#10b981',
        fillOpacity: 0.28,
        clickable: false,
      })
    }
  }

  function onSearchSubmit(e: FormEvent) {
    e.preventDefault()
    const map = mapRef.current
    const gmaps = gmapsRef.current
    if (!map || !gmaps) {
      setSearchError('Map is still loading.')
      return
    }
    geocodeQuery(gmaps, map, searchRef.current?.value ?? '')
  }

  return (
    <div className={fullscreen ? 'derive-map-shell is-fullscreen' : 'derive-map-shell'}>
      <div className="derive-map-toolbar">
        <form
          onSubmit={onSearchSubmit}
          style={{ display: 'flex', gap: 8, flex: 1, flexWrap: 'wrap', alignItems: 'center', minWidth: 0 }}
        >
          <input
            ref={searchRef}
            type="search"
            defaultValue=""
            onChange={() => {
              if (searchError) setSearchError('')
            }}
            placeholder="Search place or address (e.g. Port of Calapan)"
            style={{
              flex: 1,
              minWidth: 220,
              margin: 0,
              padding: '10px 12px',
              borderRadius: 10,
              border: '1px solid var(--line)',
              background: 'var(--panel, #fff)',
              color: 'inherit',
              font: 'inherit',
            }}
            aria-label="Search map location"
          />
          <button className="btn tiny" type="submit" disabled={searchBusy}>
            {searchBusy ? 'Searching…' : 'Go'}
          </button>
        </form>
        <button
          className="btn tiny"
          type="button"
          onClick={() => setFullscreen((v) => !v)}
          aria-pressed={fullscreen}
        >
          {fullscreen ? 'Exit full screen' : 'Full screen'}
        </button>
      </div>
      {searchError ? <p className="error" style={{ marginTop: 0 }}>{searchError}</p> : null}
      <div
        ref={host}
        className="derive-map-host"
        style={fullscreen ? undefined : { height }}
      />
      <div style={{ display: 'flex', gap: 8, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
        <p className="muted" style={{ margin: 0, flex: 1 }}>
          Search to jump to an area, then click the map to add polygon points ({points.length} point{points.length === 1 ? '' : 's'}). Need at least 3.
          {fullscreen ? ' Press Esc to exit full screen.' : ''}
        </p>
        <button className="btn tiny" type="button" disabled={points.length === 0} onClick={() => onChange(points.slice(0, -1))}>
          Undo point
        </button>
        <button className="btn tiny danger" type="button" disabled={points.length === 0} onClick={() => onChange([])}>
          Clear polygon
        </button>
      </div>
    </div>
  )
}
