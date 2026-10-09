import { FormEvent, useEffect, useRef, useState } from 'react'
import { api } from './api'

type Release = {
  id: string
  version: string
  downloadUrl: string
  releasedAtUtc: string
  notes: string | null
  isLatest: boolean
}

function formatReleasedAt(value: string) {
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value))
  } catch {
    return value
  }
}

function formatBytes(bytes: number) {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function PassengerAppSettingsPage() {
  const [latest, setLatest] = useState<Release | null>(null)
  const [releases, setReleases] = useState<Release[]>([])
  const [version, setVersion] = useState('')
  const [notes, setNotes] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [uploadLabel, setUploadLabel] = useState('')
  const [uploadPercent, setUploadPercent] = useState<number | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  async function load() {
    const data = await api.getPassengerApp()
    setLatest(data.latest)
    setReleases(data.releases)
  }

  useEffect(() => {
    let cancelled = false
    load()
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load passenger app releases.')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  async function publish(e: FormEvent) {
    e.preventDefault()
    if (!file) {
      setError('Choose an APK file.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    setUploadPercent(0)
    setUploadLabel(`Uploading 0 MB of ${formatBytes(file.size)}`)
    try {
      const data = await api.publishPassengerApp(
        { version: version.trim(), notes: notes.trim() || undefined, file },
        (loaded, total) => {
          const percent = total > 0 ? Math.min(100, Math.round((loaded / total) * 100)) : 0
          setUploadPercent(percent)
          setUploadLabel(
            percent >= 100
              ? 'Finishing publish…'
              : `Uploading ${formatBytes(loaded)} of ${formatBytes(total)} (${percent}%)`,
          )
        },
      )
      setLatest(data.latest)
      setReleases(data.releases)
      setVersion('')
      setNotes('')
      setFile(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
      setNotice(`Published passenger app ${data.latest?.version ?? version.trim()}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not publish passenger app.')
    } finally {
      setBusy(false)
      setUploadPercent(null)
      setUploadLabel('')
    }
  }

  if (loading) {
    return (
      <div className="card">
        <p className="muted">Loading passenger app…</p>
      </div>
    )
  }

  return (
    <div className="form-sections">
      <div className="card">
        <h2 style={{ marginTop: 0 }}>Current passenger app</h2>
        <p className="muted">
          Customers download from{' '}
          <a href="/Downloads" target="_blank" rel="noreferrer">
            /Downloads
          </a>
          .
        </p>
        {latest ? (
          <div style={{ marginTop: 12, display: 'grid', gap: 6 }}>
            <p style={{ margin: 0 }}>
              Latest version: <strong>{latest.version}</strong>
              {latest.isLatest ? null : ' (not marked latest)'}
            </p>
            <p className="muted" style={{ margin: 0 }}>
              Published {formatReleasedAt(latest.releasedAtUtc)}
            </p>
            {latest.notes ? <p style={{ margin: 0, whiteSpace: 'pre-wrap' }}>{latest.notes}</p> : null}
            <a href={latest.downloadUrl} download={`pasakay-passenger-${latest.version}.apk`}>
              Download current APK
            </a>
          </div>
        ) : (
          <p style={{ marginTop: 12 }}>No APK published yet.</p>
        )}
      </div>

      <form className="card" onSubmit={(e) => void publish(e)}>
        <h2 style={{ marginTop: 0 }}>Publish new build</h2>
        <p className="muted">Upload an APK and set its version. Re-uploading the same version replaces the existing file. This becomes the download on /Downloads.</p>
        <label className="field">
          <span>Version</span>
          <input
            value={version}
            onChange={(e) => setVersion(e.target.value)}
            placeholder="1.0.0"
            required
            maxLength={40}
            disabled={busy}
          />
        </label>
        <label className="field">
          <span>Release notes (optional)</span>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            maxLength={2000}
            disabled={busy}
            placeholder="What’s new for passengers"
          />
        </label>
        <label className="field">
          <span>APK file</span>
          <input
            ref={fileInputRef}
            type="file"
            accept=".apk,application/vnd.android.package-archive"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            disabled={busy}
            required
          />
        </label>
        {file ? <p className="muted">Selected: {file.name} ({formatBytes(file.size)})</p> : null}
        {busy ? (
          <div className="upload-progress">
            <progress value={uploadPercent ?? 0} max={100} />
            <p className="muted" style={{ margin: 0 }}>{uploadLabel || 'Starting upload…'}</p>
          </div>
        ) : null}
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}
        <button className="btn" type="submit" disabled={busy} style={{ marginTop: 12, maxWidth: 220 }}>
          {busy ? 'Publishing…' : 'Publish APK'}
        </button>
      </form>

      <div className="card">
        <h2 style={{ marginTop: 0 }}>Recent releases</h2>
        {releases.length === 0 ? (
          <p className="muted">No releases yet.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Version</th>
                  <th>Published</th>
                  <th>Notes</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {releases.map((row) => (
                  <tr key={row.id}>
                    <td>
                      {row.version}
                      {row.isLatest ? ' · latest' : ''}
                    </td>
                    <td>{formatReleasedAt(row.releasedAtUtc)}</td>
                    <td>{row.notes || '—'}</td>
                    <td>
                      <a href={row.downloadUrl} download={`pasakay-passenger-${row.version}.apk`}>
                        APK
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
