import { useEffect, useState } from 'react'
import { api, RiderListItem, VehicleType } from './api'

function VehicleLabel({ type }: { type: VehicleType }) {
  return <span className="tag kind">{type}</span>
}

function StatusLabel({ active }: { active: boolean }) {
  return <span className={`tag status ${active ? 'active' : 'inactive'}`}>{active ? 'Active' : 'Inactive'}</span>
}

export function OperatorPabiliRidersPage({ onOpenRider }: { onOpenRider?: (id: string) => void }) {
  const [q, setQ] = useState('')
  const [items, setItems] = useState<RiderListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
  const [linkOpen, setLinkOpen] = useState(false)
  const [candidateQ, setCandidateQ] = useState('')
  const [candidates, setCandidates] = useState<RiderListItem[]>([])
  const [candidateBusy, setCandidateBusy] = useState(false)
  const pageSize = 10

  function reload() {
    api.operatorPabiliRiders(q, page, pageSize)
      .then((data) => {
        setItems(data.items)
        setTotal(data.total)
        setError('')
      })
      .catch((err: Error) => setError(err.message))
  }

  useEffect(() => {
    const handle = window.setTimeout(reload, 200)
    return () => window.clearTimeout(handle)
  }, [q, page])

  useEffect(() => {
    if (!linkOpen) return
    setCandidateBusy(true)
    const handle = window.setTimeout(() => {
      api.operatorPabiliRiderCandidates(candidateQ, 1, 12)
        .then((data) => setCandidates(data.items))
        .catch(() => setCandidates([]))
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
      reload()
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
      reload()
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
          <input
            value={q}
            onChange={(e) => { setQ(e.target.value); setPage(1) }}
            placeholder="Search linked riders"
            style={{ minWidth: 200 }}
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
                  <strong>{row.fullName}</strong>
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
            <label className="field wide">
              <span>Search</span>
              <input
                value={candidateQ}
                onChange={(e) => setCandidateQ(e.target.value)}
                placeholder="Name, phone, or plate"
                autoFocus
              />
            </label>
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
                        <strong>{row.fullName}</strong>
                        <div className="muted" style={{ fontSize: 12 }}>{row.phoneNumber}</div>
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
