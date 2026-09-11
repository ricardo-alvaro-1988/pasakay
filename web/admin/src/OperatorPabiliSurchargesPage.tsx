import { useEffect, useState } from 'react'
import { api, FareSurcharge, PabiliSurchargeList, SurchargeKind } from './api'

const PH_TZ = 'Asia/Manila'

function peso(value: number) {
  return `₱${value.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

function fromPhInput(value: string) {
  const raw = value.trim()
  if (!raw) return null
  const normalized = raw.length === 16 ? `${raw}:00` : raw
  const stamp = new Date(`${normalized}+08:00`)
  return Number.isNaN(stamp.getTime()) ? null : stamp.toISOString()
}

function toPhInput(value: string | null | undefined) {
  if (!value) return ''
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: PH_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(value))
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value ?? ''
  return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`
}

function phClock(time: string | null | undefined) {
  if (!time) return '—'
  const [hour, minute] = time.split(':').map(Number)
  if (!Number.isFinite(hour) || !Number.isFinite(minute)) return time
  const stamp = new Date(Date.UTC(2026, 0, 1, hour, minute))
  return stamp.toLocaleTimeString('en-PH', { timeZone: 'UTC', hour: 'numeric', minute: '2-digit' })
}

function phDateTime(value: string | null | undefined) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('en-PH', {
    timeZone: PH_TZ,
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

function surchargeLine(item: FareSurcharge) {
  if (item.kind === 'TimeWindow') {
    return `${phClock(item.windowStart)} – ${phClock(item.windowEnd)} daily`
  }
  return `${phDateTime(item.rangeStartUtc)} – ${phDateTime(item.rangeEndUtc)}`
}

function kindLabel(kind: SurchargeKind) {
  return kind === 'TimeWindow' ? 'Window' : 'Date range'
}

export function OperatorPabiliSurchargesPage() {
  const [data, setData] = useState<PabiliSurchargeList | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<FareSurcharge | null>(null)
  const [name, setName] = useState('')
  const [amount, setAmount] = useState('')
  const [kind, setKind] = useState<SurchargeKind>('TimeWindow')
  const [windowStart, setWindowStart] = useState('22:00')
  const [windowEnd, setWindowEnd] = useState('05:00')
  const [rangeStart, setRangeStart] = useState('')
  const [rangeEnd, setRangeEnd] = useState('')
  const [surchargeActive, setSurchargeActive] = useState(true)
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let cancelled = false
    api.operatorPabiliSurcharges()
      .then((next) => {
        if (!cancelled) setData(next)
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message)
      })
    return () => {
      cancelled = true
    }
  }, [])

  function openCreate() {
    setEditing(null)
    setName('')
    setAmount('')
    setKind('TimeWindow')
    setWindowStart('22:00')
    setWindowEnd('05:00')
    setRangeStart('')
    setRangeEnd('')
    setSurchargeActive(true)
    setFormError('')
    setOpen(true)
  }

  function openEdit(item: FareSurcharge) {
    setEditing(item)
    setName(item.name)
    setAmount(String(item.amount))
    setKind(item.kind)
    setWindowStart(item.windowStart ?? '22:00')
    setWindowEnd(item.windowEnd ?? '05:00')
    setRangeStart(toPhInput(item.rangeStartUtc))
    setRangeEnd(toPhInput(item.rangeEndUtc))
    setSurchargeActive(item.isActive)
    setFormError('')
    setOpen(true)
  }

  function closeModal() {
    setOpen(false)
    setEditing(null)
    setFormError('')
  }

  function body() {
    return {
      kind,
      name: name.trim(),
      amount: Number(amount),
      windowStart: kind === 'TimeWindow' ? windowStart : null,
      windowEnd: kind === 'TimeWindow' ? windowEnd : null,
      rangeStartUtc: kind === 'DateRange' ? fromPhInput(rangeStart) : null,
      rangeEndUtc: kind === 'DateRange' ? fromPhInput(rangeEnd) : null,
      isActive: surchargeActive,
    }
  }

  async function save() {
    const trimmedName = name.trim()
    if (!trimmedName) {
      setFormError('Enter a surcharge name.')
      return
    }
    if (!amount.trim()) {
      setFormError('Enter an amount.')
      return
    }
    const pesos = Number(amount)
    if (!Number.isFinite(pesos) || pesos < 0) {
      setFormError('Enter a valid amount (0 or more).')
      return
    }
    if (kind === 'TimeWindow' && (!windowStart || !windowEnd)) {
      setFormError('Choose a start and end time.')
      return
    }
    if (kind === 'DateRange' && (!fromPhInput(rangeStart) || !fromPhInput(rangeEnd))) {
      setFormError('Choose a from and until date/time (Philippine time).')
      return
    }

    setBusy(true)
    setFormError('')
    try {
      const payload = { ...body(), name: trimmedName, amount: pesos }
      const next = editing
        ? await api.updateOperatorPabiliSurcharge(editing.id, payload)
        : await api.createOperatorPabiliSurcharge(payload)
      setData(next)
      setNotice(editing ? `${trimmedName} updated.` : `${trimmedName} added.`)
      closeModal()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Could not save surcharge.')
    } finally {
      setBusy(false)
    }
  }

  async function toggle(item: FareSurcharge, isActive: boolean) {
    try {
      const next = await api.updateOperatorPabiliSurcharge(item.id, {
        kind: item.kind,
        name: item.name,
        amount: item.amount,
        windowStart: item.windowStart,
        windowEnd: item.windowEnd,
        rangeStartUtc: item.rangeStartUtc,
        rangeEndUtc: item.rangeEndUtc,
        isActive,
      })
      setData(next)
      setNotice(`${item.name} is now ${isActive ? 'active' : 'off'}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not update surcharge.')
    }
  }

  async function remove(item: FareSurcharge) {
    if (!window.confirm(`Remove ${item.name}?`)) return
    try {
      const next = await api.deleteOperatorPabiliSurcharge(item.id)
      setData(next)
      setNotice(`${item.name} removed.`)
      if (editing?.id === item.id) closeModal()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not remove surcharge.')
    }
  }

  if (!data) {
    return error ? <p className="error">{error}</p> : <p>Loading Pabili surcharges…</p>
  }

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Pabili surcharges</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 520 }}>
            Window surcharges are time-bound (daily Philippine time). Date range covers a start–end period.
            {' '}For {data.companyName}.
          </p>
        </div>
        <button className="btn" type="button" style={{ width: 'auto', whiteSpace: 'nowrap' }} onClick={openCreate}>
          Add surcharge
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}

      <div className="table-wrap" style={{ marginTop: 18 }}>
        <p className="muted">Multiple surcharges allowed. Each row can be Active or Off.</p>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Kind</th>
              <th>Amount</th>
              <th>When</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {data.items.length === 0 ? (
              <tr>
                <td colSpan={6}>No surcharges yet.</td>
              </tr>
            ) : (
              data.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.name}</td>
                  <td>{kindLabel(item.kind)}</td>
                  <td>{peso(item.amount)}</td>
                  <td>{surchargeLine(item)}</td>
                  <td>
                    <div className="chips">
                      <button type="button" className={item.isActive ? 'on' : ''} onClick={() => void toggle(item, true)}>
                        Active
                      </button>
                      <button type="button" className={!item.isActive ? 'on' : ''} onClick={() => void toggle(item, false)}>
                        Off
                      </button>
                    </div>
                  </td>
                  <td>
                    <button className="btn tiny" type="button" onClick={() => openEdit(item)}>Edit</button>
                    <button className="btn tiny danger" type="button" style={{ marginLeft: 8 }} onClick={() => void remove(item)}>
                      Remove
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {open ? (
        <div className="modal-backdrop" role="presentation" onClick={closeModal}>
          <div
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby="pabili-surcharge-modal-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <div>
                <h2 id="pabili-surcharge-modal-title">{editing ? 'Edit surcharge' : 'Add surcharge'}</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  Window = daily time bound. Date range = from–until. Times use Philippine time.
                </p>
              </div>
              <button className="btn tiny" type="button" onClick={closeModal}>Close</button>
            </div>
            <div className="form-grid">
              <label className="field">
                <span>Name</span>
                <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Night" />
              </label>
              <label className="field">
                <span>Amount</span>
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  inputMode="decimal"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  placeholder="50"
                />
              </label>
              <div className="field wide">
                <span>Kind</span>
                <div className="chips" style={{ marginTop: 8 }}>
                  <button type="button" className={kind === 'TimeWindow' ? 'on' : ''} onClick={() => setKind('TimeWindow')}>
                    Window
                  </button>
                  <button type="button" className={kind === 'DateRange' ? 'on' : ''} onClick={() => setKind('DateRange')}>
                    Date range
                  </button>
                </div>
              </div>
              {kind === 'TimeWindow' ? (
                <>
                  <label className="field">
                    <span>Start</span>
                    <input type="time" value={windowStart} onChange={(e) => setWindowStart(e.target.value)} />
                  </label>
                  <label className="field">
                    <span>End</span>
                    <input type="time" value={windowEnd} onChange={(e) => setWindowEnd(e.target.value)} />
                  </label>
                </>
              ) : (
                <>
                  <label className="field">
                    <span>From</span>
                    <input type="datetime-local" value={rangeStart} onChange={(e) => setRangeStart(e.target.value)} />
                  </label>
                  <label className="field">
                    <span>Until</span>
                    <input type="datetime-local" value={rangeEnd} onChange={(e) => setRangeEnd(e.target.value)} />
                  </label>
                </>
              )}
              <div className="field">
                <span>Status</span>
                <div className="chips" style={{ marginTop: 8 }}>
                  <button type="button" className={surchargeActive ? 'on' : ''} onClick={() => setSurchargeActive(true)}>
                    Active
                  </button>
                  <button type="button" className={!surchargeActive ? 'on' : ''} onClick={() => setSurchargeActive(false)}>
                    Off
                  </button>
                </div>
              </div>
            </div>
            {formError ? <p className="error">{formError}</p> : null}
            <div className="modal-actions">
              <button className="btn" type="button" disabled={busy} onClick={() => void save()}>
                {busy ? 'Saving…' : editing ? 'Save surcharge' : 'Add surcharge'}
              </button>
              <button className="btn ghost" type="button" disabled={busy} onClick={closeModal}>Cancel</button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
