import { FormEvent, useEffect, useState } from 'react'
import {
  api,
  MerchantDetailItem,
  MerchantListItem,
  MerchantOperatingHourItem,
  MerchantProductCategoryItem,
  MerchantProductItem,
  ProductAddonGroupItem,
  SaveMerchantBody,
  SaveMerchantProductBody,
} from './api'
import { compressImageFile } from './compress-image'
import { MerchantPinMap } from './MerchantPinMap'

const DAY_LABELS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

function defaultHours(): MerchantOperatingHourItem[] {
  return DAY_LABELS.map((_, day) => ({
    dayOfWeek: day,
    isClosed: day === 0,
    openTime: day === 0 ? null : '08:00',
    closeTime: day === 0 ? null : '20:00',
  }))
}

function blankMerchantForm() {
  return {
    businessName: '',
    contactPerson: '',
    latitude: 13.4115,
    longitude: 121.1803,
    pinnedAddress: '',
    managedByMerchant: false,
    mobile: '',
    email: '',
    password: '',
    isActive: true,
    sortOrder: 0,
    operatingHours: defaultHours(),
  }
}

export function OperatorMerchantsPage() {
  const [items, setItems] = useState<MerchantListItem[] | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)

  function reload() {
    return api
      .operatorMerchants()
      .then((res) => setItems(res.items))
      .catch((err: Error) => setError(err.message))
  }

  useEffect(() => {
    void reload()
  }, [])

  if (selectedId) {
    return (
      <OperatorMerchantDetail
        merchantId={selectedId}
        onBack={() => {
          setSelectedId(null)
          void reload()
        }}
      />
    )
  }

  if (!items) return error ? <p className="error">{error}</p> : <p>Loading merchants…</p>

  return (
    <MerchantListView
      items={items}
      error={error}
      notice={notice}
      setError={setError}
      setNotice={setNotice}
      setItems={setItems}
      onOpen={setSelectedId}
    />
  )
}

function MerchantListView({
  items,
  error,
  notice,
  setError,
  setNotice,
  setItems,
  onOpen,
}: {
  items: MerchantListItem[]
  error: string
  notice: string
  setError: (v: string) => void
  setNotice: (v: string) => void
  setItems: (v: MerchantListItem[]) => void
  onOpen: (id: string) => void
}) {
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<MerchantDetailItem | null>(null)
  const [form, setForm] = useState(blankMerchantForm)
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)

  async function openCreate() {
    setEditing(null)
    setForm(blankMerchantForm())
    setFormError('')
    setOpen(true)
  }

  async function openEdit(id: string) {
    setFormError('')
    setBusy(true)
    try {
      const row = await api.operatorMerchant(id)
      setEditing(row)
      setForm({
        businessName: row.businessName,
        contactPerson: row.contactPerson,
        latitude: row.latitude,
        longitude: row.longitude,
        pinnedAddress: row.pinnedAddress,
        managedByMerchant: row.managedByMerchant,
        mobile: row.mobile,
        email: row.email,
        password: '',
        isActive: row.isActive,
        sortOrder: row.sortOrder,
        operatingHours: row.operatingHours?.length ? row.operatingHours : defaultHours(),
      })
      setOpen(true)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load merchant.')
    } finally {
      setBusy(false)
    }
  }

  async function save() {
    if (!form.businessName.trim() || !form.contactPerson.trim()) {
      setFormError('Business name and contact person are required.')
      return
    }
    if (!form.pinnedAddress.trim()) {
      setFormError('Pin an address on the map.')
      return
    }
    if (form.managedByMerchant) {
      if (!form.mobile.trim() || !form.email.trim()) {
        setFormError('Mobile and email are required when manage by merchant is on.')
        return
      }
      if (!editing && !form.password.trim()) {
        setFormError('Password is required when manage by merchant is on.')
        return
      }
    }
    setBusy(true)
    setFormError('')
    try {
      const body: SaveMerchantBody = {
        businessName: form.businessName.trim(),
        contactPerson: form.contactPerson.trim(),
        latitude: form.latitude,
        longitude: form.longitude,
        pinnedAddress: form.pinnedAddress.trim(),
        managedByMerchant: form.managedByMerchant,
        mobile: form.mobile.trim() || null,
        email: form.email.trim() || null,
        password: form.managedByMerchant ? form.password.trim() || null : null,
        isActive: form.isActive,
        sortOrder: form.sortOrder,
        operatingHours: form.operatingHours,
      }
      const row = editing
        ? await api.updateOperatorMerchant(editing.id, body)
        : await api.createOperatorMerchant(body)
      const list = await api.operatorMerchants()
      setItems(list.items)
      setNotice(`${row.businessName} saved.`)
      setOpen(false)
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Could not save merchant.')
    } finally {
      setBusy(false)
    }
  }

  async function toggle(item: MerchantListItem) {
    try {
      const row = await api.toggleOperatorMerchant(item.id)
      setItems(items.map((x) => (x.id === row.id ? { ...x, ...row } : x)))
      setNotice(`${row.businessName} is now ${row.isActive ? 'active' : 'inactive'}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not toggle merchant.')
    }
  }

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Merchants</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 560 }}>
            Stores for Pabili: pin location, hours, login (optional), and products with Foodpanda-style add-ons.
          </p>
        </div>
        <button className="btn" type="button" style={{ width: 'auto', whiteSpace: 'nowrap' }} onClick={() => void openCreate()}>
          Add merchant
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Business</th>
              <th>Contact</th>
              <th>Managed by</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={5}>No merchants yet. Add your first store.</td>
              </tr>
            ) : (
              items.map((item) => (
                <tr key={item.id}>
                  <td>
                    <strong>{item.businessName}</strong>
                    <div className="muted" style={{ fontSize: 12 }}>{item.pinnedAddress}</div>
                  </td>
                  <td>{item.contactPerson}</td>
                  <td>{item.managedByMerchant ? 'Merchant' : 'Operator'}</td>
                  <td>
                    <span className={`tag ${item.isActive ? 'active' : 'rejected'}`}>
                      {item.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td style={{ whiteSpace: 'nowrap', textAlign: 'right' }}>
                    <button className="btn tiny" type="button" onClick={() => onOpen(item.id)}>Products</button>{' '}
                    <button className="btn tiny" type="button" disabled={busy} onClick={() => void openEdit(item.id)}>Edit</button>{' '}
                    <button className={`btn tiny${item.isActive ? ' danger' : ''}`} type="button" onClick={() => void toggle(item)}>
                      {item.isActive ? 'Deactivate' : 'Activate'}
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {open ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setOpen(false)}>
          <div
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            style={{ maxWidth: 720, maxHeight: '90vh', overflow: 'auto' }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <div>
                <h2>{editing ? 'Edit merchant' : 'Add merchant'}</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>Pin the store location and set weekly hours.</p>
              </div>
              <button className="btn tiny" type="button" onClick={() => setOpen(false)}>Close</button>
            </div>
            <div className="form-grid">
              <label className="field">
                <span>Business name</span>
                <input value={form.businessName} onChange={(e) => setForm({ ...form, businessName: e.target.value })} />
              </label>
              <label className="field">
                <span>Contact person</span>
                <input value={form.contactPerson} onChange={(e) => setForm({ ...form, contactPerson: e.target.value })} />
              </label>
              <label className="field" style={{ gridColumn: '1 / -1' }}>
                <span>Pinned address</span>
                <input value={form.pinnedAddress} onChange={(e) => setForm({ ...form, pinnedAddress: e.target.value })} />
              </label>
              <div style={{ gridColumn: '1 / -1' }}>
                <MerchantPinMap
                  value={{ lat: form.latitude, lng: form.longitude, address: form.pinnedAddress }}
                  onChange={(pin) =>
                    setForm({
                      ...form,
                      latitude: pin.lat,
                      longitude: pin.lng,
                      pinnedAddress: pin.address,
                    })
                  }
                />
              </div>
              <label className="field" style={{ gridColumn: '1 / -1' }}>
                <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <input
                    type="checkbox"
                    checked={form.managedByMerchant}
                    onChange={(e) => setForm({ ...form, managedByMerchant: e.target.checked })}
                  />
                  Manage by merchant
                </span>
                <span className="muted" style={{ fontSize: 12 }}>
                  When checked, create a merchant login (mobile, email, password). When unchecked, Operator manages the store.
                </span>
              </label>
              {form.managedByMerchant ? (
                <>
                  <label className="field">
                    <span>Mobile</span>
                    <input value={form.mobile} onChange={(e) => setForm({ ...form, mobile: e.target.value })} />
                  </label>
                  <label className="field">
                    <span>Email</span>
                    <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
                  </label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}>
                    <span>Password {editing ? '(leave blank to keep)' : ''}</span>
                    <input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
                  </label>
                </>
              ) : null}
              <label className="field">
                <span>Sort order</span>
                <input
                  type="number"
                  value={form.sortOrder}
                  onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) || 0 })}
                />
              </label>
              <label className="field">
                <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
                  Active
                </span>
              </label>
              <div style={{ gridColumn: '1 / -1' }}>
                <h3 style={{ margin: '8px 0' }}>Operating hours</h3>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Day</th>
                        <th>Closed</th>
                        <th>Open</th>
                        <th>Close</th>
                      </tr>
                    </thead>
                    <tbody>
                      {form.operatingHours.map((h) => (
                        <tr key={h.dayOfWeek}>
                          <td>{DAY_LABELS[h.dayOfWeek]}</td>
                          <td>
                            <input
                              type="checkbox"
                              checked={h.isClosed}
                              onChange={(e) => {
                                const next = form.operatingHours.map((x) =>
                                  x.dayOfWeek === h.dayOfWeek
                                    ? {
                                        ...x,
                                        isClosed: e.target.checked,
                                        openTime: e.target.checked ? null : x.openTime || '08:00',
                                        closeTime: e.target.checked ? null : x.closeTime || '20:00',
                                      }
                                    : x,
                                )
                                setForm({ ...form, operatingHours: next })
                              }}
                            />
                          </td>
                          <td>
                            <input
                              type="time"
                              disabled={h.isClosed}
                              value={h.openTime ?? ''}
                              onChange={(e) => {
                                const next = form.operatingHours.map((x) =>
                                  x.dayOfWeek === h.dayOfWeek ? { ...x, openTime: e.target.value || null } : x,
                                )
                                setForm({ ...form, operatingHours: next })
                              }}
                            />
                          </td>
                          <td>
                            <input
                              type="time"
                              disabled={h.isClosed}
                              value={h.closeTime ?? ''}
                              onChange={(e) => {
                                const next = form.operatingHours.map((x) =>
                                  x.dayOfWeek === h.dayOfWeek ? { ...x, closeTime: e.target.value || null } : x,
                                )
                                setForm({ ...form, operatingHours: next })
                              }}
                            />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
            {formError ? <p className="error">{formError}</p> : null}
            <div className="modal-actions" style={{ marginTop: 16, display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
              <button className="btn" type="button" disabled={busy} onClick={() => void save()}>
                {busy ? 'Saving…' : 'Save'}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}

function OperatorMerchantDetail({ merchantId, onBack }: { merchantId: string; onBack: () => void }) {
  const [merchant, setMerchant] = useState<MerchantDetailItem | null>(null)
  const [categories, setCategories] = useState<MerchantProductCategoryItem[]>([])
  const [products, setProducts] = useState<MerchantProductItem[]>([])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  const [catName, setCatName] = useState('')
  const [productOpen, setProductOpen] = useState(false)
  const [editingProduct, setEditingProduct] = useState<MerchantProductItem | null>(null)
  const [productForm, setProductForm] = useState({
    name: '',
    description: '',
    basePrice: '0',
    categoryId: '',
    availableOnStorefront: true,
    sortOrder: 0,
  })
  const [addonGroups, setAddonGroups] = useState<ProductAddonGroupItem[]>([])
  const [productError, setProductError] = useState('')

  async function loadAll() {
    const [m, c, p] = await Promise.all([
      api.operatorMerchant(merchantId),
      api.operatorMerchantCategories(merchantId),
      api.operatorMerchantProducts(merchantId),
    ])
    setMerchant(m)
    setCategories(c.items)
    setProducts(p.items)
  }

  useEffect(() => {
    loadAll().catch((err: Error) => setError(err.message))
  }, [merchantId])

  async function uploadImage(kind: 'logo' | 'background', file: File | null) {
    if (!file || !merchant) return
    setBusy(true)
    setError('')
    try {
      const compressed = await compressImageFile(file)
      const row =
        kind === 'logo'
          ? await api.uploadOperatorMerchantLogo(merchant.id, compressed)
          : await api.uploadOperatorMerchantBackground(merchant.id, compressed)
      setMerchant(row)
      setNotice(`${kind === 'logo' ? 'Logo' : 'Background'} updated.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Upload failed.')
    } finally {
      setBusy(false)
    }
  }

  async function addCategory(e: FormEvent) {
    e.preventDefault()
    if (!catName.trim()) return
    try {
      const row = await api.createOperatorMerchantCategory(merchantId, {
        name: catName.trim(),
        sortOrder: categories.length,
        isActive: true,
      })
      setCategories([...categories, row])
      setCatName('')
      setNotice('Category added.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not add category.')
    }
  }

  function openProduct(product?: MerchantProductItem) {
    if (product) {
      setEditingProduct(product)
      setProductForm({
        name: product.name,
        description: product.description,
        basePrice: String(product.basePrice),
        categoryId: product.categoryId ?? '',
        availableOnStorefront: product.availableOnStorefront,
        sortOrder: product.sortOrder,
      })
      setAddonGroups(product.addonGroups?.length ? product.addonGroups : [])
    } else {
      setEditingProduct(null)
      setProductForm({
        name: '',
        description: '',
        basePrice: '0',
        categoryId: '',
        availableOnStorefront: true,
        sortOrder: products.length,
      })
      setAddonGroups([])
    }
    setProductError('')
    setProductOpen(true)
  }

  async function saveProduct() {
    const price = Number(productForm.basePrice)
    if (!productForm.name.trim() || !Number.isFinite(price) || price < 0) {
      setProductError('Name and a valid base price are required.')
      return
    }
    for (const g of addonGroups) {
      if (!g.name.trim()) {
        setProductError('Each add-on group needs a name.')
        return
      }
      if (g.minSelect < 0 || g.maxSelect < 1 || g.minSelect > g.maxSelect) {
        setProductError(`Add-on group "${g.name}" has invalid min/max.`)
        return
      }
      if (!g.options.length) {
        setProductError(`Add-on group "${g.name}" needs at least one option.`)
        return
      }
    }
    setBusy(true)
    setProductError('')
    try {
      const body: SaveMerchantProductBody = {
        name: productForm.name.trim(),
        description: productForm.description.trim(),
        basePrice: price,
        categoryId: productForm.categoryId || null,
        availableOnStorefront: productForm.availableOnStorefront,
        sortOrder: productForm.sortOrder,
      }
      let row = editingProduct
        ? await api.updateOperatorMerchantProduct(merchantId, editingProduct.id, body)
        : await api.createOperatorMerchantProduct(merchantId, body)
      row = await api.saveOperatorMerchantProductAddons(merchantId, row.id, addonGroups)
      const list = await api.operatorMerchantProducts(merchantId)
      setProducts(list.items)
      setProductOpen(false)
      setNotice(`${row.name} saved.`)
    } catch (err) {
      setProductError(err instanceof Error ? err.message : 'Could not save product.')
    } finally {
      setBusy(false)
    }
  }

  async function toggleStorefront(product: MerchantProductItem) {
    try {
      const row = await api.toggleOperatorMerchantProductStorefront(merchantId, product.id)
      setProducts(products.map((x) => (x.id === row.id ? row : x)))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not update storefront flag.')
    }
  }

  async function removeProduct(product: MerchantProductItem) {
    if (!confirm(`Delete ${product.name}?`)) return
    try {
      await api.deleteOperatorMerchantProduct(merchantId, product.id)
      setProducts(products.filter((x) => x.id !== product.id))
      setNotice('Product deleted.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete product.')
    }
  }

  if (!merchant) return error ? <p className="error">{error}</p> : <p>Loading merchant…</p>

  return (
    <div className="form-sections">
      <div className="card">
        <div className="toolbar">
          <div>
            <button className="btn tiny" type="button" onClick={onBack}>← Back</button>
            <h2 style={{ margin: '10px 0 0' }}>{merchant.businessName}</h2>
            <p className="muted" style={{ margin: '6px 0 0' }}>{merchant.pinnedAddress}</p>
          </div>
        </div>
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}
        <div className="form-grid" style={{ marginTop: 12 }}>
          <label className="field">
            <span>Store logo</span>
            {merchant.logoUrl ? <img src={merchant.logoUrl} alt="" style={{ maxHeight: 64, display: 'block', marginBottom: 8 }} /> : null}
            <input type="file" accept="image/*" disabled={busy} onChange={(e) => void uploadImage('logo', e.target.files?.[0] ?? null)} />
          </label>
          <label className="field">
            <span>Store background</span>
            {merchant.backgroundUrl ? (
              <img src={merchant.backgroundUrl} alt="" style={{ maxHeight: 64, display: 'block', marginBottom: 8, maxWidth: '100%' }} />
            ) : null}
            <input type="file" accept="image/*" disabled={busy} onChange={(e) => void uploadImage('background', e.target.files?.[0] ?? null)} />
          </label>
        </div>
      </div>

      <div className="card">
        <div className="toolbar">
          <h3 style={{ margin: 0 }}>Categories</h3>
        </div>
        <form onSubmit={(e) => void addCategory(e)} style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
          <input
            placeholder="e.g. Meals, Drinks"
            value={catName}
            onChange={(e) => setCatName(e.target.value)}
            style={{ flex: 1 }}
          />
          <button className="btn" type="submit" style={{ width: 'auto' }}>Add</button>
        </form>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {categories.length === 0 ? (
                <tr><td colSpan={2}>No categories yet.</td></tr>
              ) : categories.map((c) => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td>{c.isActive ? 'Active' : 'Inactive'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="card">
        <div className="toolbar">
          <h3 style={{ margin: 0 }}>Products</h3>
          <button className="btn" type="button" style={{ width: 'auto' }} onClick={() => openProduct()}>
            Add product
          </button>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Category</th>
                <th>Price</th>
                <th>Storefront</th>
                <th>Add-ons</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {products.length === 0 ? (
                <tr><td colSpan={6}>No products yet.</td></tr>
              ) : products.map((p) => (
                <tr key={p.id}>
                  <td><strong>{p.name}</strong></td>
                  <td>{p.categoryName || '—'}</td>
                  <td>₱{p.basePrice.toFixed(2)}</td>
                  <td>
                    <button className="btn tiny" type="button" onClick={() => void toggleStorefront(p)}>
                      {p.availableOnStorefront ? 'On' : 'Off'}
                    </button>
                  </td>
                  <td>{p.addonGroups?.length ?? 0} groups</td>
                  <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                    <button className="btn tiny" type="button" onClick={() => openProduct(p)}>Edit</button>{' '}
                    <button className="btn tiny danger" type="button" onClick={() => void removeProduct(p)}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {productOpen ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setProductOpen(false)}>
          <div
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            style={{ maxWidth: 720, maxHeight: '90vh', overflow: 'auto' }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <h2>{editingProduct ? 'Edit product' : 'Add product'}</h2>
              <button className="btn tiny" type="button" onClick={() => setProductOpen(false)}>Close</button>
            </div>
            <div className="form-grid">
              <label className="field">
                <span>Name</span>
                <input value={productForm.name} onChange={(e) => setProductForm({ ...productForm, name: e.target.value })} />
              </label>
              <label className="field">
                <span>Base price</span>
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  value={productForm.basePrice}
                  onChange={(e) => setProductForm({ ...productForm, basePrice: e.target.value })}
                />
              </label>
              <label className="field" style={{ gridColumn: '1 / -1' }}>
                <span>Description</span>
                <textarea
                  rows={2}
                  value={productForm.description}
                  onChange={(e) => setProductForm({ ...productForm, description: e.target.value })}
                />
              </label>
              <label className="field">
                <span>Category</span>
                <select
                  value={productForm.categoryId}
                  onChange={(e) => setProductForm({ ...productForm, categoryId: e.target.value })}
                >
                  <option value="">None</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <input
                    type="checkbox"
                    checked={productForm.availableOnStorefront}
                    onChange={(e) => setProductForm({ ...productForm, availableOnStorefront: e.target.checked })}
                  />
                  Available on storefront
                </span>
              </label>
            </div>

            <div style={{ marginTop: 16 }}>
              <div className="toolbar">
                <h3 style={{ margin: 0 }}>Add-on groups</h3>
                <button
                  className="btn tiny"
                  type="button"
                  onClick={() =>
                    setAddonGroups([
                      ...addonGroups,
                      {
                        name: 'Extras',
                        minSelect: 0,
                        maxSelect: 3,
                        sortOrder: addonGroups.length,
                        isActive: true,
                        options: [{ name: '', priceDelta: 0, sortOrder: 0, isActive: true }],
                      },
                    ])
                  }
                >
                  Add group
                </button>
              </div>
              {addonGroups.map((group, gi) => (
                <div key={gi} className="card" style={{ marginTop: 10, padding: 12 }}>
                  <div className="form-grid">
                    <label className="field">
                      <span>Group name</span>
                      <input
                        value={group.name}
                        onChange={(e) => {
                          const next = [...addonGroups]
                          next[gi] = { ...group, name: e.target.value }
                          setAddonGroups(next)
                        }}
                      />
                    </label>
                    <label className="field">
                      <span>Min select</span>
                      <input
                        type="number"
                        min={0}
                        value={group.minSelect}
                        onChange={(e) => {
                          const next = [...addonGroups]
                          next[gi] = { ...group, minSelect: Number(e.target.value) || 0 }
                          setAddonGroups(next)
                        }}
                      />
                    </label>
                    <label className="field">
                      <span>Max select</span>
                      <input
                        type="number"
                        min={1}
                        value={group.maxSelect}
                        onChange={(e) => {
                          const next = [...addonGroups]
                          next[gi] = { ...group, maxSelect: Number(e.target.value) || 1 }
                          setAddonGroups(next)
                        }}
                      />
                    </label>
                  </div>
                  {group.options.map((opt, oi) => (
                    <div key={oi} style={{ display: 'flex', gap: 8, marginTop: 8 }}>
                      <input
                        placeholder="Option name (e.g. Extra rice)"
                        value={opt.name}
                        style={{ flex: 1 }}
                        onChange={(e) => {
                          const next = [...addonGroups]
                          const options = [...group.options]
                          options[oi] = { ...opt, name: e.target.value }
                          next[gi] = { ...group, options }
                          setAddonGroups(next)
                        }}
                      />
                      <input
                        type="number"
                        min={0}
                        step="0.01"
                        placeholder="+₱"
                        value={opt.priceDelta}
                        style={{ width: 100 }}
                        onChange={(e) => {
                          const next = [...addonGroups]
                          const options = [...group.options]
                          options[oi] = { ...opt, priceDelta: Number(e.target.value) || 0 }
                          next[gi] = { ...group, options }
                          setAddonGroups(next)
                        }}
                      />
                      <button
                        className="btn tiny danger"
                        type="button"
                        onClick={() => {
                          const next = [...addonGroups]
                          next[gi] = { ...group, options: group.options.filter((_, i) => i !== oi) }
                          setAddonGroups(next)
                        }}
                      >
                        ×
                      </button>
                    </div>
                  ))}
                  <div style={{ marginTop: 8, display: 'flex', gap: 8 }}>
                    <button
                      className="btn tiny"
                      type="button"
                      onClick={() => {
                        const next = [...addonGroups]
                        next[gi] = {
                          ...group,
                          options: [
                            ...group.options,
                            { name: '', priceDelta: 0, sortOrder: group.options.length, isActive: true },
                          ],
                        }
                        setAddonGroups(next)
                      }}
                    >
                      Add option
                    </button>
                    <button
                      className="btn tiny danger"
                      type="button"
                      onClick={() => setAddonGroups(addonGroups.filter((_, i) => i !== gi))}
                    >
                      Remove group
                    </button>
                  </div>
                </div>
              ))}
            </div>

            {productError ? <p className="error">{productError}</p> : null}
            <div style={{ marginTop: 16, display: 'flex', justifyContent: 'flex-end' }}>
              <button className="btn" type="button" disabled={busy} onClick={() => void saveProduct()}>
                {busy ? 'Saving…' : 'Save product'}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
