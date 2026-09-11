import { useEffect, useState } from 'react'
import { api, MerchantListItem, MerchantProductCategoryItem } from './api'

type SuggestRow = { id: string; name: string; extra?: string }

function MerchantSuggest({
  query,
  onQuery,
  items,
  placeholder,
  onPick,
}: {
  query: string
  onQuery: (value: string) => void
  items: SuggestRow[]
  placeholder: string
  onPick: (item: SuggestRow) => void
}) {
  const [open, setOpen] = useState(false)
  const q = query.trim().toLowerCase()
  const filtered = items
    .filter((item) => !q || item.name.toLowerCase().includes(q) || (item.extra ?? '').toLowerCase().includes(q))
    .sort((a, b) => {
      if (!q) return a.name.localeCompare(b.name)
      const aStarts = a.name.toLowerCase().startsWith(q)
      const bStarts = b.name.toLowerCase().startsWith(q)
      if (aStarts !== bStarts) return aStarts ? -1 : 1
      return a.name.localeCompare(b.name)
    })

  return (
    <div className="ac">
      <input
        value={query}
        placeholder={placeholder}
        autoComplete="off"
        onChange={(e) => {
          onQuery(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => window.setTimeout(() => setOpen(false), 160)}
      />
      {open ? (
        <div className="suggest">
          {filtered.length === 0 ? (
            <div className="suggest-empty">No matches</div>
          ) : (
            filtered.map((item) => (
              <button
                key={item.id}
                type="button"
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  onPick(item)
                  setOpen(false)
                }}
              >
                <span className="ac-text">
                  <span className="suggest-name">{item.name}</span>
                  {item.extra ? <small>{item.extra}</small> : null}
                </span>
              </button>
            ))
          )}
        </div>
      ) : null}
    </div>
  )
}

export function OperatorProductCategoriesPage() {
  const [merchants, setMerchants] = useState<MerchantListItem[]>([])
  const [merchantId, setMerchantId] = useState('')
  const [merchantQuery, setMerchantQuery] = useState('')
  const [categories, setCategories] = useState<MerchantProductCategoryItem[]>([])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [catOpen, setCatOpen] = useState(false)
  const [editingCat, setEditingCat] = useState<MerchantProductCategoryItem | null>(null)
  const [catForm, setCatForm] = useState({ name: '', sortOrder: 0, isActive: true })
  const [catError, setCatError] = useState('')

  useEffect(() => {
    api
      .operatorMerchants()
      .then((res) => setMerchants(res.items))
      .catch((err: Error) => setError(err.message))
  }, [])

  useEffect(() => {
    if (!merchantId) {
      setCategories([])
      return
    }
    setError('')
    api
      .operatorMerchantCategories(merchantId)
      .then((res) => setCategories(res.items))
      .catch((err: Error) => setError(err.message))
  }, [merchantId])

  function openCategory(cat?: MerchantProductCategoryItem) {
    if (!merchantId) {
      setError('Select a merchant first.')
      return
    }
    if (cat) {
      setEditingCat(cat)
      setCatForm({ name: cat.name, sortOrder: cat.sortOrder, isActive: cat.isActive })
    } else {
      setEditingCat(null)
      setCatForm({ name: '', sortOrder: categories.length, isActive: true })
    }
    setCatError('')
    setCatOpen(true)
  }

  async function saveCategory() {
    if (!merchantId) return
    if (!catForm.name.trim()) {
      setCatError('Category name is required.')
      return
    }
    setBusy(true)
    setCatError('')
    try {
      if (editingCat?.id) {
        await api.updateOperatorMerchantCategory(merchantId, editingCat.id, {
          name: catForm.name.trim(),
          sortOrder: catForm.sortOrder,
          isActive: catForm.isActive,
        })
      } else {
        await api.createOperatorMerchantCategory(merchantId, {
          name: catForm.name.trim(),
          sortOrder: catForm.sortOrder,
          isActive: catForm.isActive,
        })
      }
      const c = await api.operatorMerchantCategories(merchantId)
      setCategories(c.items)
      setCatOpen(false)
      setNotice(editingCat ? 'Category updated.' : 'Category added.')
    } catch (err) {
      setCatError(err instanceof Error ? err.message : 'Could not save category.')
    } finally {
      setBusy(false)
    }
  }

  async function removeCategory(cat: MerchantProductCategoryItem) {
    if (!merchantId) return
    if (!confirm(`Delete category "${cat.name}"? Products in it will become uncategorized.`)) return
    try {
      await api.deleteOperatorMerchantCategory(merchantId, cat.id)
      setCategories(categories.filter((x) => x.id !== cat.id))
      setNotice('Category deleted.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete category.')
    }
  }

  const selected = merchants.find((m) => m.id === merchantId)

  return (
    <div className="form-sections">
      <div className="card">
        <div className="toolbar">
          <div>
            <h2 style={{ margin: 0 }}>Product categories</h2>
            <p className="muted" style={{ margin: '6px 0 0' }}>
              Manage categories per merchant. Assign them when editing products.
            </p>
          </div>
        </div>
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}
        <label className="field" style={{ marginTop: 12, maxWidth: 420 }}>
          <span>Merchant</span>
          <MerchantSuggest
            query={merchantQuery}
            onQuery={(value) => {
              setMerchantQuery(value)
              if (selected && value !== selected.businessName) {
                setMerchantId('')
              }
            }}
            placeholder="Search merchant…"
            items={merchants.map((m) => ({
              id: m.id,
              name: m.businessName,
              extra: m.pinnedAddress || m.contactPerson,
            }))}
            onPick={(item) => {
              setMerchantId(item.id)
              setMerchantQuery(item.name)
              setNotice('')
            }}
          />
        </label>
      </div>

      {merchantId ? (
        <div className="card">
          <div className="toolbar">
            <div>
              <h3 style={{ margin: 0 }}>{selected?.businessName ?? 'Categories'}</h3>
              <p className="muted" style={{ margin: '6px 0 0' }}>{categories.length} categor{categories.length === 1 ? 'y' : 'ies'}</p>
            </div>
            <button className="btn" type="button" style={{ width: 'auto' }} onClick={() => openCategory()}>
              Add category
            </button>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Sort</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {categories.length === 0 ? (
                  <tr><td colSpan={4}>No categories yet for this merchant.</td></tr>
                ) : categories.map((c) => (
                  <tr key={c.id}>
                    <td><strong>{c.name}</strong></td>
                    <td>{c.sortOrder}</td>
                    <td>{c.isActive ? 'Active' : 'Inactive'}</td>
                    <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                      <button className="btn tiny" type="button" onClick={() => openCategory(c)}>Edit</button>{' '}
                      <button className="btn tiny danger" type="button" onClick={() => void removeCategory(c)}>Delete</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <div className="card">
          <p className="muted" style={{ margin: 0 }}>Select a merchant to manage its product categories.</p>
        </div>
      )}

      {catOpen ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setCatOpen(false)}>
          <div className="modal-panel merchant-modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <div className="modal-head">
              <h2>{editingCat ? 'Edit category' : 'New category'}</h2>
              <button className="btn tiny" type="button" onClick={() => setCatOpen(false)}>Close</button>
            </div>
            <div className="merchant-modal-body">
              <section className="form-section">
                <div className="form-grid">
                  <label className="field">
                    <span>Name</span>
                    <input value={catForm.name} onChange={(e) => setCatForm({ ...catForm, name: e.target.value })} placeholder="Meals, Drinks…" />
                  </label>
                  <label className="field">
                    <span>Sort order</span>
                    <input type="number" value={catForm.sortOrder} onChange={(e) => setCatForm({ ...catForm, sortOrder: Number(e.target.value) || 0 })} />
                  </label>
                  <label className="field check-field">
                    <span className="check">
                      <input
                        type="checkbox"
                        checked={catForm.isActive}
                        onChange={(e) => setCatForm({ ...catForm, isActive: e.target.checked })}
                      />
                      <span>Active</span>
                    </span>
                  </label>
                </div>
              </section>
            </div>
            {catError ? <p className="error" style={{ padding: '0 22px' }}>{catError}</p> : null}
            <div className="merchant-modal-footer">
              <button className="btn tiny" type="button" onClick={() => setCatOpen(false)}>Cancel</button>
              <button className="btn" type="button" disabled={busy} onClick={() => void saveCategory()}>
                {busy ? 'Saving…' : 'Save category'}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
