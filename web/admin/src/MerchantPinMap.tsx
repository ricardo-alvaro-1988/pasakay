import { FormEvent, useEffect, useRef, useState } from 'react'
import { api } from './api'
import { loadGoogleMaps } from './FleetMap'
import { placeDetailsNew, reverseGeocodePlacesNew, searchPlacesNew, type PlaceSuggestion } from './places-new'

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

const DEFAULT = { lat: 13.4115, lng: 121.1803 }

export function MerchantPinMap({ value, onChange, height = 280 }: Props) {
  const host = useRef<HTMLDivElement>(null)
  const mapRef = useRef<GMap | null>(null)
  const markerRef = useRef<GMarker | null>(null)
  const gmapsRef = useRef<GMaps | null>(null)
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange
  const [error, setError] = useState('')
  const [query, setQuery] = useState('')
  const [hints, setHints] = useState<PlaceSuggestion[]>([])
  const [searchBusy, setSearchBusy] = useState(false)

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
          const finish = (address: string) => onChangeRef.current({ lat, lng, address })
          try {
            const geocoder = new gmaps.Geocoder()
            geocoder.geocode({ location: { lat, lng } }, (results, status) => {
              if (status === 'OK' && results?.[0]?.formatted_address) {
                finish(results[0].formatted_address)
                return
              }
              void reverseGeocodePlacesNew(lat, lng).then((address) =>
                finish(address || `${lat.toFixed(6)}, ${lng.toFixed(6)}`),
              )
            })
          } catch {
            void reverseGeocodePlacesNew(lat, lng).then((address) =>
              finish(address || `${lat.toFixed(6)}, ${lng.toFixed(6)}`),
            )
          }
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

  useEffect(() => {
    const q = query.trim()
    if (q.length < 2) {
      setHints([])
      return
    }
    let cancelled = false
    const timer = window.setTimeout(() => {
      setSearchBusy(true)
      void searchPlacesNew(q, { lat: value.lat || DEFAULT.lat, lng: value.lng || DEFAULT.lng })
        .then((next) => {
          if (!cancelled) setHints(next.slice(0, 6))
        })
        .catch(() => {
          if (!cancelled) setHints([])
        })
        .finally(() => {
          if (!cancelled) setSearchBusy(false)
        })
    }, 220)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [query, value.lat, value.lng])

  async function pickHint(hint: PlaceSuggestion) {
    setSearchBusy(true)
    setError('')
    try {
      const place = await placeDetailsNew(hint.placeId)
      mapRef.current?.setCenter({ lat: place.lat, lng: place.lng })
      mapRef.current?.setZoom(17)
      markerRef.current?.setPosition({ lat: place.lat, lng: place.lng })
      onChange({ lat: place.lat, lng: place.lng, address: place.address })
      setQuery(place.address)
      setHints([])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load that place.')
    } finally {
      setSearchBusy(false)
    }
  }

  function onSearchSubmit(e: FormEvent) {
    e.preventDefault()
    const first = hints[0]
    if (first) void pickHint(first)
  }

  return (
    <div className="merchant-pin-map">
      <form className="merchant-pin-search" onSubmit={onSearchSubmit}>
        <span className="merchant-pin-search-icon" aria-hidden="true">⌕</span>
        <input
          type="search"
          className="merchant-pin-search-input"
          placeholder="Search place or address"
          autoComplete="off"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </form>
      {hints.length > 0 ? (
        <div className="merchant-pin-hints" role="listbox">
          {hints.map((hint) => (
            <button
              key={hint.placeId}
              type="button"
              className="merchant-pin-hint"
              onClick={() => void pickHint(hint)}
              disabled={searchBusy}
            >
              {hint.description}
            </button>
          ))}
        </div>
      ) : null}
      {error ? <p className="error">{error}</p> : null}
      <div
        ref={host}
        className="merchant-pin-map-host"
        style={{ height }}
      />
    </div>
  )
}
