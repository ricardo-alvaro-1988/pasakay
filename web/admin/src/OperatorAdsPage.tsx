import { useEffect, useState } from 'react'
import { api, OperatorAdItem, SaveOperatorAdBody } from './api'
import { compressImageFile } from './compress-image'

export function OperatorAdsPage() {
  const [items, setItems] = useState<OperatorAdItem[] | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<OperatorAdItem | null>(null)
  const [title, setTitle] = useState('')
  const [redirectUrl, setRedirectUrl] = useState('')
  const [sortOrder, setSortOrder] = useState('0')
  const [isActive, setIsActive] = useState(true)
  const [imageFile, setImageFile] = useState<File | null>(null)
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.operatorAds()
      .then((res) => setItems(res.items))
      .catch((err: Error) => setError(err.message))
  }, [])

  function openCreate() {
    setEditing(null)
    setTitle('')
    setRedirectUrl('https://')
    setSortOrder('0')
    setIsActive(true)
    setImageFile(null)
    setFormError('')
    setOpen(true)
  }

  function openEdit(item: OperatorAdItem) {
    setEditing(item)
    setTitle(item.title)
    setRedirectUrl(item.redirectUrl)
    setSortOrder(String(item.sortOrder))
    setIsActive(item.isActive)
    setImageFile(null)
    setFormError('')
    setOpen(true)
  }

  function closeModal() {
    setOpen(false)
    setEditing(null)
    setFormError('')
    setImageFile(null)
  }

  function body(): SaveOperatorAdBody {
    const order = Math.floor(Number(sortOrder))
    return {
      title: title.trim(),
      redirectUrl: redirectUrl.trim(),
      isActive,
      sortOrder: Number.isFinite(order) ? order : 0,
    }
  }

  async function save() {
    if (!title.trim()) {
      setFormError('Title is required.')
      return
    }
    if (!redirectUrl.trim()) {
      setFormError('Redirect URL is required.')
      return
    }
    if (!editing && !imageFile) {
      setFormError('Upload an image for this offer.')
      return
    }
    setBusy(true)
    setFormError('')
    try {
      const payload = body()
      let row: OperatorAdItem
      if (editing) {
        row = await api.updateOperatorAd(editing.id, payload)
      } else {
        row = await api.createOperatorAd(payload)
      }
      if (imageFile) {
        const compressed = await compressImageFile(imageFile)
        row = await api.uploadOperatorAdImage(row.id, compressed)
      }
      setItems((prev) => {
        const list = prev ?? []
        const next = editing
          ? list.map((x) => (x.id === row.id ? row : x))
          : [row, ...list]
        return next.sort((a, b) => a.sortOrder - b.sortOrder || a.title.localeCompare(b.title))
      })
      setNotice(editing ? 'Exclusive offer updated.' : 'Exclusive offer created.')
      closeModal()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Could not save offer.')
    } finally {
      setBusy(false)
    }
  }

  async function toggle(item: OperatorAdItem) {
    setError('')
    try {
      const row = await api.toggleOperatorAd(item.id)
      setItems((prev) => (prev ?? []).map((x) => (x.id === row.id ? row : x)))
      setNotice(`${row.title} is now ${row.isActive ? 'active' : 'inactive'}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not toggle offer.')
    }
  }

  async function remove(item: OperatorAdItem) {
    if (!window.confirm(`Delete “${item.title}”?`)) return
    setError('')
    try {
      await api.deleteOperatorAd(item.id)
      setItems((prev) => (prev ?? []).filter((x) => x.id !== item.id))
      setNotice('Exclusive offer deleted.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete offer.')
    }
  }

  if (!items) return error ? <p className="error">{error}</p> : <p>Loading exclusive offers…</p>

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Exclusive Offer</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 560 }}>
            Create image ads for the Pabili storefront. Customers see them under Exclusive Offer; tapping opens the redirect URL.
          </p>
        </div>
        <button className="btn" type="button" style={{ width: 'auto', whiteSpace: 'nowrap' }} onClick={openCreate}>
          Add offer
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Image</th>
              <th>Title</th>
              <th>Redirect</th>
              <th>Order</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={6}>No exclusive offers yet. Add an image and redirect URL.</td>
              </tr>
            ) : items.map((item) => (
              <tr key={item.id}>
                <td>
                  {item.imageUrl ? (
                    <img src={item.imageUrl} alt="" style={{ width: 72, height: 40, objectFit: 'cover', borderRadius: 8 }} />
                  ) : (
                    <span className="muted">No image</span>
                  )}
                </td>
                <td><strong>{item.title}</strong></td>
                <td style={{ maxWidth: 220, overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  <a href={item.redirectUrl} target="_blank" rel="noreferrer">{item.redirectUrl}</a>
                </td>
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
            aria-labelledby="exclusive-offer-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <div>
                <h2 id="exclusive-offer-title">{editing ? 'Edit exclusive offer' : 'New exclusive offer'}</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  Upload a banner image and set where it opens when customers tap it.
                </p>
              </div>
              <button className="btn tiny" type="button" onClick={closeModal}>Close</button>
            </div>
            <div className="form-grid">
              <label className="field wide">
                <span>Title</span>
                <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Weekend deal" />
              </label>
              <label className="field wide">
                <span>Redirect URL</span>
                <input
                  value={redirectUrl}
                  onChange={(e) => setRedirectUrl(e.target.value)}
                  placeholder="https://example.com/promo"
                />
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
                <span>Status</span>
                <select value={isActive ? 'active' : 'inactive'} onChange={(e) => setIsActive(e.target.value === 'active')}>
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                </select>
              </label>
              <label className="field wide">
                <span>Image{editing ? ' (optional replace)' : ''}</span>
                <input
                  type="file"
                  accept="image/*"
                  onChange={(e) => setImageFile(e.target.files?.[0] ?? null)}
                />
              </label>
              {editing?.imageUrl ? (
                <div className="field wide">
                  <span>Current image</span>
                  <img
                    src={editing.imageUrl}
                    alt=""
                    style={{ maxWidth: '100%', maxHeight: 160, borderRadius: 12, objectFit: 'cover', border: '1px solid var(--line)' }}
                  />
                </div>
              ) : null}
            </div>
            {formError ? <p className="error">{formError}</p> : null}
            <div className="modal-actions">
              <button className="btn" type="button" onClick={() => void save()} disabled={busy}>
                {busy ? 'Saving…' : editing ? 'Save changes' : 'Create offer'}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
