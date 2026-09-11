import { FormEvent, useEffect, useState } from 'react'
import { api, PabiliMatrixDetail } from './api'

function roundPercent(value: number) {
  return Math.round(value * 100) / 100
}

function remainderPercent(system: number, other: string) {
  const n = Number(other)
  if (!Number.isFinite(n)) {
    return ''
  }
  return String(roundPercent(Math.max(0, 100 - system - n)))
}

function commissionTotal(system: number, operator: string, rider: string) {
  return roundPercent(system + Number(operator || 0) + Number(rider || 0))
}

function percent(value: number) {
  return `${roundPercent(value)}%`
}

type Draft = {
  baseFareAmount: string
  kmScope: string
  succeedingKm: string
  fareOperatorCommissionPercent: string
  fareRiderCommissionPercent: string
  markupOperatorCommissionPercent: string
  markupRiderCommissionPercent: string
  isActive: boolean
}

function fromDetail(data: PabiliMatrixDetail): Draft {
  return {
    baseFareAmount: String(data.baseFareAmount ?? 0),
    kmScope: String(data.kmScope ?? 1),
    succeedingKm: String(data.succeedingKm ?? 0),
    fareOperatorCommissionPercent: String(data.fareOperatorCommissionPercent ?? 0),
    fareRiderCommissionPercent: String(data.fareRiderCommissionPercent ?? 0),
    markupOperatorCommissionPercent: String(data.markupOperatorCommissionPercent ?? 0),
    markupRiderCommissionPercent: String(data.markupRiderCommissionPercent ?? 0),
    isActive: data.isActive,
  }
}

export function OperatorPabiliMatrixPage() {
  const [data, setData] = useState<PabiliMatrixDetail | null>(null)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let cancelled = false
    api.operatorPabiliMatrix()
      .then((next) => {
        if (cancelled) return
        setData(next)
        setDraft(fromDetail(next))
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message)
      })
    return () => {
      cancelled = true
    }
  }, [])

  function patch(update: Partial<Draft>, side?: 'fare' | 'markup', field?: 'operator' | 'rider') {
    setDraft((current) => {
      if (!current || !data) return current
      const next = { ...current, ...update }
      if (side === 'fare' && field === 'operator') {
        next.fareRiderCommissionPercent = remainderPercent(
          data.fareSystemCommissionPercent,
          next.fareOperatorCommissionPercent,
        )
      }
      if (side === 'fare' && field === 'rider') {
        next.fareOperatorCommissionPercent = remainderPercent(
          data.fareSystemCommissionPercent,
          next.fareRiderCommissionPercent,
        )
      }
      if (side === 'markup' && field === 'operator') {
        next.markupRiderCommissionPercent = remainderPercent(
          data.markupSystemCommissionPercent,
          next.markupOperatorCommissionPercent,
        )
      }
      if (side === 'markup' && field === 'rider') {
        next.markupOperatorCommissionPercent = remainderPercent(
          data.markupSystemCommissionPercent,
          next.markupRiderCommissionPercent,
        )
      }
      return next
    })
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!draft || !data) return
    const baseFareAmount = Number(draft.baseFareAmount)
    const kmScope = Number(draft.kmScope)
    const succeedingKm = Number(draft.succeedingKm)
    const fareOp = Number(draft.fareOperatorCommissionPercent)
    const fareRider = Number(draft.fareRiderCommissionPercent)
    const markupOp = Number(draft.markupOperatorCommissionPercent)
    const markupRider = Number(draft.markupRiderCommissionPercent)
    if (![baseFareAmount, kmScope, succeedingKm, fareOp, fareRider, markupOp, markupRider].every(Number.isFinite)) {
      setError('Enter valid numbers for fare and commission fields.')
      return
    }
    if (baseFareAmount < 0 || kmScope < 0 || succeedingKm < 0) {
      setError('Base fare, KM scope, and succeeding KM cannot be negative.')
      return
    }
    const fareTotal = commissionTotal(data.fareSystemCommissionPercent, draft.fareOperatorCommissionPercent, draft.fareRiderCommissionPercent)
    const markupTotal = commissionTotal(data.markupSystemCommissionPercent, draft.markupOperatorCommissionPercent, draft.markupRiderCommissionPercent)
    if (fareTotal !== 100 || markupTotal !== 100) {
      setError('Fare and markup commissions must each total 100%.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const next = await api.saveOperatorPabiliMatrix({
        baseFareAmount,
        kmScope,
        succeedingKm,
        fareOperatorCommissionPercent: fareOp,
        fareRiderCommissionPercent: fareRider,
        markupOperatorCommissionPercent: markupOp,
        markupRiderCommissionPercent: markupRider,
        isActive: draft.isActive,
      })
      setData(next)
      setDraft(fromDetail(next))
      setNotice('Pabili matrix saved.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save Pabili matrix.')
    } finally {
      setBusy(false)
    }
  }

  if (!data || !draft) {
    return (
      <div className="card">
        <h2>Pabili Matrix</h2>
        {error ? <p className="error">{error}</p> : <p className="muted">Loading…</p>}
      </div>
    )
  }

  const fareTotal = commissionTotal(
    data.fareSystemCommissionPercent,
    draft.fareOperatorCommissionPercent,
    draft.fareRiderCommissionPercent,
  )
  const markupTotal = commissionTotal(
    data.markupSystemCommissionPercent,
    draft.markupOperatorCommissionPercent,
    draft.markupRiderCommissionPercent,
  )

  return (
    <form className="card" onSubmit={submit}>
      <div className="panel-head">
        <div>
          <h2>Pabili Matrix</h2>
          <p className="muted">Delivery fare and commission split for Pabili. System shares are set by admin.</p>
        </div>
        <div className="chips">
          <button type="button" className={draft.isActive ? 'on' : ''} disabled={busy} onClick={() => patch({ isActive: true })}>
            Active
          </button>
          <button type="button" className={!draft.isActive ? 'on' : ''} disabled={busy} onClick={() => patch({ isActive: false })}>
            Inactive
          </button>
        </div>
      </div>

      <div className="form-sections">
        <section className="form-section">
          <h3>Base fare</h3>
          <div className="form-grid">
            <label className="field">
              <span>Base fare amount</span>
              <input
                type="number"
                min={0}
                step="0.01"
                value={draft.baseFareAmount}
                disabled={busy}
                onChange={(e) => patch({ baseFareAmount: e.target.value })}
              />
            </label>
            <label className="field">
              <span>KM scope</span>
              <input
                type="number"
                min={0}
                step="0.01"
                value={draft.kmScope}
                disabled={busy}
                onChange={(e) => patch({ kmScope: e.target.value })}
              />
            </label>
            <label className="field">
              <span>Succeeding KM</span>
              <input
                type="number"
                min={0}
                step="0.01"
                value={draft.succeedingKm}
                disabled={busy}
                onChange={(e) => patch({ succeedingKm: e.target.value })}
              />
            </label>
          </div>
        </section>

        <section className="form-section">
          <h3>Fare commission</h3>
          <div className="fare-commission-grid">
            <div>
              <small>System Comm</small>
              <strong className="fare-matrix-system-value">{percent(data.fareSystemCommissionPercent)}</strong>
            </div>
            <div>
              <small>Operator Comm</small>
              <input
                value={draft.fareOperatorCommissionPercent}
                disabled={busy}
                onChange={(e) => patch({ fareOperatorCommissionPercent: e.target.value }, 'fare', 'operator')}
              />
            </div>
            <div>
              <small>Rider Comm</small>
              <input
                value={draft.fareRiderCommissionPercent}
                disabled={busy}
                onChange={(e) => patch({ fareRiderCommissionPercent: e.target.value }, 'fare', 'rider')}
              />
            </div>
            <div>
              <small>Total</small>
              <strong className={fareTotal === 100 ? '' : 'error'}>{percent(fareTotal)}</strong>
            </div>
          </div>
          <p className="muted" style={{ margin: '8px 0 0', fontSize: 12 }}>
            System is set by admin. The three shares must add up to 100%.
          </p>
        </section>

        <section className="form-section">
          <h3>Markup commission</h3>
          <div className="fare-commission-grid">
            <div>
              <small>System Comm</small>
              <strong className="fare-matrix-system-value">{percent(data.markupSystemCommissionPercent)}</strong>
            </div>
            <div>
              <small>Operator Comm</small>
              <input
                value={draft.markupOperatorCommissionPercent}
                disabled={busy}
                onChange={(e) => patch({ markupOperatorCommissionPercent: e.target.value }, 'markup', 'operator')}
              />
            </div>
            <div>
              <small>Rider Comm</small>
              <input
                value={draft.markupRiderCommissionPercent}
                disabled={busy}
                onChange={(e) => patch({ markupRiderCommissionPercent: e.target.value }, 'markup', 'rider')}
              />
            </div>
            <div>
              <small>Total</small>
              <strong className={markupTotal === 100 ? '' : 'error'}>{percent(markupTotal)}</strong>
            </div>
          </div>
          <p className="muted" style={{ margin: '8px 0 0', fontSize: 12 }}>
            System is set by admin. The three shares must add up to 100%.
          </p>
        </section>
      </div>

      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <div style={{ display: 'flex', gap: 10, maxWidth: 280, marginTop: 12 }}>
        <button className="btn" type="submit" disabled={busy || fareTotal !== 100 || markupTotal !== 100}>
          {busy ? 'Saving…' : 'Save matrix'}
        </button>
      </div>
    </form>
  )
}
