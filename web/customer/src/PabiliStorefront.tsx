import { useEffect, useMemo, useRef, useState } from 'react'
import { api, mediaUrl, PaymentMethod, Desk, PabiliOrderDetail } from './api'
import { AccountHub, AccountPage } from './account-screens'
import {
  drawDrivingRoute,
  loadGoogleMaps,
  MapHandle,
  DirectionsRendererHandle,
  MarkerHandle,
  reverseGeocode,
  searchPlaces,
  placeDetails,
  geocodeText,
  Prediction,
} from './maps'
import { lastKnownGps, readGps, readPickupGps } from './gps'

type View = 'home' | 'store' | 'cart' | 'checkout' | 'track' | 'orders' | 'account'

type MerchantCard = {
  id: string
  name: string
  address: string
  logoUrl: string | null
  coverUrl: string | null
  isOpen: boolean
  latitude: number
  longitude: number
}

type AddonOption = { id: string; name: string; sellingPrice: number; basePrice: number }
type AddonGroup = { id: string; name: string; minSelect: number; maxSelect: number; options: AddonOption[] }
type ProductCard = {
  id: string
  categoryId: string | null
  categoryName: string
  name: string
  description: string
  sellingPrice: number
  imageUrl: string | null
  addonGroups: AddonGroup[]
}

type Store = {
  id: string
  name: string
  address: string
  logoUrl: string | null
  coverUrl: string | null
  isOpen: boolean
  latitude: number
  longitude: number
  categories: Array<{ id: string; name: string }>
  products: ProductCard[]
}

type CartAddon = { optionId: string; name: string; sellingPrice: number; quantity: number }
type CartLine = {
  key: string
  productId: string
  name: string
  imageUrl: string | null
  quantity: number
  unitSellingPrice: number
  addons: CartAddon[]
}

type Quote = {
  goodsSubtotal: number
  deliveryFee: number
  surchargeTotal: number
  distanceKm: number
  adjustmentAmount: number
  adjustmentLabel: string
  customerTotal: number
}

type OrderDetail = PabiliOrderDetail

function peso(n: number) {
  return `₱${n.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function lineTotal(line: CartLine) {
  const addons = line.addons.reduce((s, a) => s + a.sellingPrice * a.quantity, 0)
  return (line.unitSellingPrice + addons) * line.quantity
}

const FALLBACK_LAT = 14.5995
const FALLBACK_LNG = 120.9842
const HOME_CATEGORIES = ['Food', 'Groceries', 'Gadgets', 'Drinks', 'Pharmacy', 'Pets'] as const
const PABILI_CART_KEY = 'yapasakay-pabili-cart'

function readPersistedCart(): { storeId: string | null; lines: CartLine[] } {
  try {
    const raw = sessionStorage.getItem(PABILI_CART_KEY)
    if (!raw) return { storeId: null, lines: [] }
    const parsed = JSON.parse(raw) as { storeId?: unknown; lines?: unknown }
    if (!Array.isArray(parsed.lines)) return { storeId: null, lines: [] }
    return {
      storeId: typeof parsed.storeId === 'string' ? parsed.storeId : null,
      lines: parsed.lines as CartLine[],
    }
  } catch {
    return { storeId: null, lines: [] }
  }
}

function writePersistedCart(storeId: string | null, lines: CartLine[]) {
  try {
    if (!lines.length) {
      sessionStorage.removeItem(PABILI_CART_KEY)
      return
    }
    sessionStorage.setItem(PABILI_CART_KEY, JSON.stringify({ storeId, lines }))
  } catch {
    /* ignore quota / private mode */
  }
}

function shuffleAds<T>(items: T[]) {
  const next = [...items]
  for (let i = next.length - 1; i > 0; i -= 1) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[next[i], next[j]] = [next[j], next[i]]
  }
  return next
}

type AdCard = { id: string; title: string; imageUrl: string | null; redirectUrl: string }
type PayMethodCard = { method: PaymentMethod; label: string; qrImageUrl: string | null; sortOrder: number }
type SuggestMerchant = { id: string; name: string; address: string; logoUrl: string | null }
type SuggestProduct = {
  id: string
  merchantId: string
  merchantName: string
  name: string
  sellingPrice: number
  imageUrl: string | null
}

export function PabiliStorefront({
  desk,
  mapLat,
  mapLng,
  onSwitchToPasakay,
  onDesk,
  onLogout,
}: {
  brandName?: string
  brandLogo?: string
  desk: Desk
  places?: unknown
  mapLat: number | null
  mapLng: number | null
  onSwitchToPasakay: () => void
  onDesk: (desk: Desk) => void
  onLogout: () => void
}) {
  const [view, setView] = useState<View>('home')
  const [accountPage, setAccountPage] = useState<AccountPage>('menu')
  const [search, setSearch] = useState('')
  const [merchants, setMerchants] = useState<MerchantCard[]>([])
  const [popularProducts, setPopularProducts] = useState<Array<{
    id: string
    merchantId: string
    merchantName: string
    name: string
    description: string
    sellingPrice: number
    imageUrl: string | null
    merchantOpen: boolean
  }>>([])
  const [ads, setAds] = useState<AdCard[]>([])
  const [payMethods, setPayMethods] = useState<PayMethodCard[]>([{ method: 'Cash', label: 'Cash', qrImageUrl: null, sortOrder: 0 }])
  const [suggestMerchants, setSuggestMerchants] = useState<SuggestMerchant[]>([])
  const [suggestProducts, setSuggestProducts] = useState<SuggestProduct[]>([])
  const [suggestOpen, setSuggestOpen] = useState(false)
  const suggestTimer = useRef<number | null>(null)
  const [store, setStore] = useState<Store | null>(null)
  const [cart, setCart] = useState<CartLine[]>(() => readPersistedCart().lines)
  const [gps, setGps] = useState<{ lat: number; lng: number }>(() => {
    const cached = lastKnownGps()
    if (cached) return { lat: cached.lat, lng: cached.lng }
    if (mapLat != null && mapLng != null) return { lat: mapLat, lng: mapLng }
    return { lat: FALLBACK_LAT, lng: FALLBACK_LNG }
  })
  const [dropoffLabel, setDropoffLabel] = useState('Current location')
  const [locating, setLocating] = useState(false)
  const [locationPickerOpen, setLocationPickerOpen] = useState(false)
  const [locationQuery, setLocationQuery] = useState('')
  const [locationHints, setLocationHints] = useState<Prediction[]>([])
  const [locationHits, setLocationHits] = useState<Array<{ label: string; details: string; lat: number; lng: number }>>([])
  const mapsRef = useRef<Awaited<ReturnType<typeof loadGoogleMaps>> | null>(null)
  const locateGen = useRef(0)
  const locationTimer = useRef<number | null>(null)
  const [quote, setQuote] = useState<Quote | null>(null)
  const [order, setOrder] = useState<OrderDetail | null>(null)
  const [orders, setOrders] = useState<OrderDetail[]>([])
  const [sheetProduct, setSheetProduct] = useState<ProductCard | null>(null)
  const [sheetQty, setSheetQty] = useState(1)
  const [sheetPicks, setSheetPicks] = useState<Record<string, string[]>>({})
  const [payment, setPayment] = useState<PaymentMethod>('Cash')
  const [categoryFilter, setCategoryFilter] = useState<string | null>(null)
  const [storeSearch, setStoreSearch] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const lat = gps.lat
  const lng = gps.lng
  const cartCount = cart.reduce((s, x) => s + x.quantity, 0)
  const cartGoods = useMemo(() => cart.reduce((s, x) => s + lineTotal(x), 0), [cart])
  const storeCategories = store?.categories ?? []

  useEffect(() => {
    writePersistedCart(store?.id ?? readPersistedCart().storeId, cart)
  }, [cart, store?.id])

  useEffect(() => {
    const persisted = readPersistedCart()
    if (!persisted.storeId || !persisted.lines.length) return
    let dead = false
    void (async () => {
      try {
        const row = await api.pabiliStore(persisted.storeId!)
        if (!dead) setStore(row)
      } catch {
        /* keep cart lines; store can be reopened */
      }
    })()
    return () => {
      dead = true
    }
  }, [])

  useEffect(() => {
    let dead = false
    void (async () => {
      try {
        const pos = await readGps({ waitMs: 12_000, minSamples: 1 })
        if (dead) return
        const next = { lat: pos.coords.latitude, lng: pos.coords.longitude }
        setGps(next)
        try {
          const maps = await ensureMaps()
          const label = await reverseGeocode(maps, next.lat, next.lng)
          if (!dead) setDropoffLabel(label)
        } catch {
          if (!dead) setDropoffLabel('Current location')
        }
      } catch {
        /* keep last known / fallback */
      }
    })()
    return () => {
      dead = true
    }
  }, [])

  async function ensureMaps() {
    if (mapsRef.current) return mapsRef.current
    const { googleMapsBrowserKey } = await api.mapsConfig()
    const maps = await loadGoogleMaps(googleMapsBrowserKey)
    mapsRef.current = maps
    return maps
  }

  async function applyLocation(next: { lat: number; lng: number }, label: string) {
    setGps(next)
    setDropoffLabel(label)
    setLocationPickerOpen(false)
    setLocationQuery('')
    setLocationHints([])
    setLocationHits([])
    setError('')
  }

  async function useCurrentLocation() {
    const gen = ++locateGen.current
    setLocating(true)
    setError('')
    try {
      const pos = await readPickupGps()
      if (gen !== locateGen.current) return
      const next = { lat: pos.coords.latitude, lng: pos.coords.longitude }
      let label = 'Current location'
      try {
        const maps = await ensureMaps()
        label = await reverseGeocode(maps, next.lat, next.lng)
      } catch {
        /* keep fallback label */
      }
      if (gen !== locateGen.current) return
      await applyLocation(next, label)
    } catch (e) {
      if (gen === locateGen.current) {
        setError(e instanceof Error ? e.message : 'Could not get your location.')
      }
    } finally {
      if (gen === locateGen.current) setLocating(false)
    }
  }

  function openLocationPicker() {
    setLocationPickerOpen(true)
    setLocationQuery('')
    setLocationHints([])
    setLocationHits([])
    setError('')
  }

  useEffect(() => {
    if (!locationPickerOpen) return
    if (locationTimer.current != null) window.clearTimeout(locationTimer.current)
    const term = locationQuery.trim()
    if (term.length < 2) {
      setLocationHints([])
      setLocationHits([])
      return
    }
    locationTimer.current = window.setTimeout(() => {
      void (async () => {
        try {
          const maps = await ensureMaps()
          const [hints, hits] = await Promise.all([
            searchPlaces(maps, term, gps),
            geocodeText(maps, term, gps),
          ])
          setLocationHints(hints.slice(0, 8))
          setLocationHits(
            hits.slice(0, 6).map((h) => ({
              label: h.label,
              details: h.details,
              lat: h.lat,
              lng: h.lng,
            })),
          )
        } catch {
          setLocationHints([])
          setLocationHits([])
        }
      })()
    }, 250)
    return () => {
      if (locationTimer.current != null) window.clearTimeout(locationTimer.current)
    }
  }, [locationQuery, locationPickerOpen, gps.lat, gps.lng])

  async function chooseLocationPrediction(item: Prediction) {
    try {
      const maps = await ensureMaps()
      const place = await placeDetails(maps, item.place_id)
      await applyLocation({ lat: place.lat, lng: place.lng }, place.address)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not set that location.')
    }
  }

  async function chooseLocationHit(item: { label: string; details: string; lat: number; lng: number }) {
    await applyLocation({ lat: item.lat, lng: item.lng }, item.details || item.label)
  }

  async function confirmTypedLocation() {
    const term = locationQuery.trim()
    if (!term) return
    try {
      const maps = await ensureMaps()
      const hits = await geocodeText(maps, term, gps)
      if (hits[0]) {
        await applyLocation({ lat: hits[0].lat, lng: hits[0].lng }, hits[0].details || hits[0].label)
        return
      }
      setError('No matching place. Try another address.')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not find that place.')
    }
  }

  async function loadMerchants(term = search) {
    setError('')
    try {
      const [rows, popular, exclusive, payments] = await Promise.all([
        api.pabiliMerchants({ lat, lng, q: term }),
        api.pabiliPopularProducts({ lat, lng, q: term }),
        api.pabiliAds({ lat, lng }),
        api.pabiliPaymentMethods({ lat, lng }),
      ])
      setMerchants(rows)
      setPopularProducts(popular)
      setAds(shuffleAds(exclusive))
      const nextPay = payments.length
        ? payments
        : [{ method: 'Cash' as PaymentMethod, label: 'Cash', qrImageUrl: null, sortOrder: 0 }]
      setPayMethods(nextPay)
      if (!nextPay.some((p) => p.method === payment)) {
        setPayment(nextPay[0].method)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load stores.')
    }
  }

  function clearSuggest() {
    setSuggestMerchants([])
    setSuggestProducts([])
    setSuggestOpen(false)
  }

  function onSearchChange(value: string) {
    setSearch(value)
    if (suggestTimer.current != null) window.clearTimeout(suggestTimer.current)
    const term = value.trim()
    if (term.length < 1) {
      clearSuggest()
      return
    }
    suggestTimer.current = window.setTimeout(() => {
      void (async () => {
        try {
          const res = await api.pabiliSuggest({ lat, lng, q: term })
          setSuggestMerchants(res.merchants)
          setSuggestProducts(res.products)
          setSuggestOpen(res.merchants.length > 0 || res.products.length > 0)
        } catch {
          clearSuggest()
        }
      })()
    }, 220)
  }

  function applySearch(term: string) {
    setSearch(term)
    clearSuggest()
    void loadMerchants(term)
  }

  useEffect(() => {
    void loadMerchants('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lat, lng])

  async function openStore(id: string, focusProductId?: string) {
    setBusy(true)
    setError('')
    try {
      const row = await api.pabiliStore(id)
      setStore(row)
      setCategoryFilter(null)
      setStoreSearch('')
      setView('store')
      if (focusProductId) {
        const product = row.products.find((p) => p.id === focusProductId)
        if (product) openProduct(product)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Store unavailable.')
    } finally {
      setBusy(false)
    }
  }

  function openProduct(product: ProductCard) {
    setSheetProduct(product)
    setSheetQty(1)
    const picks: Record<string, string[]> = {}
    for (const g of product.addonGroups) {
      picks[g.id] = g.minSelect > 0 ? g.options.slice(0, g.minSelect).map((o) => o.id) : []
    }
    setSheetPicks(picks)
  }

  function togglePick(group: AddonGroup, optionId: string) {
    setSheetPicks((prev) => {
      const current = prev[group.id] ?? []
      const has = current.includes(optionId)
      let next = has ? current.filter((x) => x !== optionId) : [...current, optionId]
      if (group.maxSelect === 1 && !has) next = [optionId]
      if (group.maxSelect > 1 && next.length > group.maxSelect) next = [...next.slice(1), optionId]
      return { ...prev, [group.id]: next }
    })
  }

  function addSheetToCart() {
    if (!sheetProduct) return
    for (const g of sheetProduct.addonGroups) {
      const n = (sheetPicks[g.id] ?? []).length
      if (n < g.minSelect || (g.maxSelect > 0 && n > g.maxSelect)) {
        setError(`Choose ${g.minSelect}–${g.maxSelect} for ${g.name}.`)
        return
      }
    }
    const addons: CartAddon[] = []
    for (const g of sheetProduct.addonGroups) {
      for (const id of sheetPicks[g.id] ?? []) {
        const opt = g.options.find((o) => o.id === id)
        if (opt) addons.push({ optionId: opt.id, name: opt.name, sellingPrice: opt.sellingPrice, quantity: 1 })
      }
    }
    const key = `${sheetProduct.id}:${addons.map((a) => a.optionId).sort().join(',')}`
    setCart((prev) => {
      const existing = prev.find((x) => x.key === key)
      if (existing) {
        return prev.map((x) => (x.key === key ? { ...x, quantity: x.quantity + sheetQty } : x))
      }
      return [
        ...prev,
        {
          key,
          productId: sheetProduct.id,
          name: sheetProduct.name,
          imageUrl: sheetProduct.imageUrl,
          quantity: sheetQty,
          unitSellingPrice: sheetProduct.sellingPrice,
          addons,
        },
      ]
    })
    setSheetProduct(null)
    setError('')
  }

  async function refreshQuote() {
    if (!store || !cart.length) {
      setQuote(null)
      return
    }
    setBusy(true)
    setError('')
    try {
      const row = await api.pabiliQuote({
        merchantId: store.id,
        dropoffLat: lat,
        dropoffLng: lng,
        items: cart.map((c) => ({
          productId: c.productId,
          quantity: c.quantity,
          addons: c.addons.map((a) => ({ optionId: a.optionId, quantity: a.quantity })),
        })),
      })
      setQuote(row)
    } catch (e) {
      setQuote(null)
      setError(e instanceof Error ? e.message : 'Could not quote delivery.')
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    if (view === 'checkout') void refreshQuote()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view, lat, lng, cart])

  async function placeOrder() {
    if (!store || !cart.length) return
    setBusy(true)
    setError('')
    try {
      const row = await api.pabiliPlace({
        merchantId: store.id,
        dropoffAddress: dropoffLabel || 'Current location',
        dropoffLat: lat,
        dropoffLng: lng,
        paymentMethod: payment,
        items: cart.map((c) => ({
          productId: c.productId,
          quantity: c.quantity,
          addons: c.addons.map((a) => ({ optionId: a.optionId, quantity: a.quantity })),
        })),
      })
      setOrder(row)
      setCart([])
      writePersistedCart(null, [])
      setView('track')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not place order.')
    } finally {
      setBusy(false)
    }
  }

  async function loadOrders() {
    setBusy(true)
    setError('')
    try {
      const rows = await api.pabiliOrders()
      setOrders(rows)
      setView('orders')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load orders.')
    } finally {
      setBusy(false)
    }
  }

  async function openOrder(id: string) {
    setBusy(true)
    try {
      const row = await api.pabiliOrder(id)
      setOrder(row)
      setView('track')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Order not found.')
    } finally {
      setBusy(false)
    }
  }

  async function cancelOrder() {
    if (!order?.canCancel) return
    setBusy(true)
    try {
      const row = await api.pabiliCancelOrder(order.id)
      setOrder(row)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not cancel.')
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    if (view !== 'track' || !order) return
    if (order.status === 'Completed' || order.status === 'Cancelled') return
    const timer = window.setInterval(() => {
      void api.pabiliOrder(order.id).then(setOrder).catch(() => undefined)
    }, 8000)
    return () => window.clearInterval(timer)
  }, [view, order?.id, order?.status])

  const filteredProducts = useMemo(() => {
    if (!store) return []
    const term = storeSearch.trim().toLowerCase()
    return store.products.filter((p) => {
      if (categoryFilter && p.categoryId !== categoryFilter && p.categoryName !== categoryFilter) {
        return false
      }
      if (!term) return true
      return (
        p.name.toLowerCase().includes(term)
        || p.description.toLowerCase().includes(term)
        || p.categoryName.toLowerCase().includes(term)
      )
    })
  }, [store, categoryFilter, storeSearch])

  const showShellNav = view === 'home' || view === 'orders' || view === 'cart' || view === 'account'
  const cartNavOn = view === 'cart' || view === 'checkout'

  return (
    <div className="pb-shell">
      <div className={`pb-topbar${view === 'store' ? ' has-back' : ''}`}>
        {view === 'store' ? (
          <button type="button" className="pb-back-link" onClick={() => setView('home')}>
            ← Back
          </button>
        ) : null}
        <div className="pb-mode pb-mode-sm">
          <button type="button" className="pb-mode-btn" onClick={onSwitchToPasakay}>
            Pasakay
          </button>
          <button type="button" className="pb-mode-btn on" aria-current="page">
            Pabili
          </button>
        </div>
      </div>

      {view === 'home' ? (
        <header className="pb-search-bar">
          <div className="pb-loc-row">
            <p className="pb-loc-label" title={dropoffLabel}>
              <span className="muted">Deliver to</span>
              <b>{dropoffLabel}</b>
            </p>
            <button type="button" className="pb-loc-fix" onClick={openLocationPicker}>
              [Not accurate?]
            </button>
          </div>
          <div className="pb-search-wrap pb-search-with-locate">
            <input
              className="pb-search"
              placeholder="Search food, groceries, drinks…"
              value={search}
              onChange={(e) => onSearchChange(e.target.value)}
              onFocus={() => {
                if (suggestMerchants.length || suggestProducts.length) setSuggestOpen(true)
              }}
              onBlur={() => {
                window.setTimeout(() => setSuggestOpen(false), 160)
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault()
                  applySearch(search)
                }
              }}
              aria-autocomplete="list"
              aria-expanded={suggestOpen}
            />
            <button
              type="button"
              className="pb-locate-btn"
              disabled={locating}
              onClick={() => void useCurrentLocation()}
              aria-label="Use current location"
              title="Use current location"
            >
              <LocatePinIcon spinning={locating} />
            </button>
            {suggestOpen ? (
              <div className="pb-suggest" role="listbox">
                {suggestProducts.map((p) => (
                  <button
                    key={`p-${p.merchantId}-${p.id}`}
                    type="button"
                    className="pb-suggest-row"
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={() => {
                      clearSuggest()
                      void openStore(p.merchantId, p.id)
                    }}
                  >
                    <div
                      className="pb-suggest-img"
                      style={p.imageUrl ? { backgroundImage: `url(${mediaUrl(p.imageUrl)})` } : undefined}
                    />
                    <span>
                      <b>{p.name}</b>
                      <small className="muted">{p.merchantName} · {peso(p.sellingPrice)}</small>
                    </span>
                  </button>
                ))}
                {suggestMerchants.map((m) => (
                  <button
                    key={`m-${m.id}`}
                    type="button"
                    className="pb-suggest-row"
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={() => {
                      clearSuggest()
                      void openStore(m.id)
                    }}
                  >
                    <div
                      className="pb-suggest-img"
                      style={m.logoUrl ? { backgroundImage: `url(${mediaUrl(m.logoUrl)})` } : undefined}
                    />
                    <span>
                      <b>{m.name}</b>
                      <small className="muted">{m.address || 'Store'}</small>
                    </span>
                  </button>
                ))}
              </div>
            ) : null}
          </div>
          <nav className="pb-quick-cats" aria-label="Shop categories">
            {HOME_CATEGORIES.map((label) => (
              <button
                key={label}
                type="button"
                className={`pb-quick-cat${search.trim().toLowerCase() === label.toLowerCase() ? ' on' : ''}`}
                onClick={() => applySearch(label)}
              >
                {label}
              </button>
            ))}
          </nav>
        </header>
      ) : null}

      {error ? <p className="error pb-error">{error}</p> : null}

      {view === 'home' && (
        <main className="pb-main">
          <section className="pb-section">
            <div className="pb-section-head">
              <h2>Exclusive Offer</h2>
            </div>
            {ads.length ? (
              <div className="pb-offers-marquee" role="list" aria-label="Exclusive offers">
                <div
                  className="pb-offers-track"
                  style={{ animationDuration: `${Math.max(18, ads.length * 7)}s` }}
                >
                  {[...ads, ...ads].map((ad, index) => (
                    <a
                      key={`${ad.id}-${index}`}
                      className="pb-offer-card"
                      href={ad.redirectUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      role="listitem"
                      aria-label={ad.title}
                      tabIndex={index >= ads.length ? -1 : 0}
                      aria-hidden={index >= ads.length}
                    >
                      <span
                        className="pb-offer-media"
                        style={ad.imageUrl ? { backgroundImage: `url(${mediaUrl(ad.imageUrl)})` } : undefined}
                      />
                      <span className="pb-offer-title">{ad.title}</span>
                    </a>
                  ))}
                </div>
              </div>
            ) : (
              <p className="muted">No exclusive offers right now.</p>
            )}
          </section>

          <section className="pb-section">
            <div className="pb-section-head">
              <h2>Popular now</h2>
            </div>
            <div className="pb-tile-grid">
              {popularProducts.map((p) => (
                <button
                  key={`${p.merchantId}-${p.id}`}
                  type="button"
                  className="pb-tile-card"
                  onClick={() => void openStore(p.merchantId, p.id)}
                >
                  <div className="pb-tile-img">
                    {p.imageUrl ? (
                      <img src={mediaUrl(p.imageUrl)} alt="" loading="lazy" />
                    ) : null}
                  </div>
                  <div className="pb-tile-body">
                    <b>{p.name}</b>
                    <small className="muted">{p.merchantName}</small>
                    <span className="pb-price">{peso(p.sellingPrice)}</span>
                  </div>
                </button>
              ))}
              {!popularProducts.length ? <p className="muted">No products nearby yet.</p> : null}
            </div>
          </section>

          <section className="pb-section">
            <div className="pb-section-head">
              <h2>Store Near You</h2>
            </div>
            <div className="pb-store-list">
              {merchants.map((m) => (
                <button key={m.id} type="button" className="pb-store-row" onClick={() => void openStore(m.id)}>
                  <div
                    className="pb-store-row-img"
                    style={
                      m.logoUrl || m.coverUrl
                        ? { backgroundImage: `url(${mediaUrl(m.logoUrl || m.coverUrl)})` }
                        : undefined
                    }
                  />
                  <div className="pb-store-row-copy">
                    <b>{m.name}</b>
                    <br />
                    <span className="muted">{m.address || 'Location unavailable'}</span>
                  </div>
                </button>
              ))}
              {!merchants.length ? <p className="muted">No stores nearby yet.</p> : null}
            </div>
          </section>
        </main>
      )}

      {view === 'store' && store && (
        <main className="pb-main pb-store">
          <div
            className="pb-store-banner"
            style={store.coverUrl ? { backgroundImage: `url(${mediaUrl(store.coverUrl)})` } : undefined}
            role="img"
            aria-label={store.name}
          />
          <div className="pb-store-identity">
            <div
              className="pb-store-identity-img"
              style={
                store.logoUrl || store.coverUrl
                  ? { backgroundImage: `url(${mediaUrl(store.logoUrl || store.coverUrl)})` }
                  : undefined
              }
            />
            <div className="pb-store-identity-copy">
              <h2>{store.name}</h2>
              <p className="muted">{store.address || 'Location unavailable'}</p>
              <span className={store.isOpen ? 'ok' : 'bad'}>{store.isOpen ? 'Open' : 'Closed'}</span>
            </div>
          </div>
          <div className="pb-search-bar">
            <div className="pb-search-wrap">
              <input
                className="pb-search"
                placeholder={`Search in ${store.name}…`}
                value={storeSearch}
                onChange={(e) => setStoreSearch(e.target.value)}
                aria-label="Search store products"
              />
            </div>
          </div>
          {storeCategories.length > 0 ? (
            <div className="pb-cats sticky">
              <button type="button" className={`pb-chip ${!categoryFilter ? 'on' : ''}`} onClick={() => setCategoryFilter(null)}>
                All
              </button>
              {storeCategories.map((c) => (
                <button
                  key={c.id}
                  type="button"
                  className={`pb-chip ${categoryFilter === c.id ? 'on' : ''}`}
                  onClick={() => setCategoryFilter(c.id)}
                >
                  {c.name}
                </button>
              ))}
            </div>
          ) : null}
          <div className="pb-product-grid">
            {filteredProducts.map((p) => (
              <button key={p.id} type="button" className="pb-product-card" onClick={() => openProduct(p)}>
                <div
                  className="pb-product-img"
                  style={p.imageUrl ? { backgroundImage: `url(${mediaUrl(p.imageUrl)})` } : undefined}
                />
                <div className="pb-product-body">
                  <b>{p.name}</b>
                  <small className="muted">{p.description || p.categoryName}</small>
                  <span className="pb-price">{peso(p.sellingPrice)}</span>
                </div>
              </button>
            ))}
            {!filteredProducts.length ? (
              <p className="muted">
                {storeSearch.trim() ? 'No products match your search.' : 'No products available.'}
              </p>
            ) : null}
          </div>
          {cartCount > 0 ? (
            <button type="button" className="pb-cart-cta" onClick={() => setView('cart')}>
              View cart · {cartCount} · {peso(cartGoods)}
            </button>
          ) : null}
        </main>
      )}

      {(view === 'cart' || view === 'checkout') && (
        <main className="pb-main pb-checkout">
          <div className="pb-page-head">
            <button
              type="button"
              className="pb-icon-back"
              onClick={() => setView(view === 'checkout' ? 'cart' : store ? 'store' : 'home')}
              aria-label="Back"
            >
              ←
            </button>
            <div className="pb-page-copy">
              <h2>{view === 'cart' ? 'Your cart' : 'Checkout'}</h2>
              <p className="muted">
                {view === 'cart'
                  ? (cartCount ? `${cartCount} item${cartCount === 1 ? '' : 's'}` : 'No items yet')
                  : (store?.name || 'Confirm and place order')}
              </p>
            </div>
          </div>

          {cart.length ? (
            <div className="pb-cart-list">
              {cart.map((line) => (
                <article key={line.key} className="pb-line">
                  <div className="pb-line-img">
                    {line.imageUrl ? <img src={mediaUrl(line.imageUrl)} alt="" /> : null}
                  </div>
                  <div className="pb-line-copy">
                    <b>{line.name}</b>
                    <small className="muted">{peso(line.unitSellingPrice)} each</small>
                    {line.addons.length ? (
                      <small className="muted pb-line-addons">{line.addons.map((a) => a.name).join(' · ')}</small>
                    ) : null}
                    <div className="pb-qty" role="group" aria-label={`Quantity for ${line.name}`}>
                      <button
                        type="button"
                        aria-label="Decrease quantity"
                        onClick={() => setCart((c) => c.map((x) => (x.key === line.key ? { ...x, quantity: Math.max(1, x.quantity - 1) } : x)))}
                      >
                        −
                      </button>
                      <span>{line.quantity}</span>
                      <button
                        type="button"
                        aria-label="Increase quantity"
                        onClick={() => setCart((c) => c.map((x) => (x.key === line.key ? { ...x, quantity: x.quantity + 1 } : x)))}
                      >
                        +
                      </button>
                    </div>
                  </div>
                  <div className="pb-line-total">
                    <b>{peso(lineTotal(line))}</b>
                    <button
                      type="button"
                      className="pb-line-remove"
                      onClick={() => setCart((c) => c.filter((x) => x.key !== line.key))}
                    >
                      Remove
                    </button>
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <div className="pb-empty-card">
              <div className="pb-empty-ico" aria-hidden>🛒</div>
              <h3>Your cart is empty</h3>
              <p className="muted">Browse stores and add items to get started.</p>
              <button type="button" className="pb-secondary-btn" onClick={() => setView('home')}>
                Browse stores
              </button>
            </div>
          )}

          {view === 'checkout' && cart.length ? (
            <div className="pb-checkout-stack">
              <section className="pb-info-card">
                <span className="pb-info-label">Deliver to</span>
                <b>{dropoffLabel}</b>
              </section>
              <section className="pb-info-card">
                <span className="pb-info-label">Payment</span>
                <div className="pb-pay-options" role="group" aria-label="Payment method">
                  {payMethods.map((item) => (
                    <button
                      key={item.method}
                      type="button"
                      className={payment === item.method ? 'on' : ''}
                      onClick={() => setPayment(item.method)}
                    >
                      {item.label || item.method}
                    </button>
                  ))}
                </div>
                {(() => {
                  const selected = payMethods.find((p) => p.method === payment)
                  if (!selected?.qrImageUrl) return null
                  return (
                    <div className="pb-pay-qr">
                      <img src={mediaUrl(selected.qrImageUrl)} alt={`${selected.label} QR`} />
                      <small className="muted">Scan to pay via {selected.label}</small>
                    </div>
                  )
                })()}
              </section>
              {quote ? (
                <section className="pb-summary">
                  <div><span>Subtotal</span><b>{peso(quote.goodsSubtotal)}</b></div>
                  <div><span>Delivery fee</span><b>{quote.deliveryFee <= 0 ? 'Free' : peso(quote.deliveryFee)}</b></div>
                  {quote.adjustmentAmount !== 0 ? (
                    <div className="disc">
                      <span>{quote.adjustmentLabel || 'Adjustment'}</span>
                      <b>{peso(quote.adjustmentAmount)}</b>
                    </div>
                  ) : null}
                  <div className="total"><span>Total</span><b>{peso(quote.customerTotal)}</b></div>
                </section>
              ) : (
                <p className="muted pb-quote-wait">{busy ? 'Quoting…' : 'Getting delivery quote…'}</p>
              )}
              <button type="button" className="primary pb-primary" disabled={busy || !quote} onClick={() => void placeOrder()}>
                Place order{quote ? ` · ${peso(quote.customerTotal)}` : ''}
              </button>
            </div>
          ) : null}

          {view === 'cart' && cart.length ? (
            <div className="pb-cart-footer">
              <div>
                <span className="muted">Subtotal</span>
                <b>{peso(cartGoods)}</b>
              </div>
              <button type="button" className="primary pb-primary" onClick={() => setView('checkout')}>
                Checkout
              </button>
            </div>
          ) : null}
        </main>
      )}

      {view === 'orders' && (
        <main className="pb-main">
          <div className="pb-page-head">
            <h2>Orders</h2>
          </div>
          {orders.map((o) => (
            <button key={o.id} type="button" className="pb-order-row" onClick={() => void openOrder(o.id)}>
              <div>
                <b>{o.reference}</b>
                <div className="muted">{o.status} · {o.merchantName}</div>
              </div>
              <b>{peso(o.customerTotal)}</b>
            </button>
          ))}
          {!orders.length ? <p className="muted">No Pabili orders yet.</p> : null}
        </main>
      )}

      {view === 'track' && order && (
        <main className="pb-track">
          <div className="pb-page-head overlay">
            <button type="button" className="ghost" onClick={() => setView('orders')}>←</button>
            <h2>Order track</h2>
          </div>
          <OrderTrackMap order={order} />
          <div className="pb-rider-card">
            <div className="pb-rider-row">
              <div className="pb-rider-avatar">{(order.riderName ?? '?').slice(0, 1)}</div>
              <div>
                <b>{order.riderName ?? 'Finding rider…'}</b>
                <div className="muted">{order.status} · {order.reference}</div>
                <div className="muted">{order.merchantName}</div>
              </div>
              <div className="pb-rider-actions">
                {order.riderPhone ? (
                  <a className="pb-round" href={`tel:${order.riderPhone}`} aria-label="Call">📞</a>
                ) : null}
              </div>
            </div>
            <div className="pb-summary tight">
              <div><span>Goods</span><b>{peso(order.goodsSubtotal)}</b></div>
              <div><span>Delivery</span><b>{peso(order.deliveryFee)}</b></div>
              {order.adjustmentAmount !== 0 ? (
                <div className="disc"><span>{order.adjustmentLabel || 'Adjustment'}</span><b>{peso(order.adjustmentAmount)}</b></div>
              ) : null}
              <div className="total"><span>Total</span><b>{peso(order.customerTotal)}</b></div>
            </div>
            {order.canCancel ? (
              <button type="button" className="ghost" disabled={busy} onClick={() => void cancelOrder()}>
                Cancel order
              </button>
            ) : null}
          </div>
        </main>
      )}

      {view === 'account' && (
        <main className="pb-main pb-account">
          <AccountHub
            desk={desk}
            page={accountPage}
            onPage={setAccountPage}
            onDesk={onDesk}
            onLogout={onLogout}
          />
        </main>
      )}

      {showShellNav ? (
        <nav className="pb-nav">
          <button type="button" className={view === 'home' ? 'on' : ''} onClick={() => setView('home')}>
            <span>⌂</span>Home
          </button>
          <button type="button" className={view === 'orders' ? 'on' : ''} onClick={() => void loadOrders()}>
            <span>☰</span>Orders
          </button>
          <button type="button" className={cartNavOn ? 'on' : ''} onClick={() => setView('cart')}>
            <span className="pb-nav-ico"><CartNavIcon /></span>Cart{cartCount ? ` (${cartCount})` : ''}
          </button>
          <button
            type="button"
            className={view === 'account' ? 'on' : ''}
            onClick={() => {
              setAccountPage('menu')
              setView('account')
            }}
          >
            <span className="pb-nav-ico"><AccountNavIcon /></span>Account
          </button>
        </nav>
      ) : null}

      {sheetProduct ? (
        <div className="pb-sheet" role="dialog" aria-modal="true">
          <div className="pb-sheet-card">
            <button type="button" className="ghost pb-sheet-close" onClick={() => setSheetProduct(null)}>Close</button>
            <div className="pb-sheet-img">
              {sheetProduct.imageUrl ? (
                <img src={mediaUrl(sheetProduct.imageUrl)} alt="" />
              ) : null}
            </div>
            <h3>{sheetProduct.name}</h3>
            <p className="muted">{sheetProduct.description}</p>
            <p className="pb-price">{peso(sheetProduct.sellingPrice)}</p>
            {sheetProduct.addonGroups.map((g) => (
              <div key={g.id} className="pb-addon-group">
                <b>{g.name} <span className="muted">({g.minSelect}-{g.maxSelect})</span></b>
                {g.options.map((o) => {
                  const on = (sheetPicks[g.id] ?? []).includes(o.id)
                  return (
                    <label key={o.id} className={`pb-addon ${on ? 'on' : ''}`}>
                      <input type="checkbox" checked={on} onChange={() => togglePick(g, o.id)} />
                      <span>{o.name}</span>
                      <span>{peso(o.sellingPrice)}</span>
                    </label>
                  )
                })}
              </div>
            ))}
            <div className="pb-qty">
              <button type="button" onClick={() => setSheetQty((q) => Math.max(1, q - 1))}>−</button>
              <span>{sheetQty}</span>
              <button type="button" onClick={() => setSheetQty((q) => q + 1)}>+</button>
            </div>
            <button type="button" className="primary pb-primary" onClick={addSheetToCart}>Add to cart</button>
          </div>
        </div>
      ) : null}

      {locationPickerOpen ? (
        <div className="picker pb-location-picker">
          <button
            className="ghost"
            type="button"
            onClick={() => {
              setLocationPickerOpen(false)
              setLocationQuery('')
              setLocationHints([])
              setLocationHits([])
            }}
          >
            Back
          </button>
          <h2>Set delivery location</h2>
          <input
            autoFocus
            placeholder="Search a place or address"
            value={locationQuery}
            onChange={(e) => setLocationQuery(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                void confirmTypedLocation()
              }
            }}
          />
          <button type="button" className="picker-item" disabled={locating} onClick={() => void useCurrentLocation()}>
            <b>{locating ? 'Getting your location…' : 'Use current location'}</b>
            <div className="muted">{locating ? 'Keep this screen open while GPS locks' : 'GPS delivery pin'}</div>
          </button>
          {locationHits.map((item) => (
            <button
              key={`${item.details}-${item.lat}`}
              className="picker-item"
              type="button"
              onClick={() => void chooseLocationHit(item)}
            >
              <b>{item.label}</b>
              <div className="muted">{item.details}</div>
            </button>
          ))}
          {locationHints.map((item) => (
            <button
              key={item.place_id}
              className="picker-item"
              type="button"
              onClick={() => void chooseLocationPrediction(item)}
            >
              <b>{item.structured_formatting?.main_text ?? item.description}</b>
              <div className="muted">{item.structured_formatting?.secondary_text}</div>
            </button>
          ))}
        </div>
      ) : null}
    </div>
  )
}

function LocatePinIcon({ spinning }: { spinning?: boolean }) {
  return (
    <svg
      className={spinning ? 'pb-locate-spin' : undefined}
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
    >
      <circle cx="12" cy="12" r="3" fill="var(--accent)" />
      <path d="M12 3v3M12 18v3M3 12h3M18 12h3" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <circle cx="12" cy="12" r="7" stroke="currentColor" strokeWidth="2" />
    </svg>
  )
}

function navIcoStroke() {
  return {
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.8,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
  }
}

function CartNavIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 5h1.7l1.1 1.1 1.7 9.3a1.5 1.5 0 0 0 1.5 1.2h8.5a1.5 1.5 0 0 0 1.45-1.15L21 9H8.3" {...navIcoStroke()} />
      <circle cx="10.2" cy="19.4" r="1.35" fill="currentColor" />
      <circle cx="17.3" cy="19.4" r="1.35" fill="currentColor" />
    </svg>
  )
}

function AccountNavIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="12" cy="12" r="9" {...navIcoStroke()} />
      <circle cx="12" cy="10" r="3" {...navIcoStroke()} />
      <path d="M7 18.2c1.15-2.1 2.85-3.1 5-3.1s3.85 1 5 3.1" {...navIcoStroke()} />
    </svg>
  )
}

function OrderTrackMap({ order }: { order: OrderDetail }) {
  const el = useRef<HTMLDivElement>(null)
  const mapRef = useRef<MapHandle | null>(null)
  const routeRef = useRef<DirectionsRendererHandle | null>(null)
  const markers = useRef<MarkerHandle[]>([])

  useEffect(() => {
    let dead = false
    void (async () => {
      if (!el.current) return
      try {
        const { googleMapsBrowserKey } = await api.mapsConfig()
        const maps = await loadGoogleMaps(googleMapsBrowserKey)
        if (dead || !el.current) return
        const map = new maps.Map(el.current, {
          center: { lat: order.dropoffLat, lng: order.dropoffLng },
          zoom: 14,
          disableDefaultUI: true,
          gestureHandling: 'greedy',
        })
        mapRef.current = map
        markers.current.forEach((m) => m.setMap(null))
        markers.current = [
          new maps.Marker({ map, position: { lat: order.pickupLat, lng: order.pickupLng }, title: 'Pickup' }),
          new maps.Marker({ map, position: { lat: order.dropoffLat, lng: order.dropoffLng }, title: 'Drop-off' }),
        ]
        routeRef.current = drawDrivingRoute(
          maps,
          map,
          { lat: order.pickupLat, lng: order.pickupLng },
          { lat: order.dropoffLat, lng: order.dropoffLng },
          routeRef.current,
        )
      } catch {
        /* map optional */
      }
    })()
    return () => {
      dead = true
      markers.current.forEach((m) => m.setMap(null))
      markers.current = []
    }
  }, [order.id, order.pickupLat, order.pickupLng, order.dropoffLat, order.dropoffLng])

  return <div className="pb-track-map" ref={el} />
}
