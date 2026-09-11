import { useEffect, useMemo, useState } from 'react'
import { api, mediaUrl, Place, PaymentMethod } from './api'

type View = 'market' | 'store' | 'cart' | 'checkout' | 'order' | 'orders'

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
  quantity: number
  unitSellingPrice: number
  addons: CartAddon[]
}

type Quote = {
  merchantId: string
  merchantName: string
  goodsSubtotal: number
  deliveryFee: number
  surchargeTotal: number
  distanceKm: number
  adjustmentAmount: number
  adjustmentLabel: string
  customerTotal: number
}

type OrderDetail = {
  id: string
  reference: string
  status: string
  merchantName: string
  pickupAddress: string
  dropoffAddress: string
  goodsSubtotal: number
  deliveryFee: number
  surchargeTotal: number
  adjustmentAmount: number
  adjustmentLabel: string
  customerTotal: number
  paymentMethod: string
  riderName: string | null
  riderPhone: string | null
  canCancel: boolean
  items: Array<{
    id: string
    name: string
    quantity: number
    lineSellingTotal: number
    addons: Array<{ name: string }>
  }>
}

function peso(n: number) {
  return `₱${n.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function lineTotal(line: CartLine) {
  const addons = line.addons.reduce((s, a) => s + a.sellingPrice * a.quantity, 0)
  return (line.unitSellingPrice + addons) * line.quantity
}

export function PabiliStorefront({
  brandName,
  places,
  mapLat,
  mapLng,
}: {
  brandName: string
  places: Place[]
  mapLat: number | null
  mapLng: number | null
}) {
  const [view, setView] = useState<View>('market')
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
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const lat = dropoff?.lat ?? mapLat ?? 14.5995
  const lng = dropoff?.lng ?? mapLng ?? 120.9842
  const barangayId = dropoff?.barangayId

  const cartCount = cart.reduce((s, x) => s + x.quantity, 0)
  const cartGoods = useMemo(() => cart.reduce((s, x) => s + lineTotal(x), 0), [cart])

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
      if (group.maxSelect > 1 && next.length > group.maxSelect) {
        next = [...next.slice(1), optionId]
      }
      return { ...prev, [group.id]: next }
    })
  }

  function addSheetToCart() {
    if (!sheetProduct || !store) return
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
      setView('order')
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
      setView('order')
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

  return (
    <div className="pabili-app">
      <header className="pabili-top">
        <div className="pabili-top-row">
          {view !== 'market' ? (
            <button
              type="button"
              className="ghost pabili-back"
              onClick={() => {
                if (view === 'store') setView('market')
                else if (view === 'cart' || view === 'checkout') setView(store ? 'store' : 'market')
                else if (view === 'order' || view === 'orders') setView('market')
                else setView('market')
              }}
            >
              ←
            </button>
          ) : (
            <span className="pabili-brand">{brandName}</span>
          )}
          <button type="button" className="ghost" onClick={() => void loadOrders()}>
            Orders
          </button>
        </div>
        {view === 'market' || view === 'checkout' ? (
          <div className="pabili-loc">
            <span className="muted">Deliver to</span>
            <select
              value={dropoff?.barangayId ?? ''}
              onChange={(e) => {
                const next = places.find((p) => p.barangayId === e.target.value) ?? null
                setDropoff(next)
              }}
            >
              {places.length === 0 ? <option value="">Set a saved place from Home</option> : null}
              {places.map((p) => (
                <option key={p.barangayId} value={p.barangayId}>
                  {p.label}
                </option>
              ))}
            </select>
          </div>
        ) : null}
      </header>

      {error ? <p className="error pabili-error">{error}</p> : null}

      {view === 'market' && (
        <section className="pabili-market">
          <h2 className="pabili-hero-title">Pabili</h2>
          <p className="pabili-hero-copy">Order from nearby merchants — we deliver.</p>
          <input
            className="pabili-search"
            placeholder="Search stores"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void loadMerchants(search)
            }}
          />
          <div className="pabili-merchant-grid">
            {merchants.map((m) => (
              <button key={m.id} type="button" className="pabili-merchant-card" onClick={() => void openStore(m.id)}>
                <div
                  className="pabili-merchant-cover"
                  style={m.coverUrl ? { backgroundImage: `url(${mediaUrl(m.coverUrl)})` } : undefined}
                />
                <div className="pabili-merchant-body">
                  {m.logoUrl ? <img src={mediaUrl(m.logoUrl) ?? undefined} alt="" className="pabili-merchant-logo" /> : null}
                  <div>
                    <b>{m.name}</b>
                    <div className="muted">{m.address}</div>
                    <span className={`pabili-open ${m.isOpen ? 'yes' : 'no'}`}>{m.isOpen ? 'Open' : 'Closed'}</span>
                  </div>
                </div>
              </button>
            ))}
            {!merchants.length ? <p className="muted">No stores in this area yet.</p> : null}
          </div>
        </section>
      )}

      {view === 'store' && store && (
        <section className="pabili-store">
          <div
            className="pabili-store-hero"
            style={store.coverUrl ? { backgroundImage: `url(${mediaUrl(store.coverUrl)})` } : undefined}
          >
            <div className="pabili-store-hero-inner">
              {store.logoUrl ? <img src={mediaUrl(store.logoUrl) ?? undefined} alt="" /> : null}
              <div>
                <h2>{store.name}</h2>
                <p className="muted">{store.address}</p>
                <span className={`pabili-open ${store.isOpen ? 'yes' : 'no'}`}>{store.isOpen ? 'Open' : 'Closed'}</span>
              </div>
            </div>
          </div>
          <div className="pabili-product-list">
            {store.products.map((p) => (
              <button key={p.id} type="button" className="pabili-product" onClick={() => openProduct(p)}>
                <div className="pabili-product-copy">
                  <b>{p.name}</b>
                  <div className="muted">{p.description || p.categoryName}</div>
                  <div className="pabili-price">{peso(p.sellingPrice)}</div>
                </div>
                {p.imageUrl ? <img src={mediaUrl(p.imageUrl) ?? undefined} alt="" /> : <div className="pabili-product-ph" />}
              </button>
            ))}
          </div>
          {cartCount > 0 ? (
            <button type="button" className="pabili-cart-bar" onClick={() => setView('cart')}>
              View cart · {cartCount} · {peso(cartGoods)}
            </button>
          ) : null}
        </section>
      )}

      {view === 'cart' && (
        <section className="pabili-cart">
          <h2>Cart</h2>
          {cart.map((line) => (
            <div key={line.key} className="pabili-cart-line">
              <div>
                <b>
                  {line.quantity}× {line.name}
                </b>
                {line.addons.length ? <div className="muted">{line.addons.map((a) => a.name).join(', ')}</div> : null}
                <div>{peso(lineTotal(line))}</div>
              </div>
              <div className="pabili-qty">
                <button type="button" onClick={() => setCart((c) => c.map((x) => (x.key === line.key ? { ...x, quantity: Math.max(1, x.quantity - 1) } : x)))}>
                  −
                </button>
                <span>{line.quantity}</span>
                <button type="button" onClick={() => setCart((c) => c.map((x) => (x.key === line.key ? { ...x, quantity: x.quantity + 1 } : x)))}>
                  +
                </button>
                <button type="button" className="ghost" onClick={() => setCart((c) => c.filter((x) => x.key !== line.key))}>
                  Remove
                </button>
              </div>
            </div>
          ))}
          <button type="button" className="primary" disabled={!cart.length} onClick={() => setView('checkout')}>
            Checkout · {peso(cartGoods)}
          </button>
        </section>
      )}

      {view === 'checkout' && (
        <section className="pabili-checkout">
          <h2>Checkout</h2>
          <label>
            Payment
            <select value={payment} onChange={(e) => setPayment(e.target.value as PaymentMethod)}>
              <option value="Cash">Cash</option>
              <option value="GCash">GCash</option>
              <option value="Maya">Maya</option>
            </select>
          </label>
          {quote ? (
            <div className="pabili-breakdown">
              <div>Goods · {peso(quote.goodsSubtotal)}</div>
              <div>Delivery · {peso(quote.deliveryFee)}</div>
              {quote.adjustmentAmount !== 0 ? (
                <div>
                  {quote.adjustmentLabel || 'Adjustment'} · {peso(quote.adjustmentAmount)}
                </div>
              ) : null}
              <div>
                <b>Total · {peso(quote.customerTotal)}</b>
              </div>
              <div className="muted">{quote.distanceKm.toFixed(1)} km</div>
            </div>
          ) : (
            <p className="muted">{busy ? 'Quoting…' : 'Set a delivery place to see fees.'}</p>
          )}
          <button type="button" className="primary" disabled={busy || !quote || !dropoff} onClick={() => void placeOrder()}>
            Place order
          </button>
        </section>
      )}

      {view === 'orders' && (
        <section className="pabili-orders">
          <h2>Your orders</h2>
          {orders.map((o) => (
            <button key={o.id} type="button" className="pabili-order-row" onClick={() => void openOrder(o.id)}>
              <b>{o.reference}</b>
              <div className="muted">
                {o.status} · {o.merchantName}
              </div>
              <div>{peso(o.customerTotal)}</div>
            </button>
          ))}
          {!orders.length ? <p className="muted">No Pabili orders yet.</p> : null}
        </section>
      )}

      {view === 'order' && order && (
        <section className="pabili-order">
          <h2>{order.reference}</h2>
          <p className="pabili-status">{order.status}</p>
          <p>
            <b>{order.merchantName}</b>
            <br />
            <span className="muted">{order.pickupAddress}</span>
          </p>
          <p>
            Drop-off
            <br />
            <span className="muted">{order.dropoffAddress}</span>
          </p>
          <ul>
            {order.items.map((item) => (
              <li key={item.id}>
                {item.quantity}× {item.name} — {peso(item.lineSellingTotal)}
              </li>
            ))}
          </ul>
          <div className="pabili-breakdown">
            <div>Goods · {peso(order.goodsSubtotal)}</div>
            <div>Delivery · {peso(order.deliveryFee)}</div>
            {order.adjustmentAmount !== 0 ? (
              <div>
                {order.adjustmentLabel || 'Adjustment'} · {peso(order.adjustmentAmount)}
              </div>
            ) : null}
            <div>
              <b>Total · {peso(order.customerTotal)}</b>
            </div>
          </div>
          {order.riderName ? (
            <p className="muted">
              Rider {order.riderName} {order.riderPhone ? `· ${order.riderPhone}` : ''}
            </p>
          ) : (
            <p className="muted">Finding a Pabili rider…</p>
          )}
          {order.canCancel ? (
            <button type="button" className="ghost" disabled={busy} onClick={() => void cancelOrder()}>
              Cancel order
            </button>
          ) : null}
        </section>
      )}

      {sheetProduct ? (
        <div className="pabili-sheet" role="dialog" aria-modal="true">
          <div className="pabili-sheet-card">
            <button type="button" className="ghost" onClick={() => setSheetProduct(null)}>
              Close
            </button>
            <h3>{sheetProduct.name}</h3>
            <p className="muted">{sheetProduct.description}</p>
            <p className="pabili-price">{peso(sheetProduct.sellingPrice)}</p>
            {sheetProduct.addonGroups.map((g) => (
              <div key={g.id} className="pabili-addon-group">
                <b>
                  {g.name}{' '}
                  <span className="muted">
                    ({g.minSelect}-{g.maxSelect})
                  </span>
                </b>
                {g.options.map((o) => {
                  const on = (sheetPicks[g.id] ?? []).includes(o.id)
                  return (
                    <label key={o.id} className={`pabili-addon ${on ? 'on' : ''}`}>
                      <input type="checkbox" checked={on} onChange={() => togglePick(g, o.id)} />
                      <span>{o.name}</span>
                      <span>{peso(o.sellingPrice)}</span>
                    </label>
                  )
                })}
              </div>
            ))}
            <div className="pabili-qty">
              <button type="button" onClick={() => setSheetQty((q) => Math.max(1, q - 1))}>
                −
              </button>
              <span>{sheetQty}</span>
              <button type="button" onClick={() => setSheetQty((q) => q + 1)}>
                +
              </button>
            </div>
            <button type="button" className="primary" onClick={addSheetToCart}>
              Add to cart
            </button>
          </div>
        </div>
      ) : null}
    </div>
  )
}
