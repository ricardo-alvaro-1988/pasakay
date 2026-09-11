import { useEffect, useState } from 'react'
import { api, RiderListItem, VehicleType } from './api'

function VehicleLabel({ type }: { type: VehicleType }) {
  return <span className="tag kind">{type}</span>
}

function StatusLabel({ active }: { active: boolean }) {
  return <span className={`tag status ${active ? 'active' : 'inactive'}`}>{active ? 'Active' : 'Inactive'}</span>
}

function RiderAvatar({ name, photoUrl }: { name: string; photoUrl: string | null }) {
  if (photoUrl) {
    return <img className="avatar" src={photoUrl} alt="" width={36} height={36} style={{ objectFit: 'cover', borderRadius: '50%' }} />
  }
  return <div className="avatar">{name.trim().slice(0, 1).toUpperCase() || '?'}</div>
}

type SuggestRow = {
  id: string
  name: string
  phone: string
  photoUrl: string | null
  extra?: string
  vehicleType?: VehicleType
}

function RiderSuggest({
  value,
  onChange,
  items,
  placeholder,
  onPick,
  emptyLabel = 'No matches',
}: {
  value: string
  onChange: (value: string) => void
  items: SuggestRow[]
  placeholder: string
  onPick: (item: SuggestRow) => void
  emptyLabel?: string
}) {
  const [open, setOpen] = useState(false)
  const q = value.trim().toLowerCase()
  const filtered = items
    .filter((item) => {
      if (!q) return true
      return (
        item.name.toLowerCase().includes(q) ||
        item.phone.includes(q) ||
        (item.extra ?? '').toLowerCase().includes(q)
      )
    })
    .sort((a, b) => {
      if (!q) return a.name.localeCompare(b.name)
      const aStarts = a.name.toLowerCase().startsWith(q)
      const bStarts = b.name.toLowerCase().startsWith(q)
      if (aStarts !== bStarts) return aStarts ? -1 : 1
      return a.name.localeCompare(b.name)
    })

  return (
    <div className="ac" style={{ minWidth: 260 }}>
      <input
        value={value}
        placeholder={placeholder}
        autoComplete="off"
        onChange={(e) => {
          onChange(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => window.setTimeout(() => setOpen(false), 160)}
      />
      {open ? (
        <div className="suggest">
          {filtered.length === 0 ? (
            <div className="suggest-empty">{emptyLabel}</div>
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
                <RiderAvatar name={item.name} photoUrl={item.photoUrl} />
                <span className="ac-text">
                  <span className="suggest-name">{item.name}</span>
                  <small>{item.extra ? `${item.phone} · ${item.extra}` : item.phone}</small>
                </span>
                {item.vehicleType ? <VehicleLabel type={item.vehicleType} /> : null}
              </button>
            ))
          )}
        </div>
      ) : null}
    </div>
  )
}

function toSuggest(row: RiderListItem): SuggestRow {
  return {
    id: row.id,
    name: row.fullName,
    phone: row.phoneNumber,
    photoUrl: row.profilePhotoUrl,
    extra: row.plateNumber,
    vehicleType: row.vehicleType,
  }
}

export function OperatorPabiliRidersPage({ onOpenRider }: { onOpenRider?: (id: string) => void }) {
  const [q, setQ] = useState('')
  const [items, setItems] = useState<RiderListItem[]>([])
  const [suggest, setSuggest] = useState<RiderListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
  const [linkOpen, setLinkOpen] = useState(false)
  const [candidateQ, setCandidateQ] = useState('')
  const [candidates, setCandidates] = useState<RiderListItem[]>([])
  const [candidateSuggest, setCandidateSuggest] = useState<RiderListItem[]>([])
  const [candidateBusy, setCandidateBusy] = useState(false)
  const pageSize = 10

  function reloadList() {
    api.operatorPabiliRiders(q, page, pageSize)
      .then((data) => {
        setItems(data.items)
        setTotal(data.total)
        setError('')
      })
      .catch((err: Error) => setError(err.message))
  }

  useEffect(() => {
    const handle = window.setTimeout(() => {
      reloadList()
      api.operatorPabiliRiders(q, 1, 8)
        .then((data) => setSuggest(data.items))
        .catch(() => setSuggest([]))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, page])

  useEffect(() => {
    if (!linkOpen) return
    setCandidateBusy(true)
    const handle = window.setTimeout(() => {
      api.operatorPabiliRiderCandidates(candidateQ, 1, 12)
        .then((data) => {
          setCandidates(data.items)
          setCandidateSuggest(data.items)
        })
        .catch(() => {
          setCandidates([])
          setCandidateSuggest([])
        })
        .finally(() => setCandidateBusy(false))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [linkOpen, candidateQ])

  async function link(id: string, name: string) {
    setBusyId(id)
    setError('')
    try {
      await api.linkOperatorPabiliRider(id)
      setNotice(`${name} linked to Pabili.`)
      setLinkOpen(false)
      setCandidateQ('')
      setQ('')
      setPage(1)
      reloadList()
      api.operatorPabiliRiders('', 1, 8).then((data) => setSuggest(data.items)).catch(() => setSuggest([]))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not link rider.')
    } finally {
      setBusyId(null)
    }
  }

  async function unlink(row: RiderListItem) {
    if (!window.confirm(`Unlink ${row.fullName} from Pabili? They stay available for Pasakay.`)) {
      return
    }
    setBusyId(row.id)
    setError('')
    try {
      await api.unlinkOperatorPabiliRider(row.id)
      setNotice(`${row.fullName} unlinked from Pabili.`)
      reloadList()
      api.operatorPabiliRiders(q, 1, 8).then((data) => setSuggest(data.items)).catch(() => setSuggest([]))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not unlink rider.')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Pabili riders</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 520 }}>
            Same riders as Pasakay. Link an existing rider to include them in the Pabili pool — no duplicate accounts.
          </p>
        </div>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
          <RiderSuggest
            value={q}
            onChange={(value) => { setQ(value); setPage(1) }}
            placeholder="Search name, phone, or plate"
            items={suggest.map(toSuggest)}
            emptyLabel={q.trim() ? 'No linked riders match' : 'No linked riders yet'}
            onPick={(item) => {
              setQ(item.name)
              setPage(1)
              onOpenRider?.(item.id)
            }}
          />
          <button className="btn" type="button" style={{ width: 'auto', whiteSpace: 'nowrap' }} onClick={() => setLinkOpen(true)}>
            Link rider
          </button>
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}

      <div className="table-wrap" style={{ marginTop: 18 }}>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Phone</th>
              <th>Vehicle</th>
              <th>Plate</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={6}>{q.trim() ? 'No linked riders match that search.' : 'No Pabili riders yet. Link an existing rider.'}</td>
              </tr>
            ) : items.map((row) => (
              <tr key={row.id}>
                <td>
                  <div className="person-cell">
                    <RiderAvatar name={row.fullName} photoUrl={row.profilePhotoUrl} />
                    <strong>{row.fullName}</strong>
                  </div>
                </td>
                <td>{row.phoneNumber}</td>
                <td><VehicleLabel type={row.vehicleType} /></td>
                <td>{row.plateNumber}</td>
                <td><StatusLabel active={row.isActive} /></td>
                <td>
                  {onOpenRider ? (
                    <button className="btn tiny" type="button" onClick={() => onOpenRider(row.id)}>
                      Open
                    </button>
                  ) : null}
                  <button
                    className="btn tiny danger"
                    type="button"
                    style={{ marginLeft: 8 }}
                    disabled={busyId === row.id}
                    onClick={() => void unlink(row)}
                  >
                    Unlink
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {total > pageSize ? (
        <div className="pager" style={{ marginTop: 12 }}>
          <button className="btn tiny" type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</button>
          <span className="muted" style={{ margin: '0 10px' }}>Page {page}</span>
          <button
            className="btn tiny"
            type="button"
            disabled={page * pageSize >= total}
            onClick={() => setPage((p) => p + 1)}
          >
            Next
          </button>
        </div>
      ) : null}

      {linkOpen ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setLinkOpen(false)}>
          <div
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby="pabili-link-rider-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <div>
                <h2 id="pabili-link-rider-title">Link rider to Pabili</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  Search riders who are not yet in the Pabili pool.
                </p>
              </div>
              <button className="btn tiny" type="button" onClick={() => setLinkOpen(false)}>Close</button>
            </div>
            <div className="field wide">
              <span>Search</span>
              <RiderSuggest
                value={candidateQ}
                onChange={setCandidateQ}
                placeholder="Search name, phone, or plate"
                items={candidateSuggest.map(toSuggest)}
                emptyLabel={candidateBusy ? 'Searching…' : candidateQ.trim() ? 'No unlinked riders match' : 'Type to find a rider'}
                onPick={(item) => {
                  setCandidateQ(item.name)
                  void link(item.id, item.name)
                }}
              />
            </div>
            <div className="table-wrap" style={{ marginTop: 12 }}>
              <table>
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Vehicle</th>
                    <th>Plate</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {candidateBusy ? (
                    <tr><td colSpan={4}>Searching…</td></tr>
                  ) : candidates.length === 0 ? (
                    <tr>
                      <td colSpan={4}>
                        {candidateQ.trim() ? 'No unlinked riders match.' : 'No unlinked riders left, or none created yet.'}
                      </td>
                    </tr>
                  ) : candidates.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <div className="person-cell">
                          <RiderAvatar name={row.fullName} photoUrl={row.profilePhotoUrl} />
                          <div>
                            <strong>{row.fullName}</strong>
                            <div className="muted" style={{ fontSize: 12 }}>{row.phoneNumber}</div>
                          </div>
                        </div>
                      </td>
                      <td><VehicleLabel type={row.vehicleType} /></td>
                      <td>{row.plateNumber}</td>
                      <td>
                        <button
                          className="btn tiny"
                          type="button"
                          disabled={busyId === row.id}
                          onClick={() => void link(row.id, row.fullName)}
                        >
                          Link
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
