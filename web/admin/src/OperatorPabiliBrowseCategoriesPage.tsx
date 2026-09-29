import { useEffect, useState } from 'react'
import { api, OperatorPabiliBrowseCategoryItem, SaveOperatorPabiliBrowseCategoryBody } from './api'

export function OperatorPabiliBrowseCategoriesPage() {
  const [items, setItems] = useState<OperatorPabiliBrowseCategoryItem[] | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<OperatorPabiliBrowseCategoryItem | null>(null)
  const [name, setName] = useState('')
  const [sortOrder, setSortOrder] = useState('0')
  const [isActive, setIsActive] = useState(true)
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.operatorPabiliBrowseCategories()
      .then((res) => setItems(res.items))
      .catch((err: Error) => setError(err.message))
  }, [])

  function openCreate() {
    setEditing(null)
    setName('')
    setSortOrder(String(items?.length ?? 0))
    setIsActive(true)
    setFormError('')
    setOpen(true)
  }

  function openEdit(item: OperatorPabiliBrowseCategoryItem) {
    setEditing(item)
    setName(item.name)
    setSortOrder(String(item.sortOrder))
    setIsActive(item.isActive)
    setFormError('')
    setOpen(true)
  }

  function closeModal() {
    setOpen(false)
    setEditing(null)
    setFormError('')
  }

  function body(): SaveOperatorPabiliBrowseCategoryBody {
    const order = Math.floor(Number(sortOrder))
    return {
      name: name.trim(),
      isActive,
      sortOrder: Number.isFinite(order) ? order : 0,
    }
  }

  async function save() {
    if (!name.trim()) {
      setFormError('Name is required.')
      return
    }
    setBusy(true)
    setFormError('')
    try {
      const payload = body()
      const row = editing
        ? await api.updateOperatorPabiliBrowseCategory(editing.id, payload)
        : await api.createOperatorPabiliBrowseCategory(payload)
      setItems((prev) => {
        const list = prev ?? []
        const next = editing
          ? list.map((x) => (x.id === row.id ? row : x))
          : [...list, row]
        return next.sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name))
      })
      setNotice(editing ? 'Category updated.' : 'Category added.')
      closeModal()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Could not save category.')
    } finally {
      setBusy(false)
    }
  }

  async function toggle(item: OperatorPabiliBrowseCategoryItem) {
    setError('')
    try {
      const row = await api.toggleOperatorPabiliBrowseCategory(item.id)
      setItems((prev) => (prev ?? []).map((x) => (x.id === row.id ? row : x)))
      setNotice(`${row.name} is now ${row.isActive ? 'active' : 'inactive'}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not toggle category.')
    }
  }

  async function remove(item: OperatorPabiliBrowseCategoryItem) {
    if (!window.confirm(`Delete “${item.name}”?`)) return
    setError('')
    try {
      await api.deleteOperatorPabiliBrowseCategory(item.id)
      setItems((prev) => (prev ?? []).filter((x) => x.id !== item.id))
      setNotice('Category deleted.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete category.')
    }
  }

  if (!items) return error ? <p className="error">{error}</p> : <p>Loading browse categories…</p>

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Browse categories</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 560 }}>
            These chips appear under the Pabili search bar on the customer home. Customers tap a chip to search that label.
          </p>
        </div>
        <button className="btn" type="button" style={{ width: 'auto', whiteSpace: 'nowrap' }} onClick={openCreate}>
          Add category
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Order</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={4}>No browse categories yet. Add chips like Food, Groceries, or Pharmacy.</td>
              </tr>
            ) : items.map((item) => (
              <tr key={item.id}>
                <td><strong>{item.name}</strong></td>
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
            aria-labelledby="pabili-browse-cat-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <div>
                <h2 id="pabili-browse-cat-title">{editing ? 'Edit category' : 'Add category'}</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  Short labels work best (e.g. Food, Drinks).
                </p>
              </div>
              <button className="btn tiny" type="button" onClick={closeModal}>Close</button>
            </div>
            <div className="stack" style={{ gap: 12 }}>
              <label>
                Name
                <input value={name} onChange={(e) => setName(e.target.value)} maxLength={40} placeholder="Food" />
              </label>
              <label>
                Sort order
                <input value={sortOrder} onChange={(e) => setSortOrder(e.target.value)} inputMode="numeric" />
              </label>
              <label className="check">
                <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
                Active (visible to customers)
              </label>
              {formError ? <p className="error">{formError}</p> : null}
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                <button className="btn" type="button" disabled={busy} onClick={() => void save()}>
                  {busy ? 'Saving…' : editing ? 'Save changes' : 'Add category'}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
