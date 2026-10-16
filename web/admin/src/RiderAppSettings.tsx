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

export function RiderAppSettingsPage() {
  const [latest, setLatest] = useState<Release | null>(null)
  const [releases, setReleases] = useState<Release[]>([])
  const [selected, setSelected] = useState<Set<string>>(() => new Set())
  const [version, setVersion] = useState('')
  const [notes, setNotes] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [loading, setLoading] = useState(true)
  const [uploadLabel, setUploadLabel] = useState('')
  const [uploadPercent, setUploadPercent] = useState<number | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  async function load() {
    const data = await api.getRiderApp()
    setLatest(data.latest)
    setReleases(data.releases)
    setSelected(new Set())
  }

  useEffect(() => {
    let cancelled = false
    load()
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load rider app releases.')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  function toggleOne(id: string, checked: boolean) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (checked) next.add(id)
      else next.delete(id)
      return next
    })
  }

  function toggleAll(checked: boolean) {
    setSelected(checked ? new Set(releases.map((r) => r.id)) : new Set())
  }

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
      const data = await api.publishRiderApp(
        { version: version.trim(), notes: notes.trim() || undefined, file },
        (loaded, total) => {
          const percent = total > 0 ? Math.min(100, Math.round((loaded / total) * 100)) : 0
          setUploadPercent(percent)
          setUploadLabel(
            percent >= 100
              ? 'Upload finished. Publishing the release…'
              : `Uploading ${formatBytes(loaded)} of ${formatBytes(total)} (${percent}%)`,
          )
        },
      )
      setLatest(data.latest)
      setReleases(data.releases)
      setSelected(new Set())
      setVersion('')
      setNotes('')
      setFile(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
      setNotice(`Published version ${data.latest?.version ?? version.trim()}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not publish the APK.')
    } finally {
      setBusy(false)
      setUploadPercent(null)
      setUploadLabel('')
    }
  }

  async function deleteSelected() {
    const ids = [...selected]
    if (ids.length === 0) {
      setError('Select at least one release to delete.')
      return
    }
    const label = ids.length === 1 ? 'this release and its APK file' : `${ids.length} releases and their APK files`
    if (!window.confirm(`Delete ${label}? This cannot be undone.`)) return

    setDeleting(true)
    setError('')
    setNotice('')
    try {
      const data = await api.deleteRiderAppReleases(ids)
      setLatest(data.latest)
      setReleases(data.releases)
      setSelected(new Set())
      setNotice(ids.length === 1 ? 'Deleted 1 release.' : `Deleted ${ids.length} releases.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete the selected releases.')
    } finally {
      setDeleting(false)
    }
  }

  if (loading) {
    return (
      <div className="card">
        <p className="muted">Loading rider app…</p>
      </div>
    )
  }

  const allSelected = releases.length > 0 && selected.size === releases.length
  const someSelected = selected.size > 0

  return (
    <div className="form-sections">
      <div className="card">
        <h2 style={{ marginTop: 0 }}>Current rider app</h2>
        <p className="muted">
          Drivers download from{' '}
          <a href="/rider/download" target="_blank" rel="noreferrer">
            /rider/download
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
            <a href={latest.downloadUrl} download={`pasakay-rider-${latest.version}.apk`}>
              Download current APK
            </a>
          </div>
        ) : (
          <p style={{ marginTop: 12 }}>No APK published yet.</p>
        )}
      </div>

      <form className="card" onSubmit={publish}>
        <h2 style={{ marginTop: 0 }}>Publish new build</h2>
        <p className="muted">Upload an APK and set its version. Re-uploading the same version replaces the existing file. This becomes the download on /rider/download.</p>
        <label className="field">
          <span>Version</span>
          <input
            value={version}
            onChange={(e) => setVersion(e.target.value)}
            placeholder="1.4.2"
            required
            maxLength={40}
            disabled={busy || deleting}
          />
        </label>
        <label className="field">
          <span>Release notes (optional)</span>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            maxLength={2000}
            disabled={busy || deleting}
            placeholder="What’s new for riders"
          />
        </label>
        <label className="field">
          <span>APK file</span>
          <input
            ref={fileInputRef}
            type="file"
            accept=".apk,application/vnd.android.package-archive"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            disabled={busy || deleting}
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
        <button className="btn" type="submit" disabled={busy || deleting} style={{ marginTop: 12, maxWidth: 220 }}>
          {busy ? 'Publishing…' : 'Publish APK'}
        </button>
      </form>

      <div className="card">
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
          <h2 style={{ marginTop: 0, marginBottom: 0 }}>Recent releases</h2>
          {releases.length > 0 ? (
            <button
              className="btn tiny danger"
              type="button"
              disabled={!someSelected || busy || deleting}
              onClick={() => void deleteSelected()}
            >
              {deleting ? 'Deleting…' : someSelected ? `Delete selected (${selected.size})` : 'Delete selected'}
            </button>
          ) : null}
        </div>
        {!busy && error ? <p className="error">{error}</p> : null}
        {!busy && notice ? <p className="ok">{notice}</p> : null}
        {releases.length === 0 ? (
          <p className="muted">No releases yet.</p>
        ) : (
          <div className="table-wrap" style={{ marginTop: 12 }}>
            <table>
              <thead>
                <tr>
                  <th style={{ width: 36 }}>
                    <input
                      type="checkbox"
                      checked={allSelected}
                      onChange={(e) => toggleAll(e.target.checked)}
                      disabled={busy || deleting}
                      aria-label="Select all releases"
                    />
                  </th>
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
                      <input
                        type="checkbox"
                        checked={selected.has(row.id)}
                        onChange={(e) => toggleOne(row.id, e.target.checked)}
                        disabled={busy || deleting}
                        aria-label={`Select version ${row.version}`}
                      />
                    </td>
                    <td>
                      {row.version}
                      {row.isLatest ? ' · latest' : ''}
                    </td>
                    <td>{formatReleasedAt(row.releasedAtUtc)}</td>
                    <td>{row.notes || '—'}</td>
                    <td>
                      <a href={row.downloadUrl} download={`pasakay-rider-${row.version}.apk`}>
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
