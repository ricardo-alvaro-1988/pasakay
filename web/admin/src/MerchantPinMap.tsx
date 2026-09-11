import { useEffect, useRef, useState } from 'react'
import { api } from './api'
import { loadGoogleMaps } from './FleetMap'

export type MerchantPin = { lat: number; lng: number; address: string }

type Props = {
  value: MerchantPin
  onChange: (pin: MerchantPin) => void
  height?: number
}

type GMaps = {
  Map: new (el: HTMLElement, opts: Record<string, unknown>) => GMap
  Marker: new (opts: Record<string, unknown>) => GMarker
  Geocoder: new () => GGeocoder
  event: {
    addListener: (target: unknown, name: string, handler: (...args: never[]) => void) => void
  }
  places?: {
    Autocomplete: new (input: HTMLInputElement, opts?: Record<string, unknown>) => GAutocomplete
  }
}

type GMap = {
  setCenter: (p: { lat: number; lng: number }) => void
  setZoom: (z: number) => void
}

type GMarker = {
  setMap: (map: GMap | null) => void
  setPosition: (p: { lat: number; lng: number }) => void
}

type GLatLng = { lat: () => number; lng: () => number }

type GGeocoder = {
  geocode: (
    req: Record<string, unknown>,
    cb: (results: Array<{ formatted_address?: string; geometry?: { location?: GLatLng } }> | null, status: string) => void,
  ) => void
}

type GAutocomplete = {
  addListener: (name: string, handler: () => void) => void
  getPlace: () => {
    geometry?: { location?: GLatLng }
    formatted_address?: string
    name?: string
  }
}

const DEFAULT = { lat: 13.4115, lng: 121.1803 }

export function MerchantPinMap({ value, onChange, height = 280 }: Props) {
  const host = useRef<HTMLDivElement>(null)
  const searchRef = useRef<HTMLInputElement>(null)
  const mapRef = useRef<GMap | null>(null)
  const markerRef = useRef<GMarker | null>(null)
  const gmapsRef = useRef<GMaps | null>(null)
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange
  const [error, setError] = useState('')

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const { googleMapsBrowserKey: key } = await api.mapsConfig()
        if (!key || cancelled) {
          if (!cancelled) setError('Google Maps key is not configured.')
          return
        }
        const gmaps = (await loadGoogleMaps(key)) as unknown as GMaps
        if (cancelled || !host.current) return
        gmapsRef.current = gmaps
        const center = value.lat && value.lng ? { lat: value.lat, lng: value.lng } : DEFAULT
        const map = new gmaps.Map(host.current, {
          center,
          zoom: 15,
          mapTypeControl: false,
          streetViewControl: false,
          fullscreenControl: false,
        })
        mapRef.current = map
        const marker = new gmaps.Marker({
          map,
          position: center,
          draggable: true,
        })
        markerRef.current = marker

        const reverse = (lat: number, lng: number) => {
          const geocoder = new gmaps.Geocoder()
          geocoder.geocode({ location: { lat, lng } }, (results, status) => {
            const address =
              status === 'OK' && results?.[0]?.formatted_address
                ? results[0].formatted_address
                : `${lat.toFixed(6)}, ${lng.toFixed(6)}`
            onChangeRef.current({ lat, lng, address })
          })
        }

        gmaps.event.addListener(map, 'click', ((e: { latLng?: GLatLng }) => {
          const ll = e.latLng
          if (!ll) return
          const lat = ll.lat()
          const lng = ll.lng()
          marker.setPosition({ lat, lng })
          reverse(lat, lng)
        }) as (...args: never[]) => void)

        gmaps.event.addListener(marker, 'dragend', ((e: { latLng?: GLatLng }) => {
          const ll = e.latLng
          if (!ll) return
          reverse(ll.lat(), ll.lng())
        }) as (...args: never[]) => void)

        if (searchRef.current && gmaps.places?.Autocomplete) {
          const ac = new gmaps.places.Autocomplete(searchRef.current, {
            fields: ['geometry', 'formatted_address', 'name'],
            componentRestrictions: { country: 'ph' },
          })
          ac.addListener('place_changed', () => {
            const place = ac.getPlace()
            const loc = place.geometry?.location
            if (!loc) return
            const lat = loc.lat()
            const lng = loc.lng()
            map.setCenter({ lat, lng })
            map.setZoom(17)
            marker.setPosition({ lat, lng })
            onChangeRef.current({
              lat,
              lng,
              address: place.formatted_address || place.name || `${lat.toFixed(6)}, ${lng.toFixed(6)}`,
            })
          })
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load Google Maps.')
      }
    })()
    return () => {
      cancelled = true
    }
    // init once
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    if (!mapRef.current || !markerRef.current) return
    if (!Number.isFinite(value.lat) || !Number.isFinite(value.lng)) return
    markerRef.current.setPosition({ lat: value.lat, lng: value.lng })
  }, [value.lat, value.lng])

  return (
    <div>
      <input
        ref={searchRef}
        className="input"
        placeholder="Search place or address"
        style={{ width: '100%', marginBottom: 8 }}
      />
      {error ? <p className="error">{error}</p> : null}
      <div ref={host} style={{ height, width: '100%', borderRadius: 8, overflow: 'hidden', border: '1px solid var(--border, #d9dde5)' }} />
      <p className="muted" style={{ margin: '8px 0 0', fontSize: 13 }}>
        Click the map or drag the pin. Address updates from Google.
      </p>
    </div>
  )
}
