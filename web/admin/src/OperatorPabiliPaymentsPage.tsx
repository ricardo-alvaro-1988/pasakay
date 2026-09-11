import { useEffect, useState } from 'react'
import { api, OperatorPabiliPaymentMethodItem, PaymentMethod, PAYMENT_METHODS, SaveOperatorPabiliPaymentMethodBody } from './api'
import { compressImageFile } from './compress-image'

export function OperatorPabiliPaymentsPage() {
  const [items, setItems] = useState<OperatorPabiliPaymentMethodItem[] | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<OperatorPabiliPaymentMethodItem | null>(null)
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  const [label, setLabel] = useState('')
  const [sortOrder, setSortOrder] = useState('0')
  const [isActive, setIsActive] = useState(true)
  const [qrFile, setQrFile] = useState<File | null>(null)
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.operatorPabiliPayments()
      .then((res) => setItems(res.items))
      .catch((err: Error) => setError(err.message))
  }, [])

  function openCreate() {
    setEditing(null)
    setMethod('Cash')
    setLabel('')
    setSortOrder(String((items?.length ?? 0)))
    setIsActive(true)
    setQrFile(null)
    setFormError('')
    setOpen(true)
  }

  function openEdit(item: OperatorPabiliPaymentMethodItem) {
    setEditing(item)
    setMethod(item.method)
    setLabel(item.label === item.method ? '' : item.label)
    setSortOrder(String(item.sortOrder))
    setIsActive(item.isActive)
    setQrFile(null)
    setFormError('')
    setOpen(true)
  }

  function closeModal() {
    setOpen(false)
    setEditing(null)
    setFormError('')
    setQrFile(null)
  }

  function body(): SaveOperatorPabiliPaymentMethodBody {
    const order = Math.floor(Number(sortOrder))
    return {
      method,
      label: method === 'Other' ? label.trim() : (label.trim() || undefined),
      isActive,
      sortOrder: Number.isFinite(order) ? order : 0,
    }
  }

  async function save() {
    if (method === 'Other' && !label.trim()) {
      setFormError('Label is required for Other.')
      return
    }
    setBusy(true)
    setFormError('')
    try {
      const payload = body()
      let row: OperatorPabiliPaymentMethodItem
      if (editing) {
        row = await api.updateOperatorPabiliPayment(editing.id, payload)
      } else {
        row = await api.createOperatorPabiliPayment(payload)
      }
      if (qrFile) {
        const compressed = await compressImageFile(qrFile)
        row = await api.uploadOperatorPabiliPaymentQr(row.id, compressed)
      }
      setItems((prev) => {
        const list = prev ?? []
        const next = editing
          ? list.map((x) => (x.id === row.id ? row : x))
          : [...list, row]
        return next.sort((a, b) => a.sortOrder - b.sortOrder || a.method.localeCompare(b.method))
      })
      setNotice(editing ? 'Payment method updated.' : 'Payment method added.')
      closeModal()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Could not save payment method.')
    } finally {
      setBusy(false)
    }
  }

  async function toggle(item: OperatorPabiliPaymentMethodItem) {
    setError('')
    try {
      const row = await api.toggleOperatorPabiliPayment(item.id)
      setItems((prev) => (prev ?? []).map((x) => (x.id === row.id ? row : x)))
      setNotice(`${row.label} is now ${row.isActive ? 'active' : 'inactive'}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not toggle payment method.')
    }
  }

  async function remove(item: OperatorPabiliPaymentMethodItem) {
    if (!window.confirm(`Delete “${item.label}”?`)) return
    setError('')
    try {
      await api.deleteOperatorPabiliPayment(item.id)
      setItems((prev) => (prev ?? []).filter((x) => x.id !== item.id))
      setNotice('Payment method deleted.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete payment method.')
    }
  }

  if (!items) return error ? <p className="error">{error}</p> : <p>Loading payment methods…</p>

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Payment methods</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 560 }}>
            Choose which payment options customers see on Pabili checkout. Upload a QR for GCash, Maya, or Other when needed.
          </p>
        </div>
        <button className="btn" type="button" style={{ width: 'auto', whiteSpace: 'nowrap' }} onClick={openCreate}>
          Add payment method
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>QR</th>
              <th>Method</th>
              <th>Label</th>
              <th>Order</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={6}>No payment methods yet. Add Cash / GCash / Maya / Other for checkout.</td>
              </tr>
            ) : items.map((item) => (
              <tr key={item.id}>
                <td>
                  {item.qrImageUrl ? (
                    <img src={item.qrImageUrl} alt="" style={{ width: 48, height: 48, objectFit: 'cover', borderRadius: 8 }} />
                  ) : (
                    <span className="muted">—</span>
                  )}
                </td>
                <td><strong>{item.method}</strong></td>
                <td>{item.label}</td>
                <td>{item.sortOrder}</td>
                <td>
                  <span className={`tag ${item.isActive ? 'active' : 'rejected'}`}>
                    {item.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td style={{ whiteSpace: 'nowrap', textAlign: 'right' }}>
                  <button className="btn tiny" type="button" onClick={() => openEdit(item)}>Edit</button>
                  {' '}
                  <button className={`btn tiny${item.isActive ? ' danger' : ''}`} type="button" onClick={() => void toggle(item)}>
                    {item.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                  {' '}
                  <button className="btn tiny danger" type="button" onClick={() => void remove(item)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {open ? (
        <div className="modal-backdrop" role="presentation" onClick={closeModal}>
          <div
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby="pabili-payment-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <div>
                <h2 id="pabili-payment-title">{editing ? 'Edit payment method' : 'Add payment method'}</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  Inactive methods are hidden from customers. QR is optional for Cash.
                </p>
              </div>
              <button className="btn tiny" type="button" onClick={closeModal}>Close</button>
            </div>
            <div className="form-grid">
              <label className="field">
                <span>Method</span>
                <select
                  value={method}
                  disabled={!!editing}
                  onChange={(e) => setMethod(e.target.value as PaymentMethod)}
                >
                  {PAYMENT_METHODS.map((m) => (
                    <option key={m} value={m}>{m}</option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>Status</span>
                <select value={isActive ? 'active' : 'inactive'} onChange={(e) => setIsActive(e.target.value === 'active')}>
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                </select>
              </label>
              <label className="field">
                <span>Sort order</span>
                <input
                  type="number"
                  inputMode="numeric"
                  value={sortOrder}
                  onChange={(e) => setSortOrder(e.target.value)}
                />
              </label>
              <label className="field">
                <span>Label{method === 'Other' ? '' : ' (optional)'}</span>
                <input
                  value={label}
                  onChange={(e) => setLabel(e.target.value)}
                  placeholder={method === 'Other' ? 'Bank transfer' : method}
                />
              </label>
              <label className="field wide">
                <span>QR image{editing ? ' (optional replace)' : ' (optional)'}</span>
                <input
                  type="file"
                  accept="image/*"
                  onChange={(e) => setQrFile(e.target.files?.[0] ?? null)}
                />
              </label>
              {editing?.qrImageUrl ? (
                <div className="field wide">
                  <span>Current QR</span>
                  <img
                    src={editing.qrImageUrl}
                    alt=""
                    style={{ maxWidth: 180, maxHeight: 180, borderRadius: 12, objectFit: 'contain', border: '1px solid var(--line)', background: '#fff' }}
                  />
                </div>
              ) : null}
            </div>
            {formError ? <p className="error">{formError}</p> : null}
            <div className="modal-actions">
              <button className="btn" type="button" onClick={() => void save()} disabled={busy}>
                {busy ? 'Saving…' : editing ? 'Save changes' : 'Add method'}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
