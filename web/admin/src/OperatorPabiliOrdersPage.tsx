import { useEffect, useState } from 'react'
import { api } from './api'

type OrderListItem = {
  id: string
  reference: string
  status: string
  merchantName: string
  customerName: string
  customerPhone: string
  customerTotal: number
  adjustmentAmount: number
  riderName: string | null
  createdAtUtc: string
}

type OrderDetail = {
  id: string
  reference: string
  status: string
  merchantId: string
  merchantName: string
  customerId: string
  customerName: string
  customerPhone: string
  pickupAddress: string
  pickupLat: number
  pickupLng: number
  dropoffAddress: string
  dropoffLat: number
  dropoffLng: number
  distanceKm: number
  goodsSubtotal: number
  goodsBaseSubtotal: number
  deliveryFee: number
  surchargeTotal: number
  adjustmentAmount: number
  adjustmentLabel: string
  customerTotal: number
  paymentMethod: string
  riderId: string | null
  riderName: string | null
  riderPhone: string | null
  notes: string | null
  cancelReason: string | null
  createdAtUtc: string
  acceptedAtUtc: string | null
  pickedUpAtUtc: string | null
  deliveringAtUtc: string | null
  completedAtUtc: string | null
  cancelledAtUtc: string | null
  canAdjust: boolean
  canCancel: boolean
  items: Array<{
    id: string
    name: string
    quantity: number
    unitSellingPrice: number
    lineSellingTotal: number
    addons: Array<{ id: string; name: string; quantity: number; unitSellingPrice: number; lineSellingTotal: number }>
  }>
}

const STATUSES = ['', 'Pending', 'Waiting', 'PickedUp', 'Delivering', 'Completed', 'Cancelled'] as const

function peso(value: number) {
  return `₱${value.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function when(value: string | null | undefined) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('en-PH', {
    timeZone: 'Asia/Manila',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

export function OperatorPabiliOrdersPage() {
  const [q, setQ] = useState('')
  const [status, setStatus] = useState('')
  const [items, setItems] = useState<OrderListItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<OrderDetail | null>(null)
  const [adjAmount, setAdjAmount] = useState('0')
  const [adjLabel, setAdjLabel] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  async function loadList(nextPage = page) {
    setError('')
    try {
      const data = await api.operatorPabiliOrders(q, status, nextPage, 20)
      setItems(data.items)
      setTotal(data.total)
      setPage(data.page)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load orders.')
    }
  }

  async function openDetail(id: string) {
    setSelectedId(id)
    setError('')
    setNotice('')
    try {
      const row = await api.operatorPabiliOrder(id)
      setDetail(row)
      setAdjAmount(String(row.adjustmentAmount))
      setAdjLabel(row.adjustmentLabel ?? '')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load order.')
      setDetail(null)
    }
  }

  useEffect(() => {
    void loadList(1)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [status])

  async function saveAdjustment() {
    if (!detail) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const amount = Number(adjAmount)
      if (!Number.isFinite(amount)) {
        setError('Adjustment amount must be a number.')
        return
      }
      const row = await api.patchOperatorPabiliOrderAdjustment(detail.id, {
        adjustmentAmount: amount,
        adjustmentLabel: adjLabel,
      })
      setDetail(row)
      setNotice('Adjustment saved.')
      await loadList(page)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not save adjustment.')
    } finally {
      setBusy(false)
    }
  }

  async function cancelOrder() {
    if (!detail || !detail.canCancel) return
    if (!window.confirm(`Cancel order ${detail.reference}?`)) return
    setBusy(true)
    setError('')
    try {
      const row = await api.cancelOperatorPabiliOrder(detail.id, 'Cancelled by operator.')
      setDetail(row)
      setNotice('Order cancelled.')
      await loadList(page)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not cancel order.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="form-sections">
      <div className="toolbar" style={{ gap: 10, flexWrap: 'wrap' }}>
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder="Search reference, customer, merchant…"
          style={{ minWidth: 240 }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void loadList(1)
          }}
        />
        <button type="button" className="ghost" onClick={() => void loadList(1)}>
          Search
        </button>
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          {STATUSES.map((s) => (
            <button
              key={s || 'all'}
              type="button"
              className={status === s ? 'primary' : 'ghost'}
              onClick={() => setStatus(s)}
            >
              {s || 'All'}
            </button>
          ))}
        </div>
      </div>

      {error && <p className="error">{error}</p>}
      {notice && <p className="ok">{notice}</p>}

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 1fr) minmax(320px, 1.2fr)', gap: 16 }}>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Ref</th>
                <th>Status</th>
                <th>Customer</th>
                <th>Total</th>
                <th>When</th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan={5} className="muted">
                    No orders yet.
                  </td>
                </tr>
              ) : (
                items.map((row) => (
                  <tr
                    key={row.id}
                    className={selectedId === row.id ? 'selected' : undefined}
                    style={{ cursor: 'pointer' }}
                    onClick={() => void openDetail(row.id)}
                  >
                    <td>
                      <b>{row.reference}</b>
                      <div className="muted">{row.merchantName}</div>
                    </td>
                    <td>
                      <span className="tag status">{row.status}</span>
                    </td>
                    <td>
                      {row.customerName}
                      <div className="muted">{row.customerPhone}</div>
                    </td>
                    <td>{peso(row.customerTotal)}</td>
                    <td>{when(row.createdAtUtc)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
          <div className="toolbar" style={{ marginTop: 8 }}>
            <span className="muted">
              {total} order{total === 1 ? '' : 's'}
            </span>
            <button type="button" className="ghost" disabled={page <= 1} onClick={() => void loadList(page - 1)}>
              Prev
            </button>
            <button
              type="button"
              className="ghost"
              disabled={page * 20 >= total}
              onClick={() => void loadList(page + 1)}
            >
              Next
            </button>
          </div>
        </div>

        <div className="panel" style={{ padding: 16 }}>
          {!detail ? (
            <p className="muted">Select an order to monitor details, adjust price, or cancel.</p>
          ) : (
            <>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, alignItems: 'flex-start' }}>
                <div>
                  <h3 style={{ margin: 0 }}>{detail.reference}</h3>
                  <p className="muted" style={{ margin: '4px 0 0' }}>
                    {detail.status} · {when(detail.createdAtUtc)}
                  </p>
                </div>
                {detail.canCancel ? (
                  <button type="button" className="ghost" disabled={busy} onClick={() => void cancelOrder()}>
                    Cancel
                  </button>
                ) : null}
              </div>

              <div style={{ marginTop: 14, display: 'grid', gap: 8 }}>
                <div>
                  <b>{detail.merchantName}</b>
                  <div className="muted">{detail.pickupAddress}</div>
                </div>
                <div>
                  <b>
                    {detail.customerName} · {detail.customerPhone}
                  </b>
                  <div className="muted">{detail.dropoffAddress}</div>
                </div>
                <div className="muted">
                  Rider: {detail.riderName ?? '—'} {detail.riderPhone ? `· ${detail.riderPhone}` : ''}
                </div>
              </div>

              <ul style={{ marginTop: 14, paddingLeft: 18 }}>
                {detail.items.map((item) => (
                  <li key={item.id}>
                    {item.quantity}× {item.name} — {peso(item.lineSellingTotal)}
                    {item.addons.length > 0 ? (
                      <div className="muted">{item.addons.map((a) => a.name).join(', ')}</div>
                    ) : null}
                  </li>
                ))}
              </ul>

              <div style={{ marginTop: 12, display: 'grid', gap: 4 }}>
                <div>Goods · {peso(detail.goodsSubtotal)}</div>
                <div>
                  Delivery · {peso(detail.deliveryFee)}
                  {detail.surchargeTotal ? ` (incl. surcharge ${peso(detail.surchargeTotal)})` : ''}
                </div>
                {detail.adjustmentAmount !== 0 ? (
                  <div>
                    {detail.adjustmentLabel || 'Adjustment'} · {peso(detail.adjustmentAmount)}
                  </div>
                ) : null}
                <div>
                  <b>Total · {peso(detail.customerTotal)}</b>
                </div>
                <div className="muted">{detail.distanceKm.toFixed(1)} km · {detail.paymentMethod}</div>
              </div>

              <div style={{ marginTop: 16, borderTop: '1px solid var(--border, #ddd)', paddingTop: 12 }}>
                <h4 style={{ margin: '0 0 8px' }}>Price adjustment</h4>
                {detail.canAdjust ? (
                  <div style={{ display: 'grid', gap: 8 }}>
                    <label>
                      <span>Amount (signed)</span>
                      <input value={adjAmount} onChange={(e) => setAdjAmount(e.target.value)} />
                    </label>
                    <label>
                      <span>Customer-visible label</span>
                      <input value={adjLabel} onChange={(e) => setAdjLabel(e.target.value)} placeholder="e.g. Rain fee" />
                    </label>
                    <button type="button" className="primary" disabled={busy} onClick={() => void saveAdjustment()}>
                      Save adjustment
                    </button>
                  </div>
                ) : (
                  <p className="muted">Locked after completed or cancelled.</p>
                )}
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
