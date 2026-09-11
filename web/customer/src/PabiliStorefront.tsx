import { useEffect, useMemo, useRef, useState } from 'react'
import { api, mediaUrl, Place, PaymentMethod, Desk, PabiliOrderDetail } from './api'
import { AccountHub, AccountPage } from './account-screens'
import { drawDrivingRoute, loadGoogleMaps, MapHandle, DirectionsRendererHandle, MarkerHandle } from './maps'

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

const CATEGORY_ICONS = ['🍳', '🦐', '🍔', '🍰', '🧁', '🍜', '🥗', '🥤']

export function PabiliStorefront({
  brandName,
  brandLogo,
  desk,
  places,
  mapLat,
  mapLng,
  onSwitchToPasakay,
  onDesk,
  onLogout,
}: {
  brandName: string
  brandLogo: string
  desk: Desk
  places: Place[]
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
  const [store, setStore] = useState<Store | null>(null)
  const [cart, setCart] = useState<CartLine[]>([])
  const [dropoff, setDropoff] = useState<Place | null>(places[0] ?? null)
  const [quote, setQuote] = useState<Quote | null>(null)
  const [order, setOrder] = useState<OrderDetail | null>(null)
  const [orders, setOrders] = useState<OrderDetail[]>([])
  const [sheetProduct, setSheetProduct] = useState<ProductCard | null>(null)
  const [sheetQty, setSheetQty] = useState(1)
  const [sheetPicks, setSheetPicks] = useState<Record<string, string[]>>({})
  const [payment, setPayment] = useState<PaymentMethod>('Cash')
  const [categoryFilter, setCategoryFilter] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const lat = dropoff?.lat ?? mapLat ?? 14.5995
  const lng = dropoff?.lng ?? mapLng ?? 120.9842
  const barangayId = dropoff?.barangayId
  const cartCount = cart.reduce((s, x) => s + x.quantity, 0)
  const cartGoods = useMemo(() => cart.reduce((s, x) => s + lineTotal(x), 0), [cart])

  const categoryChips = useMemo(() => {
    const names = new Map<string, string>()
    for (const m of merchants) {
      // placeholder chips until store open; filled from store when browsing
      names.set(m.id, m.name)
    }
    return Array.from(names.entries()).slice(0, 0)
  }, [merchants])

  const storeCategories = store?.categories ?? []

  async function loadMerchants(term = search) {
    setError('')
    try {
      const rows = await api.pabiliMerchants({ lat, lng, barangayId, q: term })
      setMerchants(rows)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load stores.')
    }
  }

  useEffect(() => {
    void loadMerchants('')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dropoff?.barangayId, lat, lng])

  useEffect(() => {
    if (!places.length || dropoff) return
    setDropoff(places[0])
  }, [places, dropoff])

  async function openStore(id: string) {
    setBusy(true)
    setError('')
    try {
      const row = await api.pabiliStore(id)
      setStore(row)
      setCategoryFilter(null)
      setView('store')
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
    if (!store || !cart.length || !dropoff) {
      setQuote(null)
      return
    }
    setBusy(true)
    setError('')
    try {
      const row = await api.pabiliQuote({
        merchantId: store.id,
        dropoffLat: dropoff.lat,
        dropoffLng: dropoff.lng,
        dropoffBarangayId: dropoff.barangayId,
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
  }, [view, dropoff?.barangayId, cart])

  async function placeOrder() {
    if (!store || !dropoff || !cart.length) return
    setBusy(true)
    setError('')
    try {
      const row = await api.pabiliPlace({
        merchantId: store.id,
        dropoffAddress: dropoff.details || dropoff.label,
        dropoffLat: dropoff.lat,
        dropoffLng: dropoff.lng,
        dropoffBarangayId: dropoff.barangayId,
        paymentMethod: payment,
        items: cart.map((c) => ({
          productId: c.productId,
          quantity: c.quantity,
          addons: c.addons.map((a) => ({ optionId: a.optionId, quantity: a.quantity })),
        })),
      })
      setOrder(row)
      setCart([])
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
    if (!categoryFilter) return store.products
    return store.products.filter((p) => p.categoryId === categoryFilter || p.categoryName === categoryFilter)
  }, [store, categoryFilter])

  const popularMerchants = merchants.slice(0, 6)
  const showShellNav = view === 'home' || view === 'orders' || view === 'cart' || view === 'account'
  const cartNavOn = view === 'cart' || view === 'checkout'

  return (
    <div className="pb-shell">
      <div className="pb-mode">
        <button type="button" className="pb-mode-btn" onClick={onSwitchToPasakay}>
          Pasakay
        </button>
        <button type="button" className="pb-mode-btn on" aria-current="page">
          Pabili
        </button>
      </div>
      <p className="pb-brand-hint">{brandName}</p>

      {(view === 'home' || view === 'store' || view === 'cart' || view === 'checkout') && (
        <header className="pb-hero">
          <div className="pb-hero-row">
            <label className="pb-loc">
              <span className="pb-loc-ico" aria-hidden>📍</span>
              <select
                value={dropoff?.barangayId ?? ''}
                onChange={(e) => {
                  const next = places.find((p) => p.barangayId === e.target.value) ?? null
                  setDropoff(next)
                }}
              >
                {places.length === 0 ? <option value="">Set a saved place</option> : null}
                {places.map((p) => (
                  <option key={p.barangayId} value={p.barangayId}>
                    {p.label}
                  </option>
                ))}
              </select>
            </label>
            <button type="button" className="pb-bag" onClick={() => setView('cart')} aria-label="Cart">
              🛒
              {cartCount > 0 ? <span className="pb-bag-dot">{cartCount}</span> : null}
            </button>
            <button
              type="button"
              className="pb-avatar"
              onClick={() => {
                setAccountPage('menu')
                setView('account')
              }}
              aria-label="Account"
            >
              <img src={brandLogo} alt="" />
            </button>
          </div>
          {view === 'home' ? (
            <div className="pb-search-wrap">
              <input
                className="pb-search"
                placeholder="Search food, groceries, drinks…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') void loadMerchants(search)
                }}
              />
            </div>
          ) : null}
        </header>
      )}

      {error ? <p className="error pb-error">{error}</p> : null}

      {view === 'home' && (
        <main className="pb-main">
          <div className="pb-cats" role="list">
            {['Breakfast', 'Seafood', 'Fast Food', 'Cake', 'Desserts'].map((label, i) => (
              <button
                key={label}
                type="button"
                className="pb-cat"
                onClick={() => {
                  setSearch(label)
                  void loadMerchants(label)
                }}
              >
                <span className="pb-cat-ico">{CATEGORY_ICONS[i % CATEGORY_ICONS.length]}</span>
                <small>{label}</small>
              </button>
            ))}
            {categoryChips.length ? null : null}
          </div>

          <section className="pb-section">
            <div className="pb-section-head">
              <h2>Popular now</h2>
            </div>
            <div className="pb-popular">
              {popularMerchants.map((m) => (
                <button key={m.id} type="button" className="pb-popular-card" onClick={() => void openStore(m.id)}>
                  <div
                    className="pb-popular-img"
                    style={m.coverUrl || m.logoUrl ? { backgroundImage: `url(${mediaUrl(m.coverUrl || m.logoUrl)})` } : undefined}
                  />
                  <div className="pb-popular-body">
                    <b>{m.name}</b>
                    <div className="pb-meta">
                      <span className={m.isOpen ? 'ok' : 'bad'}>{m.isOpen ? 'Open' : 'Closed'}</span>
                      <span className="muted">·</span>
                      <span className="muted truncate">{m.address}</span>
                    </div>
                  </div>
                </button>
              ))}
              {!popularMerchants.length ? <p className="muted">No stores nearby yet.</p> : null}
            </div>
          </section>

          <section className="pb-section">
            <div className="pb-section-head">
              <h2>All merchants</h2>
            </div>
            <div className="pb-merchant-row">
              {merchants.map((m) => (
                <button key={m.id} type="button" className="pb-merchant-mini" onClick={() => void openStore(m.id)}>
                  <div
                    className="pb-mini-img"
                    style={m.logoUrl ? { backgroundImage: `url(${mediaUrl(m.logoUrl)})` } : undefined}
                  />
                  <b>{m.name}</b>
                  <small className={m.isOpen ? 'ok' : 'bad'}>{m.isOpen ? 'Open' : 'Closed'}</small>
                </button>
              ))}
            </div>
          </section>
        </main>
      )}

      {view === 'store' && store && (
        <main className="pb-main pb-store">
          <button type="button" className="pb-back-link" onClick={() => setView('home')}>
            ← Back
          </button>
          <div
            className="pb-store-banner"
            style={store.coverUrl ? { backgroundImage: `url(${mediaUrl(store.coverUrl)})` } : undefined}
          >
            <div className="pb-store-banner-inner">
              {store.logoUrl ? <img src={mediaUrl(store.logoUrl)} alt="" /> : null}
              <div>
                <h2>{store.name}</h2>
                <p className="muted">{store.address}</p>
              </div>
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
            <button type="button" className="ghost" onClick={() => setView(view === 'checkout' ? 'cart' : store ? 'store' : 'home')}>
              ←
            </button>
            <h2>{view === 'cart' ? 'Cart' : 'Checkout'}</h2>
          </div>
          {cart.map((line) => (
            <div key={line.key} className="pb-line">
              <div
                className="pb-line-img"
                style={line.imageUrl ? { backgroundImage: `url(${mediaUrl(line.imageUrl)})` } : undefined}
              />
              <div className="pb-line-copy">
                <b>{line.name}</b>
                <small>{peso(line.unitSellingPrice)} each</small>
                {line.addons.length ? <small className="muted">{line.addons.map((a) => a.name).join(', ')}</small> : null}
                <div className="pb-qty">
                  <button type="button" onClick={() => setCart((c) => c.map((x) => (x.key === line.key ? { ...x, quantity: Math.max(1, x.quantity - 1) } : x)))}>−</button>
                  <span>{line.quantity}</span>
                  <button type="button" onClick={() => setCart((c) => c.map((x) => (x.key === line.key ? { ...x, quantity: x.quantity + 1 } : x)))}>+</button>
                </div>
              </div>
              <div className="pb-line-total">
                <b>{peso(lineTotal(line))}</b>
                <button type="button" className="ghost" onClick={() => setCart((c) => c.filter((x) => x.key !== line.key))}>🗑</button>
              </div>
            </div>
          ))}
          {!cart.length ? <p className="muted">Your basket is empty.</p> : null}

          {view === 'checkout' ? (
            <>
              <label className="pb-field">
                Payment
                <select value={payment} onChange={(e) => setPayment(e.target.value as PaymentMethod)}>
                  <option value="Cash">Cash</option>
                  <option value="GCash">GCash</option>
                  <option value="Maya">Maya</option>
                </select>
              </label>
              {quote ? (
                <div className="pb-summary">
                  <div><span>Subtotal</span><b>{peso(quote.goodsSubtotal)}</b></div>
                  <div><span>Delivery fee</span><b>{quote.deliveryFee <= 0 ? 'Free' : peso(quote.deliveryFee)}</b></div>
                  {quote.adjustmentAmount !== 0 ? (
                    <div className="disc">
                      <span>{quote.adjustmentLabel || 'Adjustment'}</span>
                      <b>{peso(quote.adjustmentAmount)}</b>
                    </div>
                  ) : null}
                  <div className="total"><span>Total</span><b>{peso(quote.customerTotal)}</b></div>
                </div>
              ) : (
                <p className="muted">{busy ? 'Quoting…' : 'Add a delivery place to see fees.'}</p>
              )}
              <button type="button" className="primary pb-primary" disabled={busy || !quote || !dropoff} onClick={() => void placeOrder()}>
                Place order
              </button>
            </>
          ) : (
            <button type="button" className="primary pb-primary" disabled={!cart.length} onClick={() => setView('checkout')}>
              Checkout · {peso(cartGoods)}
            </button>
          )}
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
            <span>🛒</span>Cart{cartCount ? ` (${cartCount})` : ''}
          </button>
          <button
            type="button"
            className={view === 'account' ? 'on' : ''}
            onClick={() => {
              setAccountPage('menu')
              setView('account')
            }}
          >
            <span>☺</span>Account
          </button>
        </nav>
      ) : null}

      {sheetProduct ? (
        <div className="pb-sheet" role="dialog" aria-modal="true">
          <div className="pb-sheet-card">
            <button type="button" className="ghost" onClick={() => setSheetProduct(null)}>Close</button>
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
    </div>
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
