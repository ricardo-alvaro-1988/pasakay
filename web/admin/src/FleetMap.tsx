import { useEffect, useRef, useState } from 'react'
import { api, FleetRider, fleetDuty, fleetDutyLabel } from './api'
import type { Theme } from './theme'

const CEBU = { lat: 10.3157, lng: 123.8854 }

export { CEBU }

type GoogleMaps = {
  Map: new (el: HTMLElement, opts: Record<string, unknown>) => GoogleMap
  LatLng: new (lat: number, lng: number) => unknown
  LatLngBounds: new () => GoogleBounds
  InfoWindow: new () => GoogleInfoWindow
  OverlayView: new () => GoogleOverlay
  event: { clearInstanceListeners: (target: unknown) => void }
}

type GoogleMap = {
  fitBounds: (bounds: GoogleBounds, padding?: number) => void
  panTo: (p: { lat: number; lng: number }) => void
  setZoom: (z: number) => void
  setOptions: (opts: Record<string, unknown>) => void
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

declare global {
  interface Window {
    google?: { maps: GoogleMaps }
  }
}

let mapsLoader: Promise<GoogleMaps> | null = null

function loadGoogleMaps(key: string) {
  if (window.google?.maps) {
    return Promise.resolve(window.google.maps)
  }
  if (mapsLoader) {
    return mapsLoader
  }
  mapsLoader = new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>('script[data-yp-google-maps]')
    if (existing) {
      existing.addEventListener('load', () => {
        if (window.google?.maps) resolve(window.google.maps)
        else reject(new Error('Google Maps did not initialize.'))
      })
      existing.addEventListener('error', () => reject(new Error('Google Maps failed to load.')))
      return
    }
    const script = document.createElement('script')
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}`
    script.async = true
    script.defer = true
    script.dataset.ypGoogleMaps = '1'
    script.onload = () => {
      if (window.google?.maps) resolve(window.google.maps)
      else reject(new Error('Google Maps did not initialize.'))
    }
    script.onerror = () => reject(new Error('Google Maps failed to load.'))
    document.head.appendChild(script)
  })
  return mapsLoader
}

export { loadGoogleMaps }

function isStale(rider: FleetRider) {
  return Date.now() - new Date(rider.lastLocationAtUtc).getTime() > 15 * 60 * 1000
}

function riderDuty(rider: FleetRider) {
  return fleetDuty(rider.status, rider.isOnline, rider.lastLocationAtUtc)
}

function riderHtml(rider: FleetRider) {
  const duty = riderDuty(rider)
  const booking = rider.bookingReference ? `<br/>${rider.bookingReference}` : ''
  return `<strong>${rider.fullName}</strong><br/>${fleetDutyLabel(duty)} · ${rider.vehicleType} · ${rider.plateNumber}<br/>${rider.phoneNumber}${booking}`
}

function pinClass(rider: FleetRider, focused: boolean) {
  const duty = riderDuty(rider)
  const stale = duty === 'offline' || isStale(rider)
  return `fleet-pin ${duty}${stale ? ' stale' : ''}${focused ? ' focused' : ''}`
}

function createPin(
  gmaps: GoogleMaps,
  map: GoogleMap,
  rider: FleetRider,
  focused: boolean,
  onClick: () => void,
) {
  const overlay = new gmaps.OverlayView()
  let el: HTMLButtonElement | null = null
  overlay.onAdd = () => {
    el = document.createElement('button')
    el.type = 'button'
    el.className = pinClass(rider, focused)
    el.title = `${rider.fullName} · ${fleetDutyLabel(riderDuty(rider))}`
    el.innerHTML = '<span class="fleet-pin-pulse"></span><span class="fleet-pin-pulse delay"></span><span class="fleet-pin-core" aria-hidden="true"></span>'
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
    const point = overlay.getProjection()?.fromLatLngToDivPixel(new gmaps.LatLng(rider.lat, rider.lng))
    if (!point) {
      return
    }
    el.style.left = `${point.x}px`
    el.style.top = `${point.y}px`
    el.className = pinClass(rider, focused)
  }
  overlay.onRemove = () => {
    el?.remove()
    el = null
  }
  overlay.setMap(map)
  return overlay
}

export default function FleetMap({
  riders,
  focusId,
  theme,
}: {
  riders: FleetRider[]
  focusId: string | null
  theme: Theme
}) {
  const el = useRef<HTMLDivElement>(null)
  const mapRef = useRef<GoogleMap | null>(null)
  const mapsRef = useRef<GoogleMaps | null>(null)
  const pinsRef = useRef<GoogleOverlay[]>([])
  const infoRef = useRef<GoogleInfoWindow | null>(null)
  const fitted = useRef(false)
  const [ready, setReady] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    let cancelled = false
    async function start() {
      try {
        const { googleMapsBrowserKey: key } = await api.mapsConfig()
        if (cancelled) {
          return
        }
        if (!key) {
          setError(`Set Maps:BrowserApiKey in the API appsettings. Allow this origin in Google Cloud: ${window.location.origin}`)
          return
        }
        const gmaps = await loadGoogleMaps(key)
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

    for (const pin of pinsRef.current) {
      pin.setMap(null)
    }
    pinsRef.current = []

    if (riders.length === 0) {
      map.panTo(CEBU)
      map.setZoom(13)
      return
    }

    const bounds = new gmaps.LatLngBounds()
    for (const rider of riders) {
      const position = { lat: rider.lat, lng: rider.lng }
      bounds.extend(position)
      const overlay = createPin(gmaps, map, rider, rider.id === focusId, () => {
        infoRef.current?.setContent(riderHtml(rider))
        infoRef.current?.open({ map, position })
      })
      pinsRef.current.push(overlay)
    }

    if (focusId) {
      const focused = riders.find((rider) => rider.id === focusId)
      if (focused) {
        const position = { lat: focused.lat, lng: focused.lng }
        map.panTo(position)
        map.setZoom(15)
        infoRef.current?.setContent(riderHtml(focused))
        infoRef.current?.open({ map, position })
      }
      return
    }

    if (!fitted.current && !bounds.isEmpty()) {
      map.fitBounds(bounds, 64)
      fitted.current = true
    }
  }, [riders, focusId, ready])

  if (error) {
    return (
      <div className="fleet-map-missing">
        <p>Google Maps is required for Fleet.</p>
        <p>{error}</p>
      </div>
    )
  }

  return <div ref={el} className="fleet-map-canvas" />
}
