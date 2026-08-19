export type GpsReadOptions = {
  goodAccuracyM?: number
  acceptAccuracyM?: number
  rejectAccuracyM?: number
  waitMs?: number
  minSamples?: number
  onProgress?: (pos: GeolocationPosition) => void
}

const GPS_OPTIONS: PositionOptions = { enableHighAccuracy: true, timeout: 25000, maximumAge: 0 }

export type CachedGps = { lat: number; lng: number; accuracy: number; at: number }

let lastGps: CachedGps | null = null

export function rememberGps(pos: GeolocationPosition) {
  const acc = pos.coords.accuracy
  if (!Number.isFinite(acc) || acc <= 0) return
  if (!lastGps || acc <= lastGps.accuracy || Date.now() - lastGps.at > 8000) {
    lastGps = {
      lat: pos.coords.latitude,
      lng: pos.coords.longitude,
      accuracy: acc,
      at: Date.now(),
    }
  }
}

export function lastKnownGps(maxAgeMs = 120_000): CachedGps | null {
  if (!lastGps) return null
  if (Date.now() - lastGps.at > maxAgeMs) return null
  return lastGps
}

function ensureGpsAvailable() {
  if (!navigator.geolocation) {
    throw new Error('Location is not available on this device.')
  }
  const host = window.location.hostname
  const local = host === 'localhost' || host === '127.0.0.1' || host === '[::1]'
  if (!window.isSecureContext && !local) {
    throw new Error('GPS needs localhost on this PC. Open http://127.0.0.1:5174 here, not the phone IP.')
  }
}

function usable(pos: GeolocationPosition, maxAccuracyM: number) {
  const acc = pos.coords.accuracy
  if (!Number.isFinite(acc) || acc <= 0) return false
  if (acc > maxAccuracyM) return false
  if (pos.coords.latitude === 0 && pos.coords.longitude === 0) return false
  return true
}

function better(next: GeolocationPosition, current: GeolocationPosition | null) {
  if (!current) return true
  if (next.coords.accuracy < current.coords.accuracy) return true
  if (next.coords.accuracy === current.coords.accuracy && next.timestamp > current.timestamp) return true
  return false
}

export function readGps(options: GpsReadOptions = {}): Promise<GeolocationPosition> {
  const goodAccuracyM = options.goodAccuracyM ?? 30
  const acceptAccuracyM = options.acceptAccuracyM ?? 70
  const rejectAccuracyM = options.rejectAccuracyM ?? 180
  const waitMs = options.waitMs ?? 16000
  const minSamples = options.minSamples ?? 2
  const onProgress = options.onProgress

  return new Promise((resolve, reject) => {
    try {
      ensureGpsAvailable()
    } catch (err) {
      reject(err instanceof Error ? err : new Error('Location is not available.'))
      return
    }

    let best: GeolocationPosition | null = null
    let samples = 0
    let settled = false
    let watchId = 0

    const done = (pos: GeolocationPosition) => {
      if (settled) return
      settled = true
      rememberGps(pos)
      navigator.geolocation.clearWatch(watchId)
      window.clearTimeout(timer)
      resolve(pos)
    }

    const fail = (err: GeolocationPositionError | Error) => {
      if (settled) return
      if (best && usable(best, acceptAccuracyM)) {
        done(best)
        return
      }
      if (best && usable(best, rejectAccuracyM)) {
        done(best)
        return
      }
      settled = true
      navigator.geolocation.clearWatch(watchId)
      window.clearTimeout(timer)
      reject(err)
    }

    watchId = navigator.geolocation.watchPosition(
      (pos) => {
        samples += 1
        if (!usable(pos, rejectAccuracyM)) return
        if (better(pos, best)) {
          best = pos
          rememberGps(pos)
          onProgress?.(pos)
        }
        if (pos.coords.accuracy <= goodAccuracyM && (samples >= minSamples || pos.coords.accuracy <= 15)) {
          done(pos)
        }
      },
      (err) => {
        if (err.code === err.PERMISSION_DENIED) fail(err)
      },
      GPS_OPTIONS,
    )

    const timer = window.setTimeout(() => {
      if (best && usable(best, acceptAccuracyM)) {
        done(best)
        return
      }
      if (best && usable(best, rejectAccuracyM)) {
        done(best)
        return
      }
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          if (usable(pos, rejectAccuracyM)) {
            onProgress?.(pos)
            done(pos)
          } else {
            fail(new Error('GPS is still too rough. Move outdoors and try again.'))
          }
        },
        fail,
        GPS_OPTIONS,
      )
    }, waitMs)
  })
}

/** Lenient GPS for first page load — pans map early, accepts coarser fixes. */
export function readBootGps(onProgress?: (pos: GeolocationPosition) => void) {
  return readGps({
    goodAccuracyM: 60,
    acceptAccuracyM: 200,
    rejectAccuracyM: 800,
    waitMs: 12000,
    minSamples: 1,
    onProgress,
  })
}

/** Pickup / locate button — faster lock, accepts indoor fixes. */
export function readPickupGps(onProgress?: (pos: GeolocationPosition) => void) {
  return readGps({
    goodAccuracyM: 40,
    acceptAccuracyM: 150,
    rejectAccuracyM: 500,
    waitMs: 10000,
    minSamples: 1,
    onProgress,
  })
}

export function watchTripGps() {
  if (!navigator.geolocation) return () => {}
  try {
    ensureGpsAvailable()
  } catch {
    return () => {}
  }
  const id = navigator.geolocation.watchPosition(
    (pos) => rememberGps(pos),
    () => {},
    { enableHighAccuracy: true, maximumAge: 5000, timeout: 20000 },
  )
  return () => navigator.geolocation.clearWatch(id)
}
