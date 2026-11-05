import { mapsApiKey } from './FleetMap'

export type PlaceSuggestion = {
  placeId: string
  description: string
}

let placesSessionToken: string | null = null
const reverseCache = new Map<string, { address: string; at: number }>()
const REVERSE_TTL_MS = 10 * 60 * 1000

function placesSession(): string {
  if (!placesSessionToken) {
    placesSessionToken =
      typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
        ? crypto.randomUUID()
        : `s${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`
  }
  return placesSessionToken
}

function endPlacesSession() {
  placesSessionToken = null
}

export async function searchPlacesNew(query: string, near?: { lat: number; lng: number }) {
  const trimmed = query.trim()
  if (trimmed.length < 2 || !mapsApiKey) return [] as PlaceSuggestion[]
  const body: Record<string, unknown> = {
    input: trimmed,
    includedRegionCodes: ['ph'],
    sessionToken: placesSession(),
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
    signal: AbortSignal.timeout(8000),
  })
  if (!response.ok) return []
  const data = (await response.json()) as {
    suggestions?: {
      placePrediction?: {
        placeId?: string
        text?: { text?: string }
      }
    }[]
  }
  return (data.suggestions ?? [])
    .map((item) => ({
      placeId: item.placePrediction?.placeId ?? '',
      description: item.placePrediction?.text?.text ?? '',
    }))
    .filter((item) => item.placeId && item.description)
}

export async function placeDetailsNew(placeId: string) {
  if (!placeId || !mapsApiKey) throw new Error('Could not load that place.')
  const id = placeId.startsWith('places/') ? placeId.slice('places/'.length) : placeId
  const token = placesSession()
  try {
    const response = await fetch(
      `https://places.googleapis.com/v1/places/${encodeURIComponent(id)}?sessionToken=${encodeURIComponent(token)}`,
      {
        headers: {
          'X-Goog-Api-Key': mapsApiKey,
          'X-Goog-FieldMask': 'id,displayName,formattedAddress,location',
        },
        signal: AbortSignal.timeout(8000),
      },
    )
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
  } finally {
    endPlacesSession()
  }
}

export async function reverseGeocodePlacesNew(lat: number, lng: number) {
  if (!mapsApiKey) return null
  const cacheKey = `${lat.toFixed(4)},${lng.toFixed(4)}`
  const cached = reverseCache.get(cacheKey)
  if (cached && Date.now() - cached.at < REVERSE_TTL_MS) {
    return cached.address
  }
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
      signal: AbortSignal.timeout(7000),
    })
    if (!response.ok) return null
    const data = (await response.json()) as {
      places?: { formattedAddress?: string; displayName?: { text?: string } }[]
    }
    const place = data.places?.[0]
    const address = place?.formattedAddress || place?.displayName?.text || null
    if (address) {
      reverseCache.set(cacheKey, { address, at: Date.now() })
    }
    return address
  } catch {
    return null
  }
}
