
export const PH = { lat: 12.8797, lng: 121.7740 }

export type MapPadding = { top: number; right: number; bottom: number; left: number }

export type MapHandle = {
  panTo: (p: { lat: number; lng: number }) => void
  panBy: (x: number, y: number) => void
  setZoom: (z: number) => void
  fitBounds: (b: unknown, pad?: number | MapPadding) => void
  setOptions: (opts: Record<string, unknown>) => void
  addListener: (name: string, handler: (e: { latLng?: { lat: () => number; lng: () => number } }) => void) => void
}

export type MarkerHandle = {
  setMap: (m: unknown) => void
  setPosition: (p: { lat: number; lng: number }) => void
  getPosition: () => { lat: () => number; lng: () => number } | null
  addListener: (name: string, handler: () => void) => void
}

export type OverlayHandle = {
  setMap: (m: unknown) => void
  onAdd?: () => void
  draw?: () => void
  onRemove?: () => void
  getPanes?: () => { overlayMouseTarget?: HTMLElement; overlayLayer?: HTMLElement } | null
  getProjection?: () => { fromLatLngToDivPixel: (ll: unknown) => { x: number; y: number } | null } | null
}

export type DirectionsRendererHandle = {
  setMap: (m: unknown) => void
  setDirections: (result: unknown) => void
}

type MapsApi = {
  Map: new (el: HTMLElement, opts: Record<string, unknown>) => MapHandle
  LatLng: new (lat: number, lng: number) => unknown
  LatLngBounds: new () => { extend: (p: { lat: number; lng: number }) => void }
  Size: new (w: number, h: number) => unknown
  Point: new (x: number, y: number) => unknown
  Marker: new (opts: Record<string, unknown>) => MarkerHandle
  DirectionsService: new () => {
    route: (req: unknown, cb: (result: unknown, status: string) => void) => void
  }
  DirectionsRenderer: new (opts: Record<string, unknown>) => DirectionsRendererHandle
  TravelMode: { DRIVING: string }
  Geocoder: new () => {
    geocode: (
      req: unknown,
      cb: (
        results: {
          formatted_address: string
          types?: string[]
          geometry?: { location?: { lat: () => number; lng: () => number } }
        }[] | null,
        status: string,
      ) => void,
    ) => void
  }
  OverlayView: new () => OverlayHandle
  places: {
    AutocompleteService: new () => {
      getPlacePredictions: (
        req: unknown,
        cb: (predictions: Prediction[] | null, status: string) => void,
      ) => void
    }
    PlacesService: new (mapOrEl: unknown) => {
      getDetails: (req: unknown, cb: (place: PlaceResult | null, status: string) => void) => void
    }
    PlacesServiceStatus: { OK: string }
  }
}

export type Prediction = {
  place_id: string
  description: string
  structured_formatting?: { main_text: string; secondary_text: string }
}

type PlaceResult = {
  formatted_address?: string
  name?: string
  geometry?: { location?: { lat: () => number; lng: () => number } }
}

declare global {
  interface Window {
    google?: {
      maps?: MapsApi
      accounts?: {
        id: {
          initialize: (config: {
            client_id: string
            callback: (response: { credential: string }) => void
            auto_select?: boolean
            ux_mode?: 'popup' | 'redirect'
          }) => void
          renderButton: (
            parent: HTMLElement,
            options: {
              type?: string
              theme?: string
              size?: string
              text?: string
              shape?: string
              width?: number
              locale?: string
            },
          ) => void
        }
      }
    }
  }
}

let loader: Promise<MapsApi> | null = null

export function loadGoogleMaps(key: string) {
  const ready = window.google?.maps
  if (ready) return Promise.resolve(ready)
  if (loader) return loader
  loader = new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>('script[data-yp-maps]')
    if (existing) {
      existing.addEventListener('load', () => {
        const maps = window.google?.maps
        if (maps) resolve(maps)
        else reject(new Error('Google Maps did not initialize.'))
      })
      return
    }
    const script = document.createElement('script')
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}&libraries=places`
    script.async = true
    script.defer = true
    script.dataset.ypMaps = '1'
    script.onload = () => {
      const maps = window.google?.maps
      if (maps) resolve(maps)
      else reject(new Error('Google Maps did not initialize.'))
    }
    script.onerror = () => reject(new Error('Google Maps failed to load.'))
    document.head.appendChild(script)
  })
  return loader
}

const STREET_TYPES = ['street_address', 'premise', 'subpremise', 'route', 'intersection']

function pickGeocodeAddress(
  results: { formatted_address: string; types?: string[] }[] | null,
) {
  if (!results?.length) return null
  // Prefer real street/locality addresses over bare Plus Codes (e.g. "QXJ9+JWF"),
  // which break municipality matching and hide Offered types like Sedan.
  const ranked = [...results].sort((a, b) => geocodeScore(b) - geocodeScore(a))
  return ranked[0] ?? null
}

function geocodeScore(item: { formatted_address: string; types?: string[] }) {
  const types = item.types ?? []
  const addr = item.formatted_address ?? ''
  let score = Math.min(addr.length, 100)
  if (types.some((t) => STREET_TYPES.includes(t))) score += 80
  if (types.includes('neighborhood') || types.includes('sublocality') || types.includes('sublocality_level_1')) score += 40
  if (types.includes('locality') || types.includes('administrative_area_level_2')) score += 50
  if (types.includes('plus_code')) score -= addr.includes(',') ? 10 : 120
  return score
}

function around(maps: MapsApi, near: { lat: number; lng: number }, delta = 0.12) {
  const bounds = new maps.LatLngBounds()
  bounds.extend({ lat: near.lat - delta, lng: near.lng - delta })
  bounds.extend({ lat: near.lat + delta, lng: near.lng + delta })
  return bounds
}

export function reverseGeocode(maps: MapsApi, lat: number, lng: number) {
  return new Promise<string>((resolve) => {
    const geocoder = new maps.Geocoder()
    geocoder.geocode({ location: { lat, lng } }, (results, status) => {
      const hit = status === 'OK' ? pickGeocodeAddress(results) : null
      if (hit) resolve(hit.formatted_address)
      else resolve(`${lat.toFixed(5)}, ${lng.toFixed(5)}`)
    })
  })
}

export function searchPlaces(maps: MapsApi, query: string, near?: { lat: number; lng: number }) {
  return new Promise<Prediction[]>((resolve) => {
    try {
      const service = new maps.places.AutocompleteService()
      service.getPlacePredictions(
        {
          input: query,
          componentRestrictions: { country: 'ph' },
          ...(near ? { location: new maps.LatLng(near.lat, near.lng), radius: 25000 } : {}),
        },
        (predictions, status) => {
          if (status === maps.places.PlacesServiceStatus.OK && predictions) resolve(predictions)
          else resolve([])
        },
      )
    } catch {
      resolve([])
    }
  })
}

export function placeDetails(maps: MapsApi, placeId: string, map?: MapHandle | null) {
  return new Promise<{ address: string; lat: number; lng: number }>((resolve, reject) => {
    try {
      const service = new maps.places.PlacesService(map ?? document.getElementById('yp-places-host') ?? placesHost())
      service.getDetails({ placeId, fields: ['formatted_address', 'geometry', 'name'] }, (place, status) => {
        const loc = place?.geometry?.location
        if (status !== maps.places.PlacesServiceStatus.OK || !loc) {
          reject(new Error('Could not load that place.'))
          return
        }
        resolve({
          address: place.formatted_address || place.name || 'Selected place',
          lat: loc.lat(),
          lng: loc.lng(),
        })
      })
    } catch (err) {
      reject(err instanceof Error ? err : new Error('Could not load that place.'))
    }
  })
}

function placesHost() {
  let host = document.getElementById('yp-places-host')
  if (!host) {
    host = document.createElement('div')
    host.id = 'yp-places-host'
    document.body.appendChild(host)
  }
  return host
}

export function geocodeText(maps: MapsApi, query: string, near?: { lat: number; lng: number }) {
  return new Promise<StopResult[]>((resolve) => {
    const geocoder = new maps.Geocoder()
    geocoder.geocode(
      {
        address: query,
        componentRestrictions: { country: 'PH' },
        region: 'ph',
        ...(near ? { bounds: around(maps, near) } : {}),
      },
      (results, status) => {
        if (status !== 'OK' || !results?.length) {
          resolve([])
          return
        }
        resolve(results.map((item) => {
          const loc = item.geometry?.location
          return {
            label: item.formatted_address.split(',')[0] || query,
            details: item.formatted_address,
            lat: loc?.lat() ?? near?.lat ?? PH.lat,
            lng: loc?.lng() ?? near?.lng ?? PH.lng,
          }
        }))
      },
    )
  })
}

export type StopResult = {
  label: string
  details: string
  lat: number
  lng: number
}

export function pinIcon(maps: MapsApi, color: string) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="22" height="30" viewBox="0 0 22 30">
    <path fill="${color}" d="M11 0C4.9 0 0 4.9 0 11c0 7.7 11 19 11 19s11-11.3 11-19C22 4.9 17.1 0 11 0z"/>
    <circle cx="11" cy="11" r="4" fill="#fff"/>
  </svg>`
  return {
    url: `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`,
    scaledSize: new maps.Size(22, 30),
    anchor: new maps.Point(11, 28),
  }
}

export function pulseStopPin(
  maps: MapsApi,
  map: MapHandle,
  position: { lat: number; lng: number },
  kind: 'pickup' | 'dropoff',
) {
  const overlay = new maps.OverlayView()
  let el: HTMLDivElement | null = null
  overlay.onAdd = () => {
    el = document.createElement('div')
    el.className = `yp-stop-pin ${kind}`
    el.innerHTML = '<span class="yp-stop-pulse"></span><span class="yp-stop-pulse delay"></span><span class="yp-stop-core" aria-hidden="true"></span>'
    overlay.getPanes?.()?.overlayLayer?.appendChild(el)
  }
  overlay.draw = () => {
    if (!el) return
    const point = overlay.getProjection?.()?.fromLatLngToDivPixel(new maps.LatLng(position.lat, position.lng))
    if (!point) return
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

function dragHandleIcon(maps: MapsApi) {
  return {
    url: 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7',
    scaledSize: new maps.Size(36, 36),
    anchor: new maps.Point(18, 18),
  }
}

export function stopDragIcon(maps: MapsApi) {
  return dragHandleIcon(maps)
}

export function riderIcon(maps: MapsApi, vehicle = 'Motorcycle') {
  const key = String(vehicle).toLowerCase()
  const trike = key.includes('tricycle') || vehicle === '2' || vehicle === 'Tricycle'
  const car = !trike && (
    key.includes('sedan') || key.includes('mpv') || key.includes('suv') || key.includes('van')
    || key.includes('pickup') || key.includes('cargo') || vehicle === '3' || vehicle === '4'
    || vehicle === '5' || vehicle === '6' || vehicle === '7' || vehicle === '8'
  )
  if (trike) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="36" height="46" viewBox="0 0 36 46">
      <defs>
        <filter id="shadow" x="-50%" y="-50%" width="200%" height="200%">
          <feDropShadow dx="0" dy="2" stdDeviation="2.2" flood-color="rgba(18,22,28,.22)"/>
        </filter>
      </defs>
      <g filter="url(#shadow)">
        <path fill="#e30613" d="M18 2C10.27 2 4 8.27 4 16c0 10.55 10.85 21.74 13.01 23.84a1.4 1.4 0 0 0 1.98 0C21.15 37.74 32 26.55 32 16 32 8.27 25.73 2 18 2Z"/>
        <circle cx="18" cy="16" r="9.25" fill="#fff"/>
        <path fill="#e30613" d="M10.9 18.8a1.75 1.75 0 1 1 0-3.5 1.75 1.75 0 0 1 0 3.5Zm10.5 0a1.75 1.75 0 1 1 0-3.5 1.75 1.75 0 0 1 0 3.5Z"/>
        <path fill="#e30613" d="M13.2 17.3h4.7l1-2.3 2.4-1 1.1 1-1.4 2.3h2.7l-.7 1.6H12.5l.7-1.6Z"/>
        <path fill="#1f2329" d="M15.6 12.6H19v1.5h-3.4Zm6.2.8 1.8-.7.4.9-1.4.7Z"/>
      </g>
    </svg>`
    return {
      url: `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`,
      scaledSize: new maps.Size(36, 46),
      anchor: new maps.Point(18, 39),
    }
  }
  if (car) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="36" height="46" viewBox="0 0 36 46">
      <defs>
        <filter id="shadow" x="-50%" y="-50%" width="200%" height="200%">
          <feDropShadow dx="0" dy="2" stdDeviation="2.2" flood-color="rgba(18,22,28,.22)"/>
        </filter>
      </defs>
      <g filter="url(#shadow)">
        <path fill="#e30613" d="M18 2C10.27 2 4 8.27 4 16c0 10.55 10.85 21.74 13.01 23.84a1.4 1.4 0 0 0 1.98 0C21.15 37.74 32 26.55 32 16 32 8.27 25.73 2 18 2Z"/>
        <circle cx="18" cy="16" r="9.25" fill="#fff"/>
        <path fill="#e30613" d="M11.2 18.2h13.6l-1.1-3.2c-.3-.9-1.1-1.5-2-1.5h-7.4c-.9 0-1.7.6-2 1.5l-1.1 3.2Z"/>
        <circle cx="13.4" cy="19.1" r="1.55" fill="#1f2329"/>
        <circle cx="22.6" cy="19.1" r="1.55" fill="#1f2329"/>
        <path fill="#1f2329" d="M14.2 13.8h7.6v1.1h-7.6z" opacity=".35"/>
      </g>
    </svg>`
    return {
      url: `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`,
      scaledSize: new maps.Size(36, 46),
      anchor: new maps.Point(18, 39),
    }
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="36" height="46" viewBox="0 0 36 46">
    <defs>
      <filter id="shadow" x="-50%" y="-50%" width="200%" height="200%">
        <feDropShadow dx="0" dy="2" stdDeviation="2.2" flood-color="rgba(18,22,28,.22)"/>
      </filter>
    </defs>
    <g filter="url(#shadow)">
      <path fill="#e30613" d="M18 2C10.27 2 4 8.27 4 16c0 10.55 10.85 21.74 13.01 23.84a1.4 1.4 0 0 0 1.98 0C21.15 37.74 32 26.55 32 16 32 8.27 25.73 2 18 2Z"/>
      <circle cx="18" cy="16" r="9.25" fill="#fff"/>
      <circle cx="12.6" cy="18.8" r="1.85" fill="#1f2329"/>
      <circle cx="23.3" cy="18.8" r="1.85" fill="#1f2329"/>
      <circle cx="12.6" cy="18.8" r=".78" fill="#eef1f5"/>
      <circle cx="23.3" cy="18.8" r=".78" fill="#eef1f5"/>
      <path fill="#e30613" d="M15.2 13.1h4c1.12 0 1.93.27 2.5.82l1.95 1.67-1.03 1.12-1.73-1.34a1.72 1.72 0 0 0-1.1-.36h-1.78l-1.04 2.17h4.05c.78 0 1.4.18 1.87.57l1.18 1.03h-2.5l-.83-.7H15l-1.3-2.74h-2.82l.55-1.3h3.06Z"/>
      <path fill="#1f2329" d="M13.55 13.45h1.57l1.9 4.17h-1.7Zm9.92.76 1.75-.57.35.88-1.35.62Z"/>
    </g>
  </svg>`
  return {
    url: `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`,
    scaledSize: new maps.Size(36, 46),
    anchor: new maps.Point(18, 39),
  }
}

export function drawDrivingRoute(
  maps: MapsApi,
  map: MapHandle,
  origin: { lat: number; lng: number },
  destination: { lat: number; lng: number },
  renderer: DirectionsRendererHandle | null,
) {
  const next = renderer ?? new maps.DirectionsRenderer({
    suppressMarkers: true,
    preserveViewport: true,
    polylineOptions: {
      strokeColor: '#e30613',
      strokeOpacity: 0.4,
      strokeWeight: 5,
    },
  })
  next.setMap(map)
  const service = new maps.DirectionsService()
  service.route(
    { origin, destination, travelMode: maps.TravelMode.DRIVING },
    (result, status) => {
      if (status === 'OK' && result) next.setDirections(result)
    },
  )
  return next
}
