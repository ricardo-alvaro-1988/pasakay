import { useEffect, useRef, useState } from 'react'
import logo from './logo-circle.png'
import {
  api,
  BookBody,
  clearToken,
  CustomerTrip,
  Desk,
  getToken,
  paymentLabel,
  peso,
  kmLabel,
  Quote,
  Stop,
  tripHeadline,
  tripCanSendChat,
  tripCanViewChat,
  mediaUrl,
  isOperatorCoverageError,
  VehicleType,
  PaymentMethod,
} from './api'
import {
  PH,
  DirectionsRendererHandle,
  drawDrivingRoute,
  geocodeText,
  loadGoogleMaps,
  MapHandle,
  MarkerHandle,
  OverlayHandle,
  placeDetails,
  Prediction,
  pulseStopPin,
  reverseGeocode,
  riderIcon,
  searchPlaces,
  stopDragIcon,
  StopResult,
} from './maps'
import { AuthScreen, CompleteMobile } from './auth-screens'
import { LoginBrandPanel } from './login-brand-panel'
import { AccountHub, AccountPage, BookingScreen, PaymentBar, ScheduleScreen } from './account-screens'
import { NoOperatorNotice, useNoOperatorNotice } from './no-operator-notice'
import { VEHICLE_ART } from './vehicle-art'
import { ShowQrButton, ShowQrOverlay } from './scan-qr'
import { TripChatPanel } from './trip-chat'
import { createDeskConnection, startDeskHub, stopDeskHub, emitDeskChat } from './desk-hub'
import type { HubConnection } from '@microsoft/signalr'
import { RateRidePanel, usePendingRating } from './rate-ride'
import { readTheme, setTheme, type Theme } from './theme'
import { ThemeSwitch } from './theme-switch'
import { ShareTripButton } from './share-trip-button'
import { lastKnownGps, readBootGps, readPickupGps, readGps, watchTripGps } from './gps'

type Tab = 'home' | 'booking' | 'schedule' | 'account'
type SearchTarget = 'pickup' | 'dropoff' | null

function addressLabel(details: string) {
  return details.split(',')[0]?.trim() || 'Current location'
}

export default function App() {
  const [desk, setDesk] = useState<Desk | null>(null)
  const [boot, setBoot] = useState(true)
  const [tab, setTab] = useState<Tab>('home')
  const [accountPage, setAccountPage] = useState<AccountPage>('menu')

  useEffect(() => {
    if (!getToken()) {
      setBoot(false)
      return
    }
    api.desk().then(setDesk).catch(() => clearToken()).finally(() => setBoot(false))
  }, [])

  useEffect(() => {
    if (!desk) return
    let connection: HubConnection | null = null
    let cancelled = false
    const refresh = () => {
      api.desk().then(setDesk).catch(() => {})
    }
    ;(async () => {
      try {
        connection = createDeskConnection()
        await startDeskHub(connection, refresh, emitDeskChat)
        if (cancelled) {
          await stopDeskHub(connection)
          return
        }
        const deviceToken = localStorage.getItem('yp-device-token') || crypto.randomUUID()
        localStorage.setItem('yp-device-token', deviceToken)
        await api.registerDevice(deviceToken, 'Web').catch(() => {})
      } catch {
        /* polling still covers desk */
      }
    })()
    const timer = window.setInterval(refresh, 12000)
    return () => {
      cancelled = true
      window.clearInterval(timer)
      void stopDeskHub(connection)
    }
  }, [desk?.customerId])

  if (boot) {
    return (
      <div className="login">
        <LoginBrandPanel
          kicker="Getting ready"
          title="Ya! Pasakay"
          description="Loading your ride…"
        />
      </div>
    )
  }
  if (!desk) return <AuthScreen onReady={setDesk} />
  if (desk.needsMobile) return <CompleteMobile desk={desk} onDesk={setDesk} />

  return (
    <RideApp
      desk={desk}
      tab={tab}
      onTab={(next) => {
        setTab(next)
        if (next === 'account') setAccountPage('menu')
      }}
      accountPage={accountPage}
      onAccountPage={setAccountPage}
      onDesk={setDesk}
    />
  )
}

function RideApp({
  desk,
  tab,
  onTab,
  accountPage,
  onAccountPage,
  onDesk,
}: {
  desk: Desk
  tab: Tab
  onTab: (tab: Tab) => void
  accountPage: AccountPage
  onAccountPage: (page: AccountPage) => void
  onDesk: (desk: Desk | null) => void
}) {
  return (
    <div className="app">
      <Home
        desk={desk}
        tab={tab}
        onTab={onTab}
        accountPage={accountPage}
        onAccountPage={onAccountPage}
        onDesk={onDesk}
        onLogout={() => {
          clearToken()
          onDesk(null)
        }}
      />
    </div>
  )
}

function Home({
  desk,
  tab,
  onTab,
  accountPage,
  onAccountPage,
  onDesk,
  onLogout,
}: {
  desk: Desk
  tab: Tab
  onTab: (tab: Tab) => void
  accountPage: AccountPage
  onAccountPage: (page: AccountPage) => void
  onDesk: (desk: Desk) => void
  onLogout: () => void
}) {
  const mapEl = useRef<HTMLDivElement>(null)
  const mapRef = useRef<MapHandle | null>(null)
  const mapsRef = useRef<Awaited<ReturnType<typeof loadGoogleMaps>> | null>(null)
  const markers = useRef<MarkerHandle[]>([])
  const overlays = useRef<OverlayHandle[]>([])
  const routeRef = useRef<DirectionsRendererHandle | null>(null)
  const [pickup, setPickup] = useState<Stop | null>(null)
  const [dropoff, setDropoff] = useState<Stop | null>(null)
  const [vehicle, setVehicle] = useState<VehicleType>('Motorcycle')
  const [payment, setPayment] = useState<PaymentMethod>('Cash')
  const [paymentRef, setPaymentRef] = useState('')
  const [quotes, setQuotes] = useState<Record<VehicleType, Quote | null>>({ Motorcycle: null, Tricycle: null })
  const [quoting, setQuoting] = useState(false)
  const [coverageHint, setCoverageHint] = useState(false)
  const [searchFor, setSearchFor] = useState<SearchTarget>(null)
  const [query, setQuery] = useState('')
  const [hints, setHints] = useState<Prediction[]>([])
  const [geoHits, setGeoHits] = useState<StopResult[]>([])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [sosBusy, setSosBusy] = useState(false)
  const [canInstall, setCanInstall] = useState(false)
  const [installed, setInstalled] = useState(() => isAppInstalled())
  const [installNote, setInstallNote] = useState('')
  const [showQr, setShowQr] = useState(false)
  const installPrompt = useRef<BeforeInstallPromptEvent | null>(null)
  const locateGen = useRef(0)
  const [locating, setLocating] = useState(false)
  const [mapReady, setMapReady] = useState(false)
  const [theme, setThemeState] = useState<Theme>(readTheme)
  const trip = desk.activeTrip
  const hail = desk.hailedRider
  const pendingRate = usePendingRating(desk)
  const noOperator = useNoOperatorNotice(
    pickup,
    dropoff,
    tab === 'home' && !trip && !pendingRate.trip,
    coverageHint,
  )
  const searchForRef = useRef<SearchTarget>(searchFor)
  searchForRef.current = searchFor
  const tripRef = useRef(trip)
  tripRef.current = trip
  const pickupRef = useRef(pickup)
  pickupRef.current = pickup
  const bootGpsDone = useRef(false)

  useEffect(() => {
    mapRef.current?.setOptions({ colorScheme: theme === 'dark' ? 'DARK' : 'LIGHT' })
  }, [theme])

  const toggleTheme = (next: Theme) => {
    setTheme(next)
    setThemeState(next)
  }
  const lastPickup = desk.recent.find((item) => item.pickupLat && item.pickupLng)
  const mapCenter = pickup
    ?? (lastPickup?.pickupLat && lastPickup.pickupLng
      ? { lat: lastPickup.pickupLat, lng: lastPickup.pickupLng }
      : null)
    ?? (desk.mapLat && desk.mapLng ? { lat: desk.mapLat, lng: desk.mapLng } : PH)

  useEffect(() => {
    if (isStandaloneApp()) markAppInstalled()
    setInstalled(isAppInstalled())
    const onPrompt = (event: BeforeInstallPromptEvent) => {
      event.preventDefault()
      installPrompt.current = event
      localStorage.removeItem(INSTALLED_KEY)
      setInstalled(false)
      setCanInstall(true)
    }
    const onInstalled = () => {
      installPrompt.current = null
      setCanInstall(false)
      markAppInstalled()
      setInstalled(true)
      setInstallNote('')
    }
    window.addEventListener('beforeinstallprompt', onPrompt)
    window.addEventListener('appinstalled', onInstalled)
    return () => {
      window.removeEventListener('beforeinstallprompt', onPrompt)
      window.removeEventListener('appinstalled', onInstalled)
    }
  }, [])

  useEffect(() => {
    let cancelled = false
    async function bootMap() {
      try {
        const { googleMapsBrowserKey: key } = await api.mapsConfig()
        const maps = await loadGoogleMaps(key)
        if (cancelled || !mapEl.current) return
        mapsRef.current = maps
        const map = new maps.Map(mapEl.current, {
          center: mapCenter,
          zoom: 14,
          disableDefaultUI: true,
          clickableIcons: false,
          gestureHandling: 'greedy',
          colorScheme: theme === 'dark' ? 'DARK' : 'LIGHT',
          styles: MAP_STYLE,
          padding: mapChromePadding(),
        })
        mapRef.current = map
        map.addListener('click', (event) => {
          if (tripRef.current) return
          const loc = event.latLng
          if (!loc) return
          const lat = loc.lat()
          const lng = loc.lng()
          const target = searchForRef.current ?? 'dropoff'
          void reverseGeocode(maps, lat, lng).then((details) => {
            applyStop(target, { label: details.split(',')[0] || 'Pinned location', details, lat, lng })
          })
        })
        if (!cancelled) setMapReady(true)
      } catch {
        if (!cancelled) setError('Map failed to load. Search a place or try again.')
      }
    }
    void bootMap()
    return () => { cancelled = true }
  }, [])

  useEffect(() => {
    if (!mapReady || trip || bootGpsDone.current) return
    bootGpsDone.current = true
    let cancelled = false
    const gen = ++locateGen.current
    setLocating(true)
    setError('')

    void (async () => {
      try {
        const pos = await readBootGps((fix) => {
          if (cancelled || gen !== locateGen.current || tripRef.current || pickupRef.current) return
          showOnMap({ lat: fix.coords.latitude, lng: fix.coords.longitude }, 16)
        })
        if (cancelled || gen !== locateGen.current || tripRef.current || pickupRef.current) return
        const here = { lat: pos.coords.latitude, lng: pos.coords.longitude }
        showOnMap(here, 16)
        applyStop('pickup', { label: 'Getting address…', details: 'Current location', lat: here.lat, lng: here.lng })
        const maps = mapsRef.current
        const details = maps ? await reverseGeocode(maps, here.lat, here.lng) : 'Current location'
        if (cancelled || gen !== locateGen.current || tripRef.current) return
        applyStop('pickup', { label: addressLabel(details), details, lat: here.lat, lng: here.lng })
      } catch (err) {
        bootGpsDone.current = false
        if (!cancelled && gen === locateGen.current && !tripRef.current && !pickupRef.current) {
          const message = err instanceof GeolocationPositionError && err.code === err.PERMISSION_DENIED
            ? 'Allow location access to set pickup automatically, or search an address.'
            : err instanceof Error
              ? err.message
              : 'Could not get your location. Tap pickup to search or use the locate button.'
          setError(message)
        }
      } finally {
        if (!cancelled && gen === locateGen.current) setLocating(false)
      }
    })()

    return () => { cancelled = true }
  }, [mapReady, trip])

  useEffect(() => {
    if (!trip) return
    return watchTripGps()
  }, [trip?.id])

  useEffect(() => {
    const maps = mapsRef.current
    const map = mapRef.current
    if (!maps || !map) return
    try {
    markers.current.forEach((m) => m.setMap(null))
    markers.current = []
    overlays.current.forEach((o) => o.setMap(null))
    overlays.current = []

    const addPin = (
      position: { lat: number; lng: number },
      kind: 'pickup' | 'dropoff',
      draggable: boolean,
      onDrag?: (stop: Stop) => void,
    ) => {
      overlays.current.push(pulseStopPin(maps, map, position, kind))
      const marker = new maps.Marker({
        map,
        position,
        draggable,
        icon: stopDragIcon(maps),
        zIndex: 6,
      })
      if (onDrag) {
        marker.addListener('dragend', () => {
          const pos = marker.getPosition()
          if (!pos) return
          const lat = pos.lat()
          const lng = pos.lng()
          void reverseGeocode(maps, lat, lng).then((details) => {
            onDrag({ label: details.split(',')[0] || 'Pinned location', details, lat, lng })
          })
        })
      }
      markers.current.push(marker)
    }

    const points: { lat: number; lng: number }[] = []
    const a = trip?.pickupLat && trip.pickupLng ? { lat: trip.pickupLat, lng: trip.pickupLng } : pickup
    const b = trip?.dropoffLat && trip.dropoffLng ? { lat: trip.dropoffLat, lng: trip.dropoffLng } : dropoff
    if (a) {
      addPin(a, 'pickup', !trip, (stop) => setPickup(stop))
      points.push(a)
    }
    if (b) {
      addPin(b, 'dropoff', !trip, (stop) => setDropoff(stop))
      points.push(b)
    }
    if (trip?.riderLat && trip.riderLng) {
      markers.current.push(new maps.Marker({
        map,
        position: { lat: trip.riderLat, lng: trip.riderLng },
        icon: riderIcon(maps, trip.vehicleType),
        title: trip.riderName ? `${trip.riderName} · ${trip.vehicleType}` : 'Your rider',
        zIndex: 8,
      }))
      points.push({ lat: trip.riderLat, lng: trip.riderLng })
    }

    const origin = trip?.status === 'Waiting' && trip.riderLat && trip.riderLng
      ? { lat: trip.riderLat, lng: trip.riderLng }
      : a
    const dest = trip?.status === 'Waiting' ? a : b
    if (origin && dest) {
      routeRef.current = drawDrivingRoute(maps, map, origin, dest, routeRef.current)
    } else if (routeRef.current) {
      routeRef.current.setMap(null)
      routeRef.current = null
    }

    if (points.length >= 2) {
      const bounds = new maps.LatLngBounds()
      points.forEach((p) => bounds.extend(p))
      map.setOptions({ padding: mapChromePadding() })
      map.fitBounds(bounds, mapChromePadding())
    } else if (points[0]) {
      showOnMap(points[0])
    }
    } catch {
      /* map overlay should never take down booking */
    }
  }, [pickup, dropoff, trip])

  useEffect(() => {
    if (!pickup || !dropoff || trip) {
      setQuoting(false)
      setCoverageHint(false)
      return
    }
    let ignore = false
    async function quoteOne(type: VehicleType): Promise<{ quote: Quote | null; error: string }> {
      try {
        return { quote: await api.quote(bookBody(type, pickup!, dropoff!, payment, paymentRef, hail?.riderId)), error: '' }
      } catch (err) {
        return { quote: null, error: err instanceof Error ? err.message : 'Could not quote fare.' }
      }
    }
    async function load() {
      setQuoting(true)
      try {
        const next = { Motorcycle: null, Tricycle: null } as Record<VehicleType, Quote | null>
        let quoteError = ''
        if (hail) {
          const one = await quoteOne(hail.vehicleType)
          next[hail.vehicleType] = one.quote
          quoteError = one.error
        } else {
          const [moto, trike] = await Promise.all([quoteOne('Motorcycle'), quoteOne('Tricycle')])
          next.Motorcycle = moto.quote
          next.Tricycle = trike.quote
          quoteError = moto.error || trike.error
        }
        if (ignore) return
        setQuotes(next)
        const uncovered = !next.Motorcycle && !next.Tricycle && isOperatorCoverageError(quoteError)
        setCoverageHint(uncovered)
        setError(next.Motorcycle || next.Tricycle || isOperatorCoverageError(quoteError) ? '' : quoteError)
        setVehicle((current) => {
          if (hail) return hail.vehicleType
          if (next[current]) return current
          if (next.Motorcycle) return 'Motorcycle'
          if (next.Tricycle) return 'Tricycle'
          return current
        })
      } catch (err) {
        if (!ignore) setError(err instanceof Error ? err.message : 'Could not quote fare.')
      } finally {
        if (!ignore) setQuoting(false)
      }
    }
    void load()
    return () => { ignore = true }
  }, [pickup, dropoff, payment, paymentRef, trip, hail?.riderId, hail?.vehicleType])

  useEffect(() => {
    if (hail) setShowQr(false)
  }, [hail?.riderId])

  useEffect(() => {
    if (!showQr) return
    const timer = window.setInterval(() => {
      api.desk().then(onDesk).catch(() => {})
    }, 1500)
    return () => window.clearInterval(timer)
  }, [showQr])

  useEffect(() => {
    if (!hail) return
    setVehicle(hail.vehicleType)
    if (!hail.paymentMethods.includes(payment)) {
      setPayment(hail.paymentMethods[0] ?? 'Cash')
    }
  }, [hail?.riderId])

  useEffect(() => {
    const maps = mapsRef.current
    if (!maps || !query.trim() || !searchFor) {
      setHints([])
      setGeoHits([])
      return
    }
    const near = pickup ?? (desk.mapLat && desk.mapLng ? { lat: desk.mapLat, lng: desk.mapLng } : PH)
    const handle = window.setTimeout(() => {
      searchPlaces(maps, query, near).then(setHints).catch(() => setHints([]))
      geocodeText(maps, query, near).then(setGeoHits).catch(() => setGeoHits([]))
    }, 180)
    return () => window.clearTimeout(handle)
  }, [query, searchFor, pickup])

  function showOnMap(point: { lat: number; lng: number }, zoom?: number) {
    const map = mapRef.current
    if (!map) return
    const pad = mapChromePadding()
    map.setOptions({ padding: pad })
    map.panTo(point)
    if (zoom) map.setZoom(zoom)
    if (window.innerWidth < 900) {
      const shiftY = Math.round(window.innerHeight / 2 - pinTopY())
      if (shiftY > 8) map.panBy(0, shiftY)
    }
  }

  function applyStop(target: 'pickup' | 'dropoff', stop: Stop) {
    if (target === 'pickup') setPickup(stop)
    else setDropoff(stop)
    setSearchFor(null)
    setQuery('')
    setHints([])
    setGeoHits([])
    showOnMap(stop, 16)
  }

  async function choosePrediction(item: Prediction) {
    const target = searchFor
    if (!target) return
    const maps = mapsRef.current
    try {
      if (maps) {
        const place = await placeDetails(maps, item.place_id, mapRef.current)
        applyStop(target, {
          label: item.structured_formatting?.main_text || item.description,
          details: place.address,
          lat: place.lat,
          lng: place.lng,
        })
        return
      }
    } catch {
      /* fall through to geocode */
    }
    if (maps) {
      const hits = await geocodeText(maps, item.description)
      if (hits[0]) {
        applyStop(target, hits[0])
        return
      }
    }
    setError('Could not load that place. Tap the map or search again.')
  }

  async function confirmTyped(target: 'pickup' | 'dropoff') {
    const text = query.trim()
    if (!text) return
    const maps = mapsRef.current
    if (!maps) {
      setError('Map is still loading. Tap the map or search again in a moment.')
      return
    }
    const hits = await geocodeText(maps, text, pickup ?? (desk.mapLat && desk.mapLng ? { lat: desk.mapLat, lng: desk.mapLng } : PH))
    if (hits[0]) applyStop(target, hits[0])
    else setError('No matching place. Tap the map to drop a pin.')
  }

  async function useCurrentLocation(fromPicker = false) {
    const gen = ++locateGen.current
    setLocating(true)
    setError('')
    const cached = lastKnownGps(120_000)
    if (cached) {
      showOnMap({ lat: cached.lat, lng: cached.lng }, 16)
    }
    try {
      const pos = await readPickupGps((fix) => {
        if (gen !== locateGen.current) return
        showOnMap({ lat: fix.coords.latitude, lng: fix.coords.longitude }, 16)
      })
      if (gen !== locateGen.current) return
      const here = { lat: pos.coords.latitude, lng: pos.coords.longitude }
      showOnMap(here, 16)
      applyStop('pickup', { label: 'Getting address…', details: 'Current location', lat: here.lat, lng: here.lng })
      const maps = mapsRef.current
      const details = maps ? await reverseGeocode(maps, here.lat, here.lng) : 'Current location'
      if (gen !== locateGen.current) return
      applyStop('pickup', { label: addressLabel(details), details, lat: here.lat, lng: here.lng })
    } catch (err) {
      if (gen === locateGen.current) {
        const cached = lastKnownGps(180_000)
        if (cached) {
          const here = { lat: cached.lat, lng: cached.lng }
          showOnMap(here, 16)
          const maps = mapsRef.current
          const details = maps ? await reverseGeocode(maps, here.lat, here.lng) : 'Current location'
          if (gen === locateGen.current) {
            applyStop('pickup', { label: addressLabel(details), details, lat: here.lat, lng: here.lng })
            setError('Used last known location. For best accuracy, allow GPS and try again outdoors.')
            return
          }
        }
        const message = err instanceof GeolocationPositionError && err.code === err.PERMISSION_DENIED
          ? 'Allow location access, then tap Use current location again.'
          : err instanceof Error && err.message.includes('localhost')
            ? err.message
            : 'Could not get GPS. Allow location, step outside, or search your address.'
        setError(message)
        if (fromPicker) setSearchFor('pickup')
      }
    } finally {
      if (gen === locateGen.current) setLocating(false)
    }
  }

  async function book() {
    if (!pickup || !dropoff) return
    if (hail?.isBusy) {
      setError('This rider is on another trip right now.')
      return
    }
    setBusy(true)
    setError('')
    try {
      onDesk(await api.book(bookBody(vehicle, pickup, dropoff, payment, paymentRef, hail?.riderId)))
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Could not book.'
      setError(isOperatorCoverageError(message) ? '' : message)
    } finally {
      setBusy(false)
    }
  }

  async function clearHail() {
    setError('')
    try { onDesk(await api.clearHail()) }
    catch (err) { setError(err instanceof Error ? err.message : 'Could not clear this rider.') }
  }

  async function sendSos() {
    if (!trip) {
      setError('SOS is available during an active ride.')
      return
    }
    setSosBusy(true)
    setError('')
    const cached = lastKnownGps()
    const lat = cached?.lat ?? trip.pickupLat ?? undefined
    const lng = cached?.lng ?? trip.pickupLng ?? undefined
    try {
      await api.sos(trip.id, lat, lng)
      setError('SOS sent to your operator.')
      setSosBusy(false)
      void readGps({
        goodAccuracyM: 25,
        acceptAccuracyM: 80,
        rejectAccuracyM: 250,
        waitMs: 8000,
        minSamples: 1,
      }).then((pos) => api.sos(trip.id, pos.coords.latitude, pos.coords.longitude)).catch(() => {})
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not send SOS.')
      setSosBusy(false)
    }
  }

  async function installOnPhone() {
    if (isStandaloneApp() || installed) {
      markAppInstalled()
      setInstalled(true)
      setInstallNote('Ya! Pasakay is already installed on this phone.')
      return
    }
    const pending = installPrompt.current
    if (pending) {
      await pending.prompt()
      const choice = await pending.userChoice
      if (choice.outcome === 'accepted') {
        installPrompt.current = null
        setCanInstall(false)
        markAppInstalled()
        setInstalled(true)
        setInstallNote('')
      }
      return
    }
    const android = /android/i.test(navigator.userAgent)
    setInstallNote(android
      ? 'On Chrome, tap the menu (⋮) then Install app / Add to Home screen.'
      : 'Open this page in Chrome on your Android phone, then tap the logo to install.')
  }

  const quote = quotes[vehicle]
  const searchingArea = noOperator.searching
  const canBook = !!pickup && !!dropoff && !quoting && !searchingArea && !noOperator.uncovered && !!quote && quote.riderAvailable !== false && !hail?.isBusy && (payment !== 'Other' || !!paymentRef.trim())
  const bookLabel = !pickup || !dropoff
    ? 'Choose pickup and drop-off'
    : busy || searchingArea
      ? (hail && busy ? 'Requesting…' : 'Finding a ride…')
      : quoting
        ? 'Getting fare…'
        : noOperator.uncovered
          ? `Confirm ${vehicle}`
          : !quote
          ? `Confirm ${vehicle}`
          : quote.riderAvailable === false
            ? `No ${vehicle.toLowerCase()} rider in this area`
            : hail
              ? `Request ${hail.fullName.split(' ')[0]} · ${peso(quote.fare)}`
              : `Confirm ${vehicle} · ${peso(quote.fare)} · ${kmLabel(quote.distanceKm)}`

  return (
    <>
      <div className="map" ref={mapEl} />
      <div className="hud">
        <div className="topbar">
          <div className="brand-col">
            {installed ? (
              <div className="brand-pill">
                <img src={logo} alt="" />
                <span>Ya! Pasakay</span>
              </div>
            ) : (
              <button type="button" className={`brand-pill${canInstall ? ' ready' : ''}`} onClick={() => void installOnPhone()} title="Install on my Android phone">
                <img src={logo} alt="" />
                <span>
                  Ya! Pasakay
                  <small>
                    <InstallMark />
                    Tap to install
                  </small>
                </span>
              </button>
            )}
            {installNote && <p className="install-note">{installNote}</p>}
          </div>
          {!trip && !hail && !pendingRate.trip && (
            <ShowQrButton onClick={() => setShowQr(true)} />
          )}
        </div>
        {tab === 'home' && (
          <div className="home-dock">
            <button className="locate" type="button" disabled={locating} onClick={() => void useCurrentLocation(false)} aria-label="My location">
              <LocateIcon />
            </button>
            <section className={`panel book-sheet${trip || pendingRate.trip ? ' live' : ''}`}>
            {trip ? (
              <TripPanel trip={trip} onDesk={onDesk} onError={setError} />
            ) : pendingRate.trip ? (
              <RateRidePanel
                trip={pendingRate.trip}
                onDesk={onDesk}
                onError={setError}
                onDone={pendingRate.dismiss}
              />
            ) : (
              <>
                <div className="sheet-head">
                  <h2 className="where">Where to?</h2>
                  <ThemeSwitch theme={theme} onChange={toggleTheme} />
                </div>
                {hail && (
                  <div className="hail">
                    {hail.photoUrl
                      ? <img src={mediaUrl(hail.photoUrl)} alt="" />
                      : <div className="hail-fallback">{hail.fullName.slice(0, 1)}</div>}
                    <div>
                      <b>{hail.fullName}</b>
                      <small>
                        {[hail.plateNumber, hail.vehicleModel || hail.vehicleType].filter(Boolean).join(' · ')}
                        {hail.isOnline ? '' : ' · Offline'}
                        {hail.isBusy ? ' · On a trip' : ''}
                      </small>
                    </div>
                    <div className="hail-actions">
                      {hail.phoneNumber && <a className="call" href={`tel:${hail.phoneNumber}`}>Call</a>}
                      <button type="button" className="ghost hail-clear" onClick={() => void clearHail()}>Clear</button>
                    </div>
                  </div>
                )}
                <div className="stop">
                  <div className="stop-row">
                    <span className="pin beat"><span className="dot a" /></span>
                    <button type="button" className={`addr ${searchFor === 'pickup' ? 'on' : ''}`} onClick={() => { setSearchFor('pickup'); setQuery(''); setError('') }}>
                      <small>Pickup</small>
                      {pickup?.label ?? (locating ? 'Waiting for GPS…' : 'Tap to set pickup')}
                    </button>
                  </div>
                  <div className="stop-row">
                    <span className="pin beat"><span className="dot b" /></span>
                    <button type="button" className={`addr ${searchFor === 'dropoff' ? 'on' : ''}`} onClick={() => { setSearchFor('dropoff'); setQuery(''); setError('') }}>
                      <small>Drop-off</small>
                      {dropoff?.label ?? 'Tap to set drop-off'}
                    </button>
                  </div>
                </div>
                <div className="vehicles">
                  <button type="button" disabled={!!hail && hail.vehicleType !== 'Motorcycle'} className={`vehicle ${vehicle === 'Motorcycle' ? 'on' : ''}`} onClick={() => setVehicle('Motorcycle')}>
                    <span className="icon moto"><img src={VEHICLE_ART.Motorcycle} alt="" /></span>
                    <span className="copy">
                      <b>Motorcycle</b>
                      <b className="price">{quotes.Motorcycle ? `${peso(quotes.Motorcycle.fare)} · ${kmLabel(quotes.Motorcycle.distanceKm)}` : '—'}</b>
                    </span>
                  </button>
                  <button type="button" disabled={!!hail && hail.vehicleType !== 'Tricycle'} className={`vehicle ${vehicle === 'Tricycle' ? 'on' : ''}`} onClick={() => setVehicle('Tricycle')}>
                    <span className="icon"><img src={VEHICLE_ART.Tricycle} alt="" /></span>
                    <span className="copy">
                      <b>Tricycle</b>
                      <b className="price">{quotes.Tricycle ? `${peso(quotes.Tricycle.fare)} · ${kmLabel(quotes.Tricycle.distanceKm)}` : '—'}</b>
                    </span>
                  </button>
                </div>
                <PaymentBar
                  payment={payment}
                  refNo={paymentRef}
                  allowed={hail?.paymentMethods}
                  onPayment={(method) => {
                    setPayment(method)
                    if (method === 'Cash') setPaymentRef('')
                  }}
                  onRefNo={setPaymentRef}
                />
                {error && !isOperatorCoverageError(error) && <p className="error">{error}</p>}
                <NoOperatorNotice show={noOperator.uncovered} />
                <button className={`primary${searchingArea ? ' searching pulse' : ''}`} disabled={busy || !canBook} onClick={() => void book()}>
                  {bookLabel}
                </button>
              </>
            )}
            {error && trip && <p className="error">{error}</p>}
            </section>
          </div>
        )}
        {tab === 'booking' && (
          <section className="panel page-panel">
            <BookingScreen desk={desk} onDesk={onDesk} />
          </section>
        )}
        {tab === 'schedule' && (
          <section className="panel page-panel">
            <ScheduleScreen
              desk={desk}
              onDesk={onDesk}
              pickup={pickup}
              dropoff={dropoff}
              onPickPickup={() => { setSearchFor('pickup'); setQuery(''); setError('') }}
              onPickDropoff={() => { setSearchFor('dropoff'); setQuery(''); setError('') }}
            />
          </section>
        )}
        {tab === 'account' && (
          <section className="panel page-panel">
            <AccountHub desk={desk} page={accountPage} onPage={onAccountPage} onDesk={onDesk} onLogout={onLogout} />
          </section>
        )}
        <nav className="nav">
          <button className={tab === 'home' ? 'on' : ''} onClick={() => onTab('home')}>
            <span className="ico"><HomeIcon /></span>
            Home
          </button>
          <button className={tab === 'booking' ? 'on' : ''} onClick={() => onTab('booking')}>
            <span className="ico"><BookingIcon /></span>
            Booking
          </button>
          <button
            type="button"
            className="nav-sos"
            disabled={sosBusy}
            onClick={() => void sendSos()}
            aria-label="SOS"
          >
            <span className="ico"><SosIcon /></span>
            {sosBusy ? '…' : 'SOS'}
          </button>
          <button className={tab === 'schedule' ? 'on' : ''} onClick={() => onTab('schedule')}>
            <span className="ico"><ScheduleIcon /></span>
            Schedule
          </button>
          <button className={tab === 'account' ? 'on' : ''} onClick={() => onTab('account')}>
            <span className="ico"><AccountIcon /></span>
            Account
          </button>
        </nav>
      </div>
      {searchFor && (
        <div className="picker">
          <button className="ghost" type="button" onClick={() => { setSearchFor(null); setQuery('') }}>Back</button>
          <h2>{searchFor === 'pickup' ? 'Set pickup' : 'Set drop-off'}</h2>
          <input
            autoFocus
            placeholder="Search a place"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void confirmTyped(searchFor) } }}
          />
          {searchFor === 'pickup' && (
            <button type="button" className="picker-item" disabled={locating} onClick={() => void useCurrentLocation(true)}>
              <b>{locating ? 'Getting your location…' : 'Use current location'}</b>
              <div className="muted">{locating ? 'Keep this screen open while GPS locks' : 'GPS pickup'}</div>
            </button>
          )}
          {geoHits.map((item) => (
            <button key={`${item.details}-${item.lat}`} className="picker-item" type="button" onClick={() => applyStop(searchFor, item)}>
              <b>{item.label}</b>
              <div className="muted">{item.details}</div>
            </button>
          ))}
          {hints.map((item) => (
            <button key={item.place_id} className="picker-item" type="button" onClick={() => void choosePrediction(item)}>
              <b>{item.structured_formatting?.main_text ?? item.description}</b>
              <div className="muted">{item.structured_formatting?.secondary_text}</div>
            </button>
          ))}
        </div>
      )}
      {showQr && !trip && (
        <ShowQrOverlay customerId={desk.customerId} onClose={() => setShowQr(false)} />
      )}
    </>
  )
}

function TripPanel({ trip, onDesk, onError }: { trip: CustomerTrip; onDesk: (desk: Desk) => void; onError: (text: string) => void }) {
  const [chatOpen, setChatOpen] = useState(false)
  const [unread, setUnread] = useState(0)
  const [shareNote, setShareNote] = useState('')
  const canView = tripCanViewChat(trip)
  const canSend = tripCanSendChat(trip)

  async function cancel() {
    try { onDesk(await api.cancel(trip.id)) }
    catch (err) { onError(err instanceof Error ? err.message : 'Could not cancel.') }
  }

  return (
    <div className="trip-panel">
      <div className="seek-row">
        {trip.status === 'Pending' && <span className="seek" aria-hidden="true" />}
        <div>
          <p className="status-title">{tripHeadline(String(trip.status))}</p>
          <p className="muted">{trip.reference} · {trip.operatorName}</p>
        </div>
        {!trip.riderName && <ShareTripButton trip={trip} onNote={setShareNote} compact />}
      </div>
      {shareNote && <p className="share-note">{shareNote}</p>}
      {trip.riderName && (
        <div className={`rider${unread > 0 && !chatOpen ? ' has-chat' : ''}`}>
          {trip.riderPhotoUrl
            ? <img className="avatar lg" src={mediaUrl(trip.riderPhotoUrl)} alt="" />
            : <div className="avatar lg">{trip.riderName.slice(0, 1)}</div>}
          <div className="rider-id">
            <b>{trip.riderName}</b>
            <div className="muted">{[trip.plateNumber, trip.vehicleModel].filter(Boolean).join(' · ') || trip.vehicleType}</div>
          </div>
          <div className="rider-actions">
            {canView && (
              <button
                className={`chat-btn icon-btn${unread > 0 && !chatOpen ? ' unread' : ''}`}
                type="button"
                aria-label={unread > 0 && !chatOpen ? `Chat, ${unread} new` : 'Chat'}
                title="Chat"
                onClick={() => { setChatOpen(true); setUnread(0) }}
              >
                <ChatIcon />
                {unread > 0 && !chatOpen && (
                  <span className="chat-badge">{unread > 9 ? '9+' : unread}</span>
                )}
              </button>
            )}
            <ShareTripButton trip={trip} onNote={setShareNote} />
            {trip.riderPhone && (
              <a className="call icon-btn" href={`tel:${trip.riderPhone}`} aria-label="Call rider" title="Call">
                <CallIcon />
              </a>
            )}
          </div>
        </div>
      )}
      <div className="stop">
        <div className="stop-row">
          <span className="pin beat"><span className="dot a" /></span>
          <div className="addr"><small>Pickup</small>{trip.pickup}</div>
        </div>
        <div className="stop-row">
          <span className="pin beat"><span className="dot b" /></span>
          <div className="addr"><small>Drop-off</small>{trip.dropoff}</div>
        </div>
      </div>
      <p className="fareline"><b>{peso(trip.fare)}</b>{kmLabel(trip.distanceKm) ? ` · ${kmLabel(trip.distanceKm)}` : ''} · {trip.vehicleType} · {paymentLabel(trip.paymentMethod, trip.paymentMethodOther)}</p>
      {trip.canCancel && (
        <div className="actions">
          <button className="danger" onClick={() => void cancel()}>Cancel ride</button>
        </div>
      )}
      {canView && !trip.riderName && (
        <div className="actions">
          <button
            className={`chat-btn icon-btn${unread > 0 ? ' unread' : ''}`}
            type="button"
            aria-label={unread > 0 ? `Chat, ${unread} new` : 'Chat'}
            title="Chat"
            onClick={() => { setChatOpen(true); setUnread(0) }}
          >
            <ChatIcon />
            {unread > 0 && <span className="chat-badge">{unread > 9 ? '9+' : unread}</span>}
          </button>
        </div>
      )}
      {canView && (
        <TripChatPanel
          tripId={trip.id}
          open={chatOpen}
          onClose={() => setChatOpen(false)}
          onError={onError}
          onUnread={setUnread}
          canSend={canSend}
        />
      )}
    </div>
  )
}

function pinTopY() {
  const topbar = document.querySelector('.topbar')
  const sheet = document.querySelector('.home-dock .panel')
  const top = topbar instanceof HTMLElement ? topbar.getBoundingClientRect().bottom + 28 : 96
  const ceiling = sheet instanceof HTMLElement
    ? sheet.getBoundingClientRect().top - 48
    : window.innerHeight * 0.42
  return Math.round(Math.min(top, Math.max(72, ceiling)))
}

function mapChromePadding() {
  if (window.innerWidth >= 900) {
    return { top: 96, right: 24, bottom: 24, left: 448 }
  }
  const sheet = document.querySelector('.home-dock .panel')
  const topbar = document.querySelector('.topbar')
  const nav = document.querySelector('.nav')
  const top = topbar instanceof HTMLElement ? Math.round(topbar.getBoundingClientRect().bottom + 12) : 80
  const bottom = sheet instanceof HTMLElement
    ? Math.round(window.innerHeight - sheet.getBoundingClientRect().top + 12)
    : nav instanceof HTMLElement
      ? Math.round(window.innerHeight - nav.getBoundingClientRect().top + 12)
      : 320
  return { top, right: 16, bottom, left: 16 }
}

function bookBody(vehicle: VehicleType, pickup: Stop, dropoff: Stop, payment: PaymentMethod, refNo = '', riderId?: string): BookBody {
  return {
    vehicleType: vehicle,
    pickupBarangayId: pickup.barangayId,
    pickupDetails: pickup.details || pickup.label,
    pickupLat: pickup.lat,
    pickupLng: pickup.lng,
    dropoffBarangayId: dropoff.barangayId,
    dropoffDetails: dropoff.details || dropoff.label,
    dropoffLat: dropoff.lat,
    dropoffLng: dropoff.lng,
    paymentMethod: payment,
    paymentMethodOther: payment === 'Cash' ? undefined : (refNo.trim() || undefined),
    riderId,
  }
}

const INSTALLED_KEY = 'yp-installed'

function isStandaloneApp() {
  return window.matchMedia('(display-mode: standalone)').matches
    || ('standalone' in navigator && Boolean((navigator as Navigator & { standalone?: boolean }).standalone))
}

function isAppInstalled() {
  return isStandaloneApp() || localStorage.getItem(INSTALLED_KEY) === '1'
}

function markAppInstalled() {
  localStorage.setItem(INSTALLED_KEY, '1')
}

function InstallMark() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M12 4v11M7 11l5 5 5-5" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M5 19h14" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" />
    </svg>
  )
}

function LocateIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="3" fill="#e30613" />
      <path d="M12 3v3M12 18v3M3 12h3M18 12h3" stroke="#16181d" strokeWidth="2" strokeLinecap="round" />
      <circle cx="12" cy="12" r="7" stroke="#16181d" strokeWidth="2" />
    </svg>
  )
}

function navStroke() {
  return { fill: 'none', stroke: 'currentColor', strokeWidth: 1.8, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const }
}

function HomeIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 11.5 12 4l8 7.5V20a1 1 0 0 1-1 1h-5v-6H10v6H5a1 1 0 0 1-1-1z" {...navStroke()} />
    </svg>
  )
}

function BookingIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M7 4h10a2 2 0 0 1 2 2v14l-7-3-7 3V6a2 2 0 0 1 2-2z" {...navStroke()} />
    </svg>
  )
}

function ScheduleIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true">
      <rect x="4" y="5" width="16" height="15" rx="2" {...navStroke()} />
      <path d="M8 3v4M16 3v4M4 10h16" {...navStroke()} />
    </svg>
  )
}

function SosIcon() {
  return (
    <svg width="18" height="16" viewBox="0 0 24 22" aria-hidden="true">
      <path d="M12 1.4 1.2 20.6h21.6L12 1.4z" fill="currentColor" />
      <path d="M12 8.2v6.2" stroke="#e30613" strokeWidth="2.2" strokeLinecap="round" />
      <circle cx="12" cy="17.2" r="1.25" fill="#e30613" />
    </svg>
  )
}

function AccountIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="12" cy="8" r="3.2" {...navStroke()} />
      <path d="M5 19c1.4-3.2 3.8-4.8 7-4.8S17.6 15.8 19 19" {...navStroke()} />
    </svg>
  )
}

function ChatIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M5 5.2h14a1.8 1.8 0 0 1 1.8 1.8V14a1.8 1.8 0 0 1-1.8 1.8H9.2L4.2 20V7a1.8 1.8 0 0 1 1.8-1.8z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
    </svg>
  )
}

function CallIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M7.4 3.6h2.8c.7 0 1.2.5 1.3 1.1l.5 2.4c.1.6-.1 1.1-.6 1.5l-1.4 1.1c1.7 3.1 3.9 5.2 7.1 6.7l1.2-1.5c.4-.5 1-.6 1.6-.5l2.4.5c.7.1 1.1.7 1.1 1.4v2.6c0 .8-.7 1.4-1.5 1.3C13.4 19.8 4.4 11.2 4 5.1c-.1-.8.5-1.5 1.3-1.5h2.1z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
    </svg>
  )
}

const MAP_STYLE = [
  { featureType: 'poi', stylers: [{ visibility: 'off' }] },
  { featureType: 'transit', stylers: [{ visibility: 'off' }] },
]
