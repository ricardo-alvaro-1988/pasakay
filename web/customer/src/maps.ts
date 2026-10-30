
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
  setDirections?: (result: unknown) => void
}

type PolylineHandle = { setMap: (m: unknown) => void }

type MapsApi = {
  Map: new (el: HTMLElement, opts: Record<string, unknown>) => MapHandle
  LatLng: new (lat: number, lng: number) => unknown
  LatLngBounds: new () => { extend: (p: { lat: number; lng: number }) => void }
  Size: new (w: number, h: number) => unknown
  Point: new (x: number, y: number) => unknown
  Marker: new (opts: Record<string, unknown>) => MarkerHandle
  Polyline?: new (opts: Record<string, unknown>) => PolylineHandle
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
  importLibrary?: (name: string) => Promise<Record<string, unknown>>
}

let mapsApiKey = ''

export type Prediction = {
  place_id: string
  description: string
  structured_formatting?: { main_text: string; secondary_text: string }
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
  mapsApiKey = key.trim()
  const ready = window.google?.maps
  if (ready) return Promise.resolve(ready)
  if (loader) return loader
  loader = new Promise((resolve, reject) => {
    let settled = false
    const finishOk = (): boolean => {
      if (settled) return true
      const maps = window.google?.maps
      if (!maps) return false
      settled = true
      resolve(maps)
      return true
    }
    const finishErr = (message: string) => {
      if (settled) return
      settled = true
      loader = null
      reject(new Error(message))
    }

    const existing = document.querySelector<HTMLScriptElement>('script[data-yp-maps]')
    if (existing) {
      if (finishOk()) return
      // Script tag may already have finished — `load` will never fire again.
      existing.addEventListener('load', () => {
        if (!finishOk()) finishErr('Google Maps did not initialize.')
      })
      existing.addEventListener('error', () => finishErr('Google Maps failed to load.'))
      let ticks = 0
      const poll = window.setInterval(() => {
        ticks += 1
        if (finishOk()) {
          window.clearInterval(poll)
          return
        }
        if (ticks >= 100) {
          window.clearInterval(poll)
          finishErr('Google Maps timed out.')
        }
      }, 50)
      return
    }

    const script = document.createElement('script')
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}&v=weekly&loading=async`
    script.async = true
    script.defer = true
    script.dataset.ypMaps = '1'
    script.onload = () => {
      if (!finishOk()) finishErr('Google Maps did not initialize.')
    }
    script.onerror = () => finishErr('Google Maps failed to load.')
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
    const fallback = `${lat.toFixed(5)}, ${lng.toFixed(5)}`
    try {
      const geocoder = new maps.Geocoder()
      geocoder.geocode({ location: { lat, lng } }, (results, status) => {
        const hit = status === 'OK' ? pickGeocodeAddress(results) : null
        if (hit) {
          resolve(hit.formatted_address)
          return
        }
        void reverseGeocodePlaces(lat, lng).then((address) => resolve(address || fallback))
      })
    } catch {
      void reverseGeocodePlaces(lat, lng).then((address) => resolve(address || fallback))
    }
  })
}

async function reverseGeocodePlaces(lat: number, lng: number) {
  if (!mapsApiKey) return null
  try {
    const response = await fetch('https://places.googleapis.com/v1/places:searchNearby', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Goog-Api-Key': mapsApiKey,
        'X-Goog-FieldMask': 'places.formattedAddress,places.displayName',
      },
      body: JSON.stringify({
        maxResultCount: 3,
        locationRestriction: {
          circle: {
            center: { latitude: lat, longitude: lng },
            radius: 80,
          },
        },
      }),
    })
    if (!response.ok) return null
    const data = (await response.json()) as {
      places?: { formattedAddress?: string; displayName?: { text?: string } }[]
    }
    const place = data.places?.[0]
    return place?.formattedAddress || place?.displayName?.text || null
  } catch {
    return null
  }
}

export async function searchPlaces(_maps: MapsApi, query: string, near?: { lat: number; lng: number }) {
  const trimmed = query.trim()
  if (trimmed.length < 2 || !mapsApiKey) return []
  try {
    const body: Record<string, unknown> = {
      input: trimmed,
      includedRegionCodes: ['ph'],
    }
    if (near) {
      body.locationBias = {
        circle: {
          center: { latitude: near.lat, longitude: near.lng },
          radius: 25000,
        },
      }
    }
    const response = await fetch('https://places.googleapis.com/v1/places:autocomplete', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Goog-Api-Key': mapsApiKey,
      },
      body: JSON.stringify(body),
    })
    if (!response.ok) return []
    const data = (await response.json()) as {
      suggestions?: {
        placePrediction?: {
          placeId?: string
          text?: { text?: string }
          structuredFormat?: { mainText?: { text?: string }; secondaryText?: { text?: string } }
        }
      }[]
    }
    return (data.suggestions ?? [])
      .map((item) => {
        const prediction = item.placePrediction
        const placeId = prediction?.placeId ?? ''
        const description = prediction?.text?.text ?? ''
        const main = prediction?.structuredFormat?.mainText?.text ?? description
        const secondary = prediction?.structuredFormat?.secondaryText?.text ?? ''
        return {
          place_id: placeId,
          description,
          structured_formatting: { main_text: main, secondary_text: secondary },
        } satisfies Prediction
      })
      .filter((item) => item.place_id && item.description)
  } catch {
    return []
  }
}

export async function placeDetails(_maps: MapsApi, placeId: string, _map?: MapHandle | null) {
  if (!placeId || !mapsApiKey) throw new Error('Could not load that place.')
  const id = placeId.startsWith('places/') ? placeId.slice('places/'.length) : placeId
  const response = await fetch(`https://places.googleapis.com/v1/places/${encodeURIComponent(id)}`, {
    headers: {
      'X-Goog-Api-Key': mapsApiKey,
      'X-Goog-FieldMask': 'id,displayName,formattedAddress,location',
    },
  })
  if (!response.ok) throw new Error('Could not load that place.')
  const place = (await response.json()) as {
    formattedAddress?: string
    displayName?: { text?: string }
    location?: { latitude?: number; longitude?: number }
  }
  const lat = place.location?.latitude
  const lng = place.location?.longitude
  if (lat == null || lng == null) throw new Error('Could not load that place.')
  return {
    address: place.formattedAddress || place.displayName?.text || 'Selected place',
    lat,
    lng,
  }
}

export function geocodeText(maps: MapsApi, query: string, near?: { lat: number; lng: number }) {
  return new Promise<StopResult[]>((resolve) => {
    const finishFromPlaces = async () => {
      const hints = await searchPlaces(maps, query, near)
      const stops: StopResult[] = []
      for (const hint of hints.slice(0, 5)) {
        try {
          const place = await placeDetails(maps, hint.place_id)
          stops.push({
            label: hint.structured_formatting?.main_text || place.address.split(',')[0] || query,
            details: place.address,
            lat: place.lat,
            lng: place.lng,
          })
        } catch {
          // Skip failed place lookups.
        }
      }
      resolve(stops)
    }

    try {
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
            void finishFromPlaces()
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
    } catch {
      void finishFromPlaces()
    }
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

type RouteHandle = DirectionsRendererHandle & {
  polylines: PolylineHandle[]
  requestId: number
}

export function drawDrivingRoute(
  maps: MapsApi,
  map: MapHandle,
  origin: { lat: number; lng: number },
  destination: { lat: number; lng: number },
  renderer: DirectionsRendererHandle | null,
) {
  const next = (renderer as RouteHandle | null) ?? {
    polylines: [] as PolylineHandle[],
    requestId: 0,
    setMap(m: unknown) {
      this.polylines.forEach((line) => line.setMap(m))
    },
  }
  next.polylines.forEach((line) => line.setMap(null))
  next.polylines = []
  const requestId = (next.requestId = (next.requestId || 0) + 1)

  void (async () => {
    try {
      const routesLib = (await (maps.importLibrary
        ? maps.importLibrary('routes')
        : window.google?.maps?.importLibrary?.('routes'))) as {
        Route?: {
          computeRoutes: (req: Record<string, unknown>) => Promise<{
            routes?: {
              createPolylines?: (opts?: Record<string, unknown>) => PolylineHandle[]
              path?: { lat: () => number; lng: () => number }[]
            }[]
          }>
        }
      } | null
      const Route = routesLib?.Route
      if (!Route?.computeRoutes) return

      const result = await Route.computeRoutes({
        origin,
        destination,
        travelMode: 'DRIVING',
        fields: ['path'],
      })
      if (next.requestId !== requestId) return
      const route = result.routes?.[0]
      if (!route) return

      let polylines: PolylineHandle[] = []
      if (typeof route.createPolylines === 'function') {
        polylines = route.createPolylines({
          polylineOptions: {
            strokeColor: '#e30613',
            strokeOpacity: 0.4,
            strokeWeight: 5,
          },
        }) ?? []
      } else if (route.path?.length && maps.Polyline) {
        polylines = [
          new maps.Polyline({
            path: route.path,
            strokeColor: '#e30613',
            strokeOpacity: 0.4,
            strokeWeight: 5,
            map,
          }),
        ]
      }

      if (next.requestId !== requestId) {
        polylines.forEach((line) => line.setMap(null))
        return
      }
      next.polylines = polylines
      polylines.forEach((line) => line.setMap(map))
    } catch {
      // Keep map usable even if routing is unavailable.
    }
  })()

  return next
}
