import { FormEvent, useEffect, useState } from 'react'
import { api, RiderNotice } from './api'

const PH_TZ = 'Asia/Manila'

function fromPhInput(value: string) {
  const raw = value.trim()
  if (!raw) return null
  const normalized = raw.length === 16 ? `${raw}:00` : raw
  const stamp = new Date(`${normalized}+08:00`)
  return Number.isNaN(stamp.getTime()) ? null : stamp.toISOString()
}

function nowPhInput() {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: PH_TZ,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date())
  const get = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value ?? ''
  return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`
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

export function OperatorRiderNoticesPage() {
  const [items, setItems] = useState<RiderNotice[]>([])
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [when, setWhen] = useState(nowPhInput)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  async function load() {
    setItems(await api.riderNotices())
  }

  useEffect(() => {
    load().catch((err: Error) => setError(err.message))
  }, [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    const scheduledAtUtc = fromPhInput(when)
    if (!title.trim() || !body.trim()) {
      setError('Title and message are required.')
      return
    }
    if (!scheduledAtUtc) {
      setError('Choose when riders should be notified.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const saved = await api.createRiderNotice({
        title: title.trim(),
        body: body.trim(),
        scheduledAtUtc,
      })
      setTitle('')
      setBody('')
      setWhen(nowPhInput())
      await load()
      setNotice(saved.status === 'Sent'
        ? 'Announcement sent. Riders get a short quiet tone.'
        : `Announcement scheduled for ${phDateTime(saved.scheduledAtUtc)}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not schedule this announcement.')
    } finally {
      setBusy(false)
    }
  }

  async function cancel(item: RiderNotice) {
    setError('')
    setNotice('')
    try {
      await api.cancelRiderNotice(item.id)
      await load()
      setNotice('Scheduled announcement cancelled.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not cancel this announcement.')
    }
  }

  return (
    <div className="form-sections">
      <form className="card" onSubmit={(event) => void submit(event)}>
        <div className="panel-head">
          <div>
            <h2 style={{ margin: 0 }}>Schedule a rider announcement</h2>
            <p className="muted" style={{ margin: '4px 0 0' }}>
              Your riders get a notification with a short quiet tone, not the job alarm. Time is Philippine time.
            </p>
          </div>
        </div>
        <div className="form-grid">
          <label className="field wide">
            <span>Title</span>
            <input maxLength={80} value={title} onChange={(e) => setTitle(e.target.value)} />
          </label>
          <label className="field wide">
            <span>Message</span>
            <textarea maxLength={400} value={body} onChange={(e) => setBody(e.target.value)} />
          </label>
          <label className="field">
            <span>Notify at</span>
            <input type="datetime-local" value={when} onChange={(e) => setWhen(e.target.value)} />
          </label>
        </div>
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}
        <div style={{ display: 'flex', gap: 10, maxWidth: 280 }}>
          <button className="btn" type="submit" disabled={busy}>
            {busy ? 'Saving…' : 'Schedule'}
          </button>
        </div>
      </form>

      <div className="card">
        <div className="toolbar">
          <h2 style={{ margin: 0 }}>Announcements</h2>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Announcement</th>
                <th>When</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan={4}>No announcements yet.</td>
                </tr>
              ) : items.map((item) => (
                <tr key={item.id}>
                  <td>
                    <strong>{item.title}</strong>
                    <div className="muted">{item.body}</div>
                  </td>
                  <td>
                    <div>{phDateTime(item.scheduledAtUtc)}</div>
                    {item.sentAtUtc ? <small className="muted">Sent {phDateTime(item.sentAtUtc)}</small> : null}
                  </td>
                  <td>
                    <span className={`tag ${item.status === 'Sent' ? 'active' : item.status === 'Cancelled' ? 'rejected' : 'pending'}`}>
                      {item.status}
                    </span>
                  </td>
                  <td>
                    {item.status === 'Scheduled' ? (
                      <button className="btn tiny danger" type="button" onClick={() => void cancel(item)}>
                        Cancel
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
