import { FormEvent, useEffect, useState } from 'react'
import { api, RiderNotice } from './api'

const PH_TZ = 'Asia/Manila'

function clockLabel(value: string) {
  const [hour, minute] = value.split(':').map(Number)
  if (!Number.isFinite(hour) || !Number.isFinite(minute)) return value
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

export function OperatorRiderNoticesPage() {
  const [items, setItems] = useState<RiderNotice[]>([])
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [when, setWhen] = useState('15:00')
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
    if (!title.trim() || !body.trim()) {
      setError('Title and message are required.')
      return
    }
    if (!when) {
      setError('Choose the daily time.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const saved = await api.createRiderNotice({
        title: title.trim(),
        body: body.trim(),
        notifyAt: when,
      })
      setTitle('')
      setBody('')
      setWhen('15:00')
      await load()
      setNotice(`Riders will be notified every day at ${clockLabel(saved.notifyAt)}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save this announcement.')
    } finally {
      setBusy(false)
    }
  }

  async function stop(item: RiderNotice) {
    setError('')
    setNotice('')
    try {
      await api.cancelRiderNotice(item.id)
      await load()
      setNotice('Daily announcement stopped.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not stop this announcement.')
    }
  }

  return (
    <div className="form-sections">
      <form className="card" onSubmit={(event) => void submit(event)}>
        <div className="panel-head">
          <div>
            <h2 style={{ margin: 0 }}>Daily rider announcement</h2>
            <p className="muted" style={{ margin: '4px 0 0' }}>
              Riders are notified every day at this time with a short quiet tone. Time is Philippine time. Example: 3:00 PM.
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
            <span>Every day at</span>
            <input type="time" value={when} onChange={(e) => setWhen(e.target.value)} />
          </label>
        </div>
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}
        <div style={{ display: 'flex', gap: 10, maxWidth: 280 }}>
          <button className="btn" type="submit" disabled={busy}>
            {busy ? 'Saving…' : 'Save daily time'}
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
                <th>Every day</th>
                <th>Last sent</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan={5}>No announcements yet.</td>
                </tr>
              ) : items.map((item) => (
                <tr key={item.id}>
                  <td>
                    <strong>{item.title}</strong>
                    <div className="muted">{item.body}</div>
                    {item.isActive ? <small className="muted">Next {phDateTime(item.nextFireUtc)}</small> : null}
                  </td>
                  <td>{clockLabel(item.notifyAt)}</td>
                  <td>{item.lastSentAtUtc ? phDateTime(item.lastSentAtUtc) : '—'}</td>
                  <td>
                    <span className={`tag ${item.status === 'Active' ? 'active' : 'rejected'}`}>
                      {item.status}
                    </span>
                  </td>
                  <td>
                    {item.isActive ? (
                      <button className="btn tiny danger" type="button" onClick={() => void stop(item)}>
                        Stop
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
