import { useEffect, useRef, useState } from 'react'
import { api } from './api'
import type { MapPoint } from './api'
import type { Theme } from './theme'
import { CEBU, loadGoogleMaps } from './FleetMap'

type GoogleMaps = {
  Map: new (el: HTMLElement, opts: Record<string, unknown>) => GoogleMap
  LatLng: new (lat: number, lng: number) => unknown
  LatLngBounds: new () => GoogleBounds
  InfoWindow: new () => GoogleInfoWindow
  OverlayView: new () => GoogleOverlay
  Polyline: new (opts: Record<string, unknown>) => GooglePolyline
}

type GoogleMap = {
  fitBounds: (bounds: GoogleBounds, padding?: number) => void
  panTo: (p: { lat: number; lng: number }) => void
  setZoom: (z: number) => void
  setOptions: (opts: Record<string, unknown>) => void
}

type GooglePolyline = {
  setMap: (map: GoogleMap | null) => void
}

type GoogleOverlay = {
  setMap: (map: GoogleMap | null) => void
  onAdd: () => void
  draw: () => void
  onRemove: () => void
  getPanes: () => { overlayMouseTarget: HTMLElement } | null
  getProjection: () => { fromLatLngToDivPixel: (ll: unknown) => { x: number; y: number } | null } | null
}

type GoogleBounds = {
  extend: (p: { lat: number; lng: number }) => void
  isEmpty: () => boolean
}

type GoogleInfoWindow = {
  setContent: (html: string) => void
  open: (opts: { map: GoogleMap; position: { lat: number; lng: number } }) => void
  close: () => void
}

type MarkerKind = 'pickup' | 'dropoff' | 'rider' | 'sos'

type LiveMarker = MapPoint & { kind: MarkerKind }

function markerHtml(marker: LiveMarker) {
  const when = marker.atUtc ? `<br/><small>${new Date(marker.atUtc).toLocaleString('en-PH', { timeZone: 'Asia/Manila' })}</small>` : ''
  const prefix = marker.kind === 'pickup' ? 'Pickup'
    : marker.kind === 'dropoff' ? 'Drop-off'
      : marker.kind === 'sos' ? 'SOS pressed here'
        : 'Rider live'
  return `<strong>${prefix}</strong><br/>${marker.label}${when}`
}

function createMarker(
  gmaps: GoogleMaps,
  map: GoogleMap,
  marker: LiveMarker,
  onClick: () => void,
) {
  const overlay = new gmaps.OverlayView()
  let el: HTMLButtonElement | null = null
  overlay.onAdd = () => {
    el = document.createElement('button')
    el.type = 'button'
    el.className = `trip-pin ${marker.kind}${marker.kind === 'rider' ? ' live' : ''}${marker.kind === 'sos' ? ' sos-aggressive' : ''}`
    el.title = marker.label
    if (marker.kind === 'sos') {
      el.innerHTML = [
        '<span class="sos-pin-label">SOS</span>',
        '<span class="sos-pin-ring"></span>',
        '<span class="sos-pin-ring delay-1"></span>',
        '<span class="sos-pin-ring delay-2"></span>',
        '<span class="sos-pin-core" aria-hidden="true"></span>',
      ].join('')
    } else if (marker.kind === 'rider') {
      el.innerHTML = '<span class="fleet-pin-pulse"></span><span class="fleet-pin-pulse delay"></span><span class="fleet-pin-core" aria-hidden="true"></span>'
    } else {
      el.innerHTML = '<span class="fleet-pin-pulse"></span><span class="fleet-pin-pulse delay"></span><span class="trip-pin-dot" aria-hidden="true"></span>'
    }
    el.addEventListener('click', (event) => {
      event.stopPropagation()
      onClick()
    })
    overlay.getPanes()?.overlayMouseTarget.appendChild(el)
  }
  overlay.draw = () => {
    if (!el) {
      return
    }
    const point = overlay.getProjection()?.fromLatLngToDivPixel(new gmaps.LatLng(marker.lat, marker.lng))
    if (!point) {
      return
    }
    el.style.left = `${point.x}px`
    el.style.top = `${point.y}px`
  }
  overlay.onRemove = () => {
    el?.remove()
    el = null
  }
  overlay.setMap(map)
  return overlay
}

function toMarkers(points: {
  pickupLocation: MapPoint | null
  dropoffLocation: MapPoint | null
  riderLocation: MapPoint | null
  sosLocation: MapPoint | null
}): LiveMarker[] {
  const rows: LiveMarker[] = []
  if (points.pickupLocation) rows.push({ ...points.pickupLocation, kind: 'pickup' })
  if (points.dropoffLocation) rows.push({ ...points.dropoffLocation, kind: 'dropoff' })
  if (points.riderLocation) rows.push({ ...points.riderLocation, kind: 'rider' })
  if (points.sosLocation) rows.push({ ...points.sosLocation, kind: 'sos' })
  return rows
}

export default function TripLiveMap({
  pickupLocation,
  dropoffLocation,
  riderLocation,
  sosLocation,
  theme,
  live = false,
}: {
  pickupLocation: MapPoint | null
  dropoffLocation: MapPoint | null
  riderLocation: MapPoint | null
  sosLocation: MapPoint | null
  theme: Theme
  live?: boolean
}) {
  const el = useRef<HTMLDivElement>(null)
  const mapRef = useRef<GoogleMap | null>(null)
  const mapsRef = useRef<GoogleMaps | null>(null)
  const pinsRef = useRef<GoogleOverlay[]>([])
  const routeRef = useRef<GooglePolyline | null>(null)
  const infoRef = useRef<GoogleInfoWindow | null>(null)
  const fitted = useRef(false)
  const [ready, setReady] = useState(false)
  const [error, setError] = useState('')
  const markers = toMarkers({ pickupLocation, dropoffLocation, riderLocation, sosLocation })
  const markerKey = JSON.stringify(markers)

  useEffect(() => {
    let cancelled = false
    async function start() {
      try {
        const { googleMapsBrowserKey: key } = await api.mapsConfig()
        if (cancelled) {
          return
        }
        if (!key) {
          setError('Set Maps:BrowserApiKey in the API appsettings.')
          return
        }
        const gmaps = await loadGoogleMaps(key) as unknown as GoogleMaps
        if (cancelled || !el.current) {
          return
        }
        mapsRef.current = gmaps
        mapRef.current = new gmaps.Map(el.current, {
          center: CEBU,
          zoom: 13,
          mapTypeControl: false,
          streetViewControl: false,
          fullscreenControl: true,
          colorScheme: theme === 'dark' ? 'DARK' : 'LIGHT',
        })
        infoRef.current = new gmaps.InfoWindow()
        setError('')
        setReady(true)
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Google Maps failed to load.')
        }
      }
    }
    void start()
    return () => {
      cancelled = true
      routeRef.current?.setMap(null)
      routeRef.current = null
      for (const pin of pinsRef.current) {
        pin.setMap(null)
      }
      pinsRef.current = []
      mapRef.current = null
      mapsRef.current = null
      fitted.current = false
    }
  }, [])

  useEffect(() => {
    mapRef.current?.setOptions({ colorScheme: theme === 'dark' ? 'DARK' : 'LIGHT' })
  }, [theme])

  useEffect(() => {
    const gmaps = mapsRef.current
    const map = mapRef.current
    if (!gmaps || !map) {
      return
    }

    routeRef.current?.setMap(null)
    routeRef.current = null
    for (const pin of pinsRef.current) {
      pin.setMap(null)
    }
    pinsRef.current = []

    if (markers.length === 0) {
      map.panTo(CEBU)
      map.setZoom(13)
      return
    }

    const bounds = new gmaps.LatLngBounds()
    const route: { lat: number; lng: number }[] = []
    for (const marker of markers) {
      const position = { lat: marker.lat, lng: marker.lng }
      bounds.extend(position)
      if (marker.kind === 'pickup' || marker.kind === 'dropoff') {
        route.push(position)
      }
      const overlay = createMarker(gmaps, map, marker, () => {
        infoRef.current?.setContent(markerHtml(marker))
        infoRef.current?.open({ map, position })
      })
      pinsRef.current.push(overlay)
    }

    if (route.length >= 2) {
      routeRef.current = new gmaps.Polyline({
        path: route,
        geodesic: true,
        strokeColor: '#64748b',
        strokeOpacity: 0.85,
        strokeWeight: 3,
        map,
      })
    }

    if (sosLocation) {
      const position = { lat: sosLocation.lat, lng: sosLocation.lng }
      map.panTo(position)
      map.setZoom(15)
      infoRef.current?.setContent(markerHtml({ ...sosLocation, kind: 'sos' }))
      infoRef.current?.open({ map, position })
      return
    }

    if (!fitted.current && !bounds.isEmpty()) {
      map.fitBounds(bounds, 72)
      fitted.current = true
    }
  }, [markerKey, ready, live])

  if (error) {
    return (
      <div className="fleet-map-missing">
        <p>Map unavailable</p>
        <p>{error}</p>
      </div>
    )
  }

  return (
    <div className="trip-live-map">
      <div className="trip-map-legend">
        {pickupLocation ? <span className="trip-legend pickup">Pickup</span> : null}
        {dropoffLocation ? <span className="trip-legend dropoff">Drop-off</span> : null}
        {riderLocation ? <span className="trip-legend rider">Rider live</span> : null}
        {sosLocation ? <span className="trip-legend sos blink">SOS</span> : null}
        {live ? <span className="trip-legend live-tag">Live</span> : null}
      </div>
      <div ref={el} className="fleet-map-canvas" />
    </div>
  )
}
