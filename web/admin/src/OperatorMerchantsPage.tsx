import { useEffect, useState } from 'react'
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

type SuggestRow = { id: string; name: string; extra?: string; photoUrl?: string | null }

function ProductSuggest({
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
                {item.photoUrl ? (
                  <img src={item.photoUrl} alt="" className="ac-avatar" style={{ objectFit: 'cover' }} />
                ) : (
                  <span className="ac-avatar ac-initial">{item.name.slice(0, 1).toUpperCase()}</span>
                )}
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
            className="modal-panel merchant-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="merchant-modal-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <div>
                <h2 id="merchant-modal-title">{editing ? 'Edit merchant' : 'Add merchant'}</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  Store profile, map pin, access, and weekly hours.
                </p>
              </div>
              <button className="btn tiny" type="button" onClick={() => setOpen(false)}>Close</button>
            </div>

            <div className="merchant-modal-body">
              <section className="form-section">
                <h3>Store</h3>
                <p className="form-hint">Basic identity shown on the storefront later.</p>
                <div className="form-grid">
                  <label className="field">
                    <span>Business name</span>
                    <input
                      value={form.businessName}
                      onChange={(e) => setForm({ ...form, businessName: e.target.value })}
                      placeholder="e.g. Aling Nena’s Carinderia"
                    />
                  </label>
                  <label className="field">
                    <span>Contact person</span>
                    <input
                      value={form.contactPerson}
                      onChange={(e) => setForm({ ...form, contactPerson: e.target.value })}
                      placeholder="Owner or manager name"
                    />
                  </label>
                  <label className="field">
                    <span>Sort order</span>
                    <input
                      type="number"
                      value={form.sortOrder}
                      onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) || 0 })}
                    />
                  </label>
                  <label className="field check-field">
                    <span className="check">
                      <input
                        type="checkbox"
                        checked={form.isActive}
                        onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                      />
                      <span>Active store</span>
                    </span>
                  </label>
                </div>
              </section>

              <section className="form-section">
                <h3>Location</h3>
                <p className="form-hint">Search or click the map to drop the store pin. Address fills in from Google.</p>
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
                <label className="field" style={{ marginTop: 12 }}>
                  <span>Pinned address</span>
                  <input
                    value={form.pinnedAddress}
                    onChange={(e) => setForm({ ...form, pinnedAddress: e.target.value })}
                    placeholder="Updates when you move the pin"
                  />
                </label>
              </section>

              <section className="form-section">
                <h3>Access</h3>
                <label className="check" style={{ marginBottom: 10 }}>
                  <input
                    type="checkbox"
                    checked={form.managedByMerchant}
                    onChange={(e) => setForm({ ...form, managedByMerchant: e.target.checked })}
                  />
                  <span>
                    Manage by merchant
                    <span className="form-hint" style={{ display: 'block', margin: '4px 0 0', fontWeight: 500 }}>
                      On: merchant gets a login. Off: Operator manages this store.
                    </span>
                  </span>
                </label>
                {form.managedByMerchant ? (
                  <div className="form-grid">
                    <label className="field">
                      <span>Mobile</span>
                      <input
                        value={form.mobile}
                        onChange={(e) => setForm({ ...form, mobile: e.target.value })}
                        placeholder="09xxxxxxxxx"
                      />
                    </label>
                    <label className="field">
                      <span>Email</span>
                      <input
                        type="email"
                        value={form.email}
                        onChange={(e) => setForm({ ...form, email: e.target.value })}
                        placeholder="store@email.com"
                      />
                    </label>
                    <label className="field" style={{ gridColumn: '1 / -1' }}>
                      <span>Password {editing ? '(leave blank to keep current)' : ''}</span>
                      <input
                        type="password"
                        value={form.password}
                        onChange={(e) => setForm({ ...form, password: e.target.value })}
                        placeholder={editing ? '••••••••' : 'At least 6 characters'}
                        autoComplete="new-password"
                      />
                    </label>
                  </div>
                ) : (
                  <p className="form-hint" style={{ margin: 0 }}>No merchant login will be created.</p>
                )}
              </section>

              <section className="form-section">
                <h3>Operating hours</h3>
                <p className="form-hint">Philippine local time. Closed days skip open/close.</p>
                <div className="merchant-hours">
                  {form.operatingHours.map((h) => (
                    <div key={h.dayOfWeek} className={`merchant-hour-row${h.isClosed ? ' is-closed' : ''}`}>
                      <div className="merchant-hour-day">{DAY_LABELS[h.dayOfWeek]}</div>
                      <label className="check merchant-hour-closed">
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
                        <span>Closed</span>
                      </label>
                      <input
                        type="time"
                        className="merchant-hour-time"
                        disabled={h.isClosed}
                        value={h.openTime ?? ''}
                        onChange={(e) => {
                          const next = form.operatingHours.map((x) =>
                            x.dayOfWeek === h.dayOfWeek ? { ...x, openTime: e.target.value || null } : x,
                          )
                          setForm({ ...form, operatingHours: next })
                        }}
                      />
                      <span className="merchant-hour-sep">to</span>
                      <input
                        type="time"
                        className="merchant-hour-time"
                        disabled={h.isClosed}
                        value={h.closeTime ?? ''}
                        onChange={(e) => {
                          const next = form.operatingHours.map((x) =>
                            x.dayOfWeek === h.dayOfWeek ? { ...x, closeTime: e.target.value || null } : x,
                          )
                          setForm({ ...form, operatingHours: next })
                        }}
                      />
                    </div>
                  ))}
                </div>
              </section>
            </div>

            {formError ? <p className="error">{formError}</p> : null}
            <div className="merchant-modal-footer">
              <button className="btn tiny" type="button" onClick={() => setOpen(false)}>Cancel</button>
              <button className="btn" type="button" disabled={busy} onClick={() => void save()}>
                {busy ? 'Saving…' : editing ? 'Save changes' : 'Create merchant'}
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
  const [library, setLibrary] = useState<ProductAddonGroupItem[]>([])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  const [productQuery, setProductQuery] = useState('')
  const [productOpen, setProductOpen] = useState(false)
  const [editingProduct, setEditingProduct] = useState<MerchantProductItem | null>(null)
  const [productForm, setProductForm] = useState({
    name: '',
    description: '',
    basePrice: '0',
    sellingPrice: '0',
    categoryId: '',
    availableOnStorefront: true,
    availableAllDay: true,
    availableFromTime: '08:00',
    availableToTime: '20:00',
    sortOrder: 0,
  })
  const [adoptedGroupIds, setAdoptedGroupIds] = useState<string[]>([])
  const [productImageFile, setProductImageFile] = useState<File | null>(null)
  const [productError, setProductError] = useState('')

  const [libOpen, setLibOpen] = useState(false)
  const [editingLib, setEditingLib] = useState<ProductAddonGroupItem | null>(null)
  const [libForm, setLibForm] = useState<ProductAddonGroupItem>({
    name: '',
    minSelect: 0,
    maxSelect: 1,
    sortOrder: 0,
    isActive: true,
    options: [{ name: '', basePrice: 0, sellingPrice: 0, sortOrder: 0, isActive: true }],
  })
  const [libError, setLibError] = useState('')

  async function loadAll() {
    const [m, c, p, g] = await Promise.all([
      api.operatorMerchant(merchantId),
      api.operatorMerchantCategories(merchantId),
      api.operatorMerchantProducts(merchantId),
      api.operatorMerchantAddonGroups(merchantId),
    ])
    setMerchant(m)
    setCategories(c.items)
    setProducts(p.items)
    setLibrary(g.items)
  }

  useEffect(() => {
    loadAll().catch((err: Error) => setError(err.message))
  }, [merchantId])

  async function uploadMerchantImage(kind: 'logo' | 'background', file: File | null) {
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

  function openLibrary(group?: ProductAddonGroupItem) {
    if (group) {
      setEditingLib(group)
      setLibForm({
        ...group,
        options: group.options?.length
          ? group.options
          : [{ name: '', basePrice: 0, sellingPrice: 0, sortOrder: 0, isActive: true }],
      })
    } else {
      setEditingLib(null)
      setLibForm({
        name: '',
        minSelect: 0,
        maxSelect: 1,
        sortOrder: library.length,
        isActive: true,
        options: [{ name: '', basePrice: 0, sellingPrice: 0, sortOrder: 0, isActive: true }],
      })
    }
    setLibError('')
    setLibOpen(true)
  }

  async function saveLibrary() {
    if (!libForm.name.trim() || !libForm.options?.length || libForm.options.some((o) => !o.name.trim())) {
      setLibError('Group name and every option name are required.')
      return
    }
    if (libForm.minSelect < 0 || libForm.maxSelect < 1 || libForm.minSelect > libForm.maxSelect) {
      setLibError('Min/max select is invalid.')
      return
    }
    setBusy(true)
    setLibError('')
    try {
      if (editingLib?.id) {
        await api.updateOperatorMerchantAddonGroup(merchantId, editingLib.id, libForm)
      } else {
        await api.createOperatorMerchantAddonGroup(merchantId, libForm)
      }
      const g = await api.operatorMerchantAddonGroups(merchantId)
      setLibrary(g.items)
      setLibOpen(false)
      setNotice('Add-on group saved to library.')
    } catch (err) {
      setLibError(err instanceof Error ? err.message : 'Could not save add-on group.')
    } finally {
      setBusy(false)
    }
  }

  async function removeLibrary(group: ProductAddonGroupItem) {
    if (!group.id || !confirm(`Remove "${group.name}" from the library? Products using it will lose this group.`)) return
    try {
      await api.deleteOperatorMerchantAddonGroup(merchantId, group.id)
      setLibrary(library.filter((x) => x.id !== group.id))
      setNotice('Add-on group removed.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete add-on group.')
    }
  }

  function openProduct(product?: MerchantProductItem) {
    setProductImageFile(null)
    if (product) {
      setEditingProduct(product)
      setProductForm({
        name: product.name,
        description: product.description,
        basePrice: String(product.basePrice ?? 0),
        sellingPrice: String(product.sellingPrice),
        categoryId: product.categoryId ?? '',
        availableOnStorefront: product.availableOnStorefront,
        availableAllDay: product.availableAllDay,
        availableFromTime: product.availableFromTime ?? '08:00',
        availableToTime: product.availableToTime ?? '20:00',
        sortOrder: product.sortOrder,
      })
      setAdoptedGroupIds(product.adoptedAddonGroupIds ?? [])
    } else {
      setEditingProduct(null)
      setProductForm({
        name: '',
        description: '',
        basePrice: '0',
        sellingPrice: '0',
        categoryId: '',
        availableOnStorefront: true,
        availableAllDay: true,
        availableFromTime: '08:00',
        availableToTime: '20:00',
        sortOrder: products.length,
      })
      setAdoptedGroupIds([])
    }
    setProductError('')
    setProductOpen(true)
  }

  async function saveProduct() {
    const basePrice = Number(productForm.basePrice)
    const sellingPrice = Number(productForm.sellingPrice)
    if (!productForm.name.trim() || !Number.isFinite(basePrice) || basePrice < 0 || !Number.isFinite(sellingPrice) || sellingPrice < 0) {
      setProductError('Name and valid base/selling prices are required.')
      return
    }
    if (!productForm.availableAllDay) {
      if (!productForm.availableFromTime || !productForm.availableToTime) {
        setProductError('Set available from/to hours, or mark all day.')
        return
      }
      if (productForm.availableFromTime >= productForm.availableToTime) {
        setProductError('Available-from must be before available-to.')
        return
      }
    }
    setBusy(true)
    setProductError('')
    try {
      const body: SaveMerchantProductBody = {
        name: productForm.name.trim(),
        description: productForm.description.trim(),
        basePrice,
        sellingPrice,
        categoryId: productForm.categoryId || null,
        availableOnStorefront: productForm.availableOnStorefront,
        availableAllDay: productForm.availableAllDay,
        availableFromTime: productForm.availableAllDay ? null : productForm.availableFromTime,
        availableToTime: productForm.availableAllDay ? null : productForm.availableToTime,
        sortOrder: productForm.sortOrder,
      }
      let row = editingProduct
        ? await api.updateOperatorMerchantProduct(merchantId, editingProduct.id, body)
        : await api.createOperatorMerchantProduct(merchantId, body)
      const addonIds = adoptedGroupIds.filter((id) => /^[0-9a-f-]{36}$/i.test(id))
      try {
        row = await api.saveOperatorMerchantProductAddons(merchantId, row.id, addonIds)
      } catch (addonErr) {
        setProductError(
          addonErr instanceof Error
            ? `Product saved, but add-ons failed: ${addonErr.message}`
            : 'Product saved, but add-ons failed.',
        )
        const list = await api.operatorMerchantProducts(merchantId)
        setProducts(list.items)
        setEditingProduct(row)
        return
      }
      if (productImageFile) {
        const compressed = await compressImageFile(productImageFile)
        row = await api.uploadOperatorMerchantProductImage(merchantId, row.id, compressed)
      }
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

  function hoursLabel(p: MerchantProductItem) {
    if (p.availableAllDay) return 'All day'
    return `${p.availableFromTime ?? '—'} – ${p.availableToTime ?? '—'}`
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
            <input type="file" accept="image/*" disabled={busy} onChange={(e) => void uploadMerchantImage('logo', e.target.files?.[0] ?? null)} />
          </label>
          <label className="field">
            <span>Store background</span>
            {merchant.backgroundUrl ? (
              <img src={merchant.backgroundUrl} alt="" style={{ maxHeight: 64, display: 'block', marginBottom: 8, maxWidth: '100%' }} />
            ) : null}
            <input type="file" accept="image/*" disabled={busy} onChange={(e) => void uploadMerchantImage('background', e.target.files?.[0] ?? null)} />
          </label>
        </div>
      </div>

      <div className="card">
        <div className="toolbar">
          <div>
            <h3 style={{ margin: 0 }}>Add-on library</h3>
            <p className="muted" style={{ margin: '6px 0 0' }}>Build groups once, then adopt them on each product.</p>
          </div>
          <button className="btn" type="button" style={{ width: 'auto' }} onClick={() => openLibrary()}>
            Add group
          </button>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Group</th>
                <th>Select</th>
                <th>Options</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {library.length === 0 ? (
                <tr><td colSpan={4}>No library groups yet. Add “Extras”, “Size”, etc.</td></tr>
              ) : library.map((g) => (
                <tr key={g.id}>
                  <td><strong>{g.name}</strong></td>
                  <td>{g.minSelect}–{g.maxSelect}</td>
                  <td>{(g.options ?? []).map((o) => `${o.name} (₱${Number(o.sellingPrice).toFixed(2)})`).join(', ') || '—'}</td>
                  <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                    <button className="btn tiny" type="button" onClick={() => openLibrary(g)}>Edit</button>{' '}
                    <button className="btn tiny danger" type="button" onClick={() => void removeLibrary(g)}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="card">
        <div className="toolbar">
          <div>
            <h3 style={{ margin: 0 }}>Products</h3>
            <p className="muted" style={{ margin: '6px 0 0' }}>Search by name or category.</p>
          </div>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
            <div style={{ minWidth: 260, flex: '1 1 260px' }}>
              <ProductSuggest
                query={productQuery}
                onQuery={setProductQuery}
                placeholder="Search products…"
                items={products.map((p) => ({
                  id: p.id,
                  name: p.name,
                  photoUrl: p.imageUrl,
                  extra: `${p.categoryName || 'No category'} · ₱${Number(p.sellingPrice).toFixed(2)}`,
                }))}
                onPick={(item) => {
                  const product = products.find((p) => p.id === item.id)
                  if (!product) return
                  setProductQuery(item.name)
                  openProduct(product)
                }}
              />
            </div>
            <button className="btn" type="button" style={{ width: 'auto' }} onClick={() => openProduct()}>
              Add product
            </button>
          </div>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Product</th>
                <th>Base</th>
                <th>Selling</th>
                <th>Hours</th>
                <th>Storefront</th>
                <th>Add-ons</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {products.length === 0 ? (
                <tr><td colSpan={7}>No products yet.</td></tr>
              ) : products
                .filter((p) => {
                  const q = productQuery.trim().toLowerCase()
                  if (!q) return true
                  return (
                    p.name.toLowerCase().includes(q) ||
                    (p.categoryName ?? '').toLowerCase().includes(q) ||
                    String(p.sellingPrice).includes(q)
                  )
                })
                .map((p) => (
                <tr key={p.id}>
                  <td>
                    <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                      {p.imageUrl ? (
                        <img src={p.imageUrl} alt="" style={{ width: 40, height: 40, objectFit: 'cover', borderRadius: 8 }} />
                      ) : (
                        <div style={{ width: 40, height: 40, borderRadius: 8, background: 'var(--chip)' }} />
                      )}
                      <div>
                        <strong>{p.name}</strong>
                        <div className="muted" style={{ fontSize: 12 }}>{p.categoryName || 'No category'}</div>
                      </div>
                    </div>
                  </td>
                  <td>₱{Number(p.basePrice ?? 0).toFixed(2)}</td>
                  <td>₱{Number(p.sellingPrice).toFixed(2)}</td>
                  <td>{hoursLabel(p)}</td>
                  <td>
                    <button className="btn tiny" type="button" onClick={() => void toggleStorefront(p)}>
                      {p.availableOnStorefront ? 'On' : 'Off'}
                    </button>
                  </td>
                  <td>{p.adoptedAddonGroupIds?.length ?? 0} groups</td>
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

      {libOpen ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setLibOpen(false)}>
          <div className="modal-panel merchant-modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <div className="modal-head">
              <h2>{editingLib ? 'Edit add-on group' : 'New add-on group'}</h2>
              <button className="btn tiny" type="button" onClick={() => setLibOpen(false)}>Close</button>
            </div>
            <div className="merchant-modal-body">
              <section className="form-section">
                <div className="form-grid">
                  <label className="field">
                    <span>Group name</span>
                    <input value={libForm.name} onChange={(e) => setLibForm({ ...libForm, name: e.target.value })} placeholder="Extras" />
                  </label>
                  <label className="field">
                    <span>Min select</span>
                    <input type="number" min={0} value={libForm.minSelect} onChange={(e) => setLibForm({ ...libForm, minSelect: Number(e.target.value) || 0 })} />
                  </label>
                  <label className="field">
                    <span>Max select</span>
                    <input type="number" min={1} value={libForm.maxSelect} onChange={(e) => setLibForm({ ...libForm, maxSelect: Number(e.target.value) || 1 })} />
                  </label>
                </div>
                <div style={{ marginTop: 14 }}>
                  <div
                    style={{
                      display: 'grid',
                      gridTemplateColumns: 'minmax(140px, 1fr) 100px 100px 36px',
                      gap: 8,
                      alignItems: 'end',
                      marginBottom: 6,
                    }}
                  >
                    <span className="form-hint" style={{ margin: 0, fontWeight: 600 }}>Name</span>
                    <span className="form-hint" style={{ margin: 0, fontWeight: 600 }}>Base</span>
                    <span className="form-hint" style={{ margin: 0, fontWeight: 600 }}>Selling</span>
                    <span />
                  </div>
                  {(libForm.options ?? []).map((opt, oi) => (
                    <div
                      key={oi}
                      style={{
                        display: 'grid',
                        gridTemplateColumns: 'minmax(140px, 1fr) 100px 100px 36px',
                        gap: 8,
                        alignItems: 'center',
                        marginTop: oi === 0 ? 0 : 8,
                      }}
                    >
                      <input
                        aria-label="Name"
                        placeholder="Option name"
                        value={opt.name}
                        onChange={(e) => {
                          const options = [...(libForm.options ?? [])]
                          options[oi] = { ...opt, name: e.target.value }
                          setLibForm({ ...libForm, options })
                        }}
                      />
                      <input
                        type="number"
                        min={0}
                        step="0.01"
                        aria-label="Base"
                        placeholder="0.00"
                        value={opt.basePrice}
                        onChange={(e) => {
                          const options = [...(libForm.options ?? [])]
                          options[oi] = { ...opt, basePrice: Number(e.target.value) || 0 }
                          setLibForm({ ...libForm, options })
                        }}
                      />
                      <input
                        type="number"
                        min={0}
                        step="0.01"
                        aria-label="Selling"
                        placeholder="0.00"
                        value={opt.sellingPrice}
                        onChange={(e) => {
                          const options = [...(libForm.options ?? [])]
                          options[oi] = { ...opt, sellingPrice: Number(e.target.value) || 0 }
                          setLibForm({ ...libForm, options })
                        }}
                      />
                      <button
                        className="btn tiny danger"
                        type="button"
                        aria-label="Remove option"
                        onClick={() => setLibForm({ ...libForm, options: (libForm.options ?? []).filter((_, i) => i !== oi) })}
                      >
                        ×
                      </button>
                    </div>
                  ))}
                </div>
                <button
                  className="btn tiny"
                  type="button"
                  style={{ marginTop: 10 }}
                  onClick={() =>
                    setLibForm({
                      ...libForm,
                      options: [
                        ...(libForm.options ?? []),
                        { name: '', basePrice: 0, sellingPrice: 0, sortOrder: (libForm.options ?? []).length, isActive: true },
                      ],
                    })
                  }
                >
                  Add option
                </button>
              </section>
            </div>
            {libError ? <p className="error" style={{ padding: '0 22px' }}>{libError}</p> : null}
            <div className="merchant-modal-footer">
              <button className="btn tiny" type="button" onClick={() => setLibOpen(false)}>Cancel</button>
              <button className="btn" type="button" disabled={busy} onClick={() => void saveLibrary()}>
                {busy ? 'Saving…' : 'Save group'}
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {productOpen ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setProductOpen(false)}>
          <div className="modal-panel merchant-modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <div className="modal-head">
              <h2>{editingProduct ? 'Edit product' : 'Add product'}</h2>
              <button className="btn tiny" type="button" onClick={() => setProductOpen(false)}>Close</button>
            </div>
            <div className="merchant-modal-body">
              <section className="form-section">
                <h3>Details</h3>
                <div className="form-grid">
                  <label className="field">
                    <span>Name</span>
                    <input value={productForm.name} onChange={(e) => setProductForm({ ...productForm, name: e.target.value })} />
                  </label>
                  <label className="field">
                    <span>Base price (₱)</span>
                    <input
                      type="number"
                      min={0}
                      step="0.01"
                      value={productForm.basePrice}
                      onChange={(e) => setProductForm({ ...productForm, basePrice: e.target.value })}
                    />
                  </label>
                  <label className="field">
                    <span>Selling price (₱)</span>
                    <input
                      type="number"
                      min={0}
                      step="0.01"
                      value={productForm.sellingPrice}
                      onChange={(e) => setProductForm({ ...productForm, sellingPrice: e.target.value })}
                    />
                  </label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}>
                    <span>Description</span>
                    <textarea rows={2} value={productForm.description} onChange={(e) => setProductForm({ ...productForm, description: e.target.value })} />
                  </label>
                  <label className="field">
                    <span>Category</span>
                    <select value={productForm.categoryId} onChange={(e) => setProductForm({ ...productForm, categoryId: e.target.value })}>
                      <option value="">None</option>
                      {categories.filter((c) => c.isActive || c.id === productForm.categoryId).map((c) => (
                        <option key={c.id} value={c.id}>{c.name}</option>
                      ))}
                    </select>
                    <span className="form-hint">Manage categories under Money → Product categories.</span>
                  </label>
                  <label className="field check-field">
                    <span className="check">
                      <input
                        type="checkbox"
                        checked={productForm.availableOnStorefront}
                        onChange={(e) => setProductForm({ ...productForm, availableOnStorefront: e.target.checked })}
                      />
                      <span>Available on storefront</span>
                    </span>
                  </label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}>
                    <span>Product image</span>
                    {editingProduct?.imageUrl ? (
                      <img src={editingProduct.imageUrl} alt="" style={{ maxHeight: 72, display: 'block', marginBottom: 8, borderRadius: 8 }} />
                    ) : null}
                    <input type="file" accept="image/*" onChange={(e) => setProductImageFile(e.target.files?.[0] ?? null)} />
                  </label>
                </div>
              </section>

              <section className="form-section">
                <h3>Available hours</h3>
                <p className="form-hint">Within the store’s open days. All day means no product-level time limit.</p>
                <label className="check" style={{ marginBottom: 12 }}>
                  <input
                    type="checkbox"
                    checked={productForm.availableAllDay}
                    onChange={(e) => setProductForm({ ...productForm, availableAllDay: e.target.checked })}
                  />
                  <span>Available all day</span>
                </label>
                {!productForm.availableAllDay ? (
                  <div className="form-grid">
                    <label className="field">
                      <span>From</span>
                      <input
                        type="time"
                        value={productForm.availableFromTime}
                        onChange={(e) => setProductForm({ ...productForm, availableFromTime: e.target.value })}
                      />
                    </label>
                    <label className="field">
                      <span>To</span>
                      <input
                        type="time"
                        value={productForm.availableToTime}
                        onChange={(e) => setProductForm({ ...productForm, availableToTime: e.target.value })}
                      />
                    </label>
                  </div>
                ) : null}
              </section>

              <section className="form-section">
                <h3>Adopt add-on groups</h3>
                <p className="form-hint">Pick from the merchant library. Build groups above first.</p>
                {library.length === 0 ? (
                  <p className="muted" style={{ margin: 0 }}>No library groups yet.</p>
                ) : (
                  <div className="merchant-hours">
                    {library.map((g) => {
                      const id = g.id ?? ''
                      const checked = adoptedGroupIds.includes(id)
                      return (
                        <label key={id} className="check" style={{ padding: '10px 12px', border: '1px solid var(--line)', borderRadius: 12 }}>
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={(e) => {
                              setAdoptedGroupIds(
                                e.target.checked
                                  ? [...adoptedGroupIds, id]
                                  : adoptedGroupIds.filter((x) => x !== id),
                              )
                            }}
                          />
                          <span>
                            <strong>{g.name}</strong>
                            <span className="form-hint" style={{ display: 'block', margin: '2px 0 0', fontWeight: 500 }}>
                              {(g.options ?? []).map((o) => o.name).join(', ') || 'No options'}
                            </span>
                          </span>
                        </label>
                      )
                    })}
                  </div>
                )}
              </section>
            </div>
            {productError ? <p className="error" style={{ padding: '0 22px' }}>{productError}</p> : null}
            <div className="merchant-modal-footer">
              <button className="btn tiny" type="button" onClick={() => setProductOpen(false)}>Cancel</button>
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

