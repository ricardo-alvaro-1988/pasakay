import { FormEvent, useEffect, useState } from 'react'
import { api, OperatorCarListing, OperatorRentalInquiry } from './api'

const PAGE_SIZE = 12
const STATUS_FILTERS = ['', 'Pending', 'Matched', 'Contacted', 'Closed'] as const

function fmt(value: string) {
  try {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Manila',
    }).format(new Date(value))
  } catch {
    return value
  }
}

function vehicleLabel(type: string) {
  switch (type) {
    case 'PickupL300':
      return 'Pickup L300'
    case 'PickupCargo':
      return 'Pickup (Cargo)'
    case 'Mpv':
      return 'MPV'
    case 'Suv':
      return 'SUV'
    default:
      return type
  }
}

function statusClass(status: string) {
  switch (status) {
    case 'Pending':
      return 'rental-status pending'
    case 'Matched':
      return 'rental-status matched'
    case 'Contacted':
      return 'rental-status contacted'
    case 'Closed':
      return 'rental-status closed'
    default:
      return 'rental-status'
  }
}

function listingThumbs(row: OperatorCarListing) {
  return [row.frontImageUrl, row.backImageUrl, row.leftImageUrl, row.rightImageUrl, row.insideImageUrl].filter(
    (url): url is string => Boolean(url),
  )
}

function RentalPager({
  page,
  pageSize,
  total,
  onPage,
}: {
  page: number
  pageSize: number
  total: number
  onPage: (page: number) => void
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, total)
  return (
    <div className="pager">
      <span>
        {from}–{to} of {total}
      </span>
      <div className="pager-btns">
        <button type="button" className="btn tiny" disabled={page <= 1} onClick={() => onPage(page - 1)}>
          Prev
        </button>
        <span>
          Page {page} / {pages}
        </span>
        <button type="button" className="btn tiny" disabled={page >= pages} onClick={() => onPage(page + 1)}>
          Next
        </button>
      </div>
    </div>
  )
}

export function OperatorRentalsPage() {
  const [tab, setTab] = useState<'inquiries' | 'listings'>('inquiries')
  const [status, setStatus] = useState('')
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [inquiries, setInquiries] = useState<OperatorRentalInquiry[]>([])
  const [listings, setListings] = useState<OperatorCarListing[]>([])
  const [pendingInquiries, setPendingInquiries] = useState(0)
  const [pendingListings, setPendingListings] = useState(0)
  const [error, setError] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const [matchFor, setMatchFor] = useState<OperatorRentalInquiry | null>(null)
  const [matches, setMatches] = useState<OperatorCarListing[]>([])
  const [matchLoading, setMatchLoading] = useState(false)
  const [matchError, setMatchError] = useState('')

  async function loadSummary() {
    const data = await api.operatorRentalsSummary()
    setPendingInquiries(data.pendingInquiries)
    setPendingListings(data.pendingListings)
  }

  async function loadList() {
    setLoading(true)
    setError('')
    try {
      if (tab === 'inquiries') {
        const data = await api.operatorRentalInquiries({
          q: search || undefined,
          status: status || undefined,
          page,
          pageSize: PAGE_SIZE,
        })
        setInquiries(data.items)
        setTotal(data.total)
      } else {
        const data = await api.operatorRentalListings({
          q: search || undefined,
          status: status || undefined,
          page,
          pageSize: PAGE_SIZE,
        })
        setListings(data.items)
        setTotal(data.total)
      }
      await loadSummary()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load rentals.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadList()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab, status, search, page])

  function onSearch(e: FormEvent) {
    e.preventDefault()
    setPage(1)
    setSearch(q.trim())
  }

  function changeTab(next: 'inquiries' | 'listings') {
    setTab(next)
    setPage(1)
    setMatchFor(null)
  }

  function changeStatus(next: string) {
    setStatus(next)
    setPage(1)
  }

  async function setInquiryStatus(id: string, next: string) {
    setBusyId(id)
    setError('')
    try {
      await api.setRentalInquiryStatus(id, next)
      await loadList()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not update status.')
    } finally {
      setBusyId(null)
    }
  }

  async function setListingStatus(id: string, next: string) {
    setBusyId(id)
    setError('')
    try {
      await api.setRentalListingStatus(id, next)
      await loadList()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not update status.')
    } finally {
      setBusyId(null)
    }
  }

  async function openSuggest(row: OperatorRentalInquiry) {
    setMatchFor(row)
    setMatches([])
    setMatchError('')
    setMatchLoading(true)
    try {
      const data = await api.operatorRentalMatches(row.id)
      setMatches(data)
    } catch (err) {
      setMatchError(err instanceof Error ? err.message : 'Could not load suggested vehicles.')
    } finally {
      setMatchLoading(false)
    }
  }

  async function assignVehicle(listingId: string) {
    if (!matchFor) return
    setBusyId(listingId)
    setMatchError('')
    try {
      await api.assignRentalMatch(matchFor.id, listingId)
      setMatchFor(null)
      await loadList()
    } catch (err) {
      setMatchError(err instanceof Error ? err.message : 'Could not assign vehicle.')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="form-sections rental-op">
      <div className="card rental-op-hero">
        <div className="rental-op-hero-copy">
          <h2 style={{ marginTop: 0 }}>Rentals</h2>
          <p className="muted" style={{ marginBottom: 0 }}>
            Match customer requests to listed cars by vehicle type and availability.
          </p>
        </div>
        <div className="rental-op-stats">
          <div className="rental-op-stat">
            <span>Pending requests</span>
            <strong>{pendingInquiries}</strong>
          </div>
          <div className="rental-op-stat">
            <span>Pending listings</span>
            <strong>{pendingListings}</strong>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="chips">
          <button type="button" className={tab === 'inquiries' ? 'on' : ''} onClick={() => changeTab('inquiries')}>
            Rental requests
          </button>
          <button type="button" className={tab === 'listings' ? 'on' : ''} onClick={() => changeTab('listings')}>
            Listed cars
          </button>
        </div>

        <form className="rental-op-toolbar" onSubmit={onSearch}>
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={tab === 'inquiries' ? 'Search name, mobile, location…' : 'Search name, plate, mobile…'}
          />
          <button className="btn" type="submit">
            Search
          </button>
        </form>

        <div className="chips" style={{ marginTop: 10 }}>
          {STATUS_FILTERS.map((value) => (
            <button
              key={value || 'all'}
              type="button"
              className={status === value ? 'on' : ''}
              onClick={() => changeStatus(value)}
            >
              {value || 'All'}
            </button>
          ))}
        </div>
        {error ? <p className="error">{error}</p> : null}
      </div>

      {tab === 'inquiries' ? (
        <div className="card">
          <div className="rental-op-section-head">
            <h2 style={{ margin: 0 }}>Rental Car requests</h2>
            <RentalPager page={page} pageSize={PAGE_SIZE} total={total} onPage={setPage} />
          </div>
          {loading ? (
            <p className="muted">Loading…</p>
          ) : inquiries.length === 0 ? (
            <p className="muted">No rental requests found.</p>
          ) : (
            <div className="rental-op-grid">
              {inquiries.map((row) => (
                <article key={row.id} className="rental-op-card">
                  <div className="rental-op-card-top">
                    <span className="rental-op-vehicle">{vehicleLabel(row.vehicleType)}</span>
                    <span className={statusClass(row.status)}>{row.status}</span>
                  </div>
                  <h3>{row.customerName}</h3>
                  <p className="rental-op-meta">{row.mobileNumber}</p>
                  <p className="rental-op-schedule">
                    {fmt(row.scheduleFromUtc)}
                    <span>→</span>
                    {fmt(row.scheduleToUtc)}
                  </p>
                  <p className="rental-op-location">{row.locationDetails}</p>
                  {row.notes ? <p className="muted rental-op-notes">{row.notes}</p> : null}
                  {row.matchedPlate ? (
                    <p className="rental-op-assigned">
                      Assigned vehicle: <strong>{row.matchedPlate}</strong>
                    </p>
                  ) : null}
                  <div className="rental-op-actions">
                    {row.status !== 'Matched' && row.status !== 'Closed' ? (
                      <button
                        className="btn tiny primary-ish"
                        type="button"
                        disabled={busyId === row.id}
                        onClick={() => void openSuggest(row)}
                      >
                        Suggest vehicles
                      </button>
                    ) : null}
                    {row.status !== 'Contacted' && row.status !== 'Closed' && row.status !== 'Matched' ? (
                      <button
                        className="btn tiny"
                        type="button"
                        disabled={busyId === row.id}
                        onClick={() => void setInquiryStatus(row.id, 'Contacted')}
                      >
                        Mark contacted
                      </button>
                    ) : null}
                    {row.status !== 'Closed' ? (
                      <button
                        className="btn tiny"
                        type="button"
                        disabled={busyId === row.id}
                        onClick={() => void setInquiryStatus(row.id, 'Closed')}
                      >
                        Close
                      </button>
                    ) : null}
                  </div>
                  <small className="muted">Requested {fmt(row.createdAtUtc)}</small>
                </article>
              ))}
            </div>
          )}
          <RentalPager page={page} pageSize={PAGE_SIZE} total={total} onPage={setPage} />
        </div>
      ) : (
        <div className="card">
          <div className="rental-op-section-head">
            <h2 style={{ margin: 0 }}>Listed cars</h2>
            <RentalPager page={page} pageSize={PAGE_SIZE} total={total} onPage={setPage} />
          </div>
          {loading ? (
            <p className="muted">Loading…</p>
          ) : listings.length === 0 ? (
            <p className="muted">No car listings found.</p>
          ) : (
            <div className="rental-op-grid">
              {listings.map((row) => {
                const thumbs = listingThumbs(row)
                return (
                  <article key={row.id} className="rental-op-card listing">
                    <div className="rental-op-card-media">
                      {thumbs[0] ? <img src={thumbs[0]} alt="" /> : <div className="rental-op-media-empty">No photo</div>}
                    </div>
                    <div className="rental-op-card-top">
                      <span className="rental-op-vehicle">{vehicleLabel(row.vehicleType)}</span>
                      <span className={statusClass(row.status)}>{row.status}</span>
                    </div>
                    <h3>{row.plateNumber}</h3>
                    <p className="rental-op-meta">
                      {row.customerName} · {row.mobileNumber}
                    </p>
                    <p className="rental-op-meta">
                      {row.seater} seater · {row.availableDays.join(', ') || '—'}
                    </p>
                    {thumbs.length > 1 ? (
                      <div className="rental-op-thumbs">
                        {thumbs.slice(0, 5).map((url) => (
                          <a key={url} href={url} target="_blank" rel="noreferrer">
                            <img src={url} alt="" />
                          </a>
                        ))}
                      </div>
                    ) : null}
                    <div className="rental-op-actions">
                      {row.status !== 'Contacted' && row.status !== 'Closed' && row.status !== 'Matched' ? (
                        <button
                          className="btn tiny"
                          type="button"
                          disabled={busyId === row.id}
                          onClick={() => void setListingStatus(row.id, 'Contacted')}
                        >
                          Mark contacted
                        </button>
                      ) : null}
                      {row.status !== 'Closed' ? (
                        <button
                          className="btn tiny"
                          type="button"
                          disabled={busyId === row.id}
                          onClick={() => void setListingStatus(row.id, 'Closed')}
                        >
                          Close
                        </button>
                      ) : null}
                    </div>
                    <small className="muted">Listed {fmt(row.createdAtUtc)}</small>
                  </article>
                )
              })}
            </div>
          )}
          <RentalPager page={page} pageSize={PAGE_SIZE} total={total} onPage={setPage} />
        </div>
      )}

      {matchFor ? (
        <div className="rental-match-overlay" role="dialog" aria-modal="true" aria-labelledby="rental-match-title">
          <div className="rental-match-panel">
            <div className="rental-match-head">
              <div>
                <h2 id="rental-match-title" style={{ margin: 0 }}>
                  Suggest vehicles
                </h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  {matchFor.customerName} · {vehicleLabel(matchFor.vehicleType)} · {fmt(matchFor.scheduleFromUtc)} →{' '}
                  {fmt(matchFor.scheduleToUtc)}
                </p>
                <p className="muted" style={{ margin: '4px 0 0' }}>
                  Matches by same vehicle type and availability covering the schedule.
                </p>
              </div>
              <button type="button" className="btn tiny" onClick={() => setMatchFor(null)}>
                Close
              </button>
            </div>
            {matchError ? <p className="error">{matchError}</p> : null}
            {matchLoading ? (
              <p className="muted">Finding vehicles…</p>
            ) : matches.length === 0 ? (
              <p className="muted">No listed cars match this request yet.</p>
            ) : (
              <div className="rental-op-grid match-grid">
                {matches.map((row) => {
                  const thumbs = listingThumbs(row)
                  return (
                    <article key={row.id} className="rental-op-card listing">
                      <div className="rental-op-card-media">
                        {thumbs[0] ? <img src={thumbs[0]} alt="" /> : <div className="rental-op-media-empty">No photo</div>}
                      </div>
                      <div className="rental-op-card-top">
                        <span className="rental-op-vehicle">{vehicleLabel(row.vehicleType)}</span>
                        <span className={statusClass(row.status)}>{row.status}</span>
                      </div>
                      <h3>{row.plateNumber}</h3>
                      <p className="rental-op-meta">
                        {row.customerName} · {row.seater} seater
                      </p>
                      <p className="rental-op-meta">{row.availableDays.join(', ') || '—'}</p>
                      <div className="rental-op-actions">
                        <button
                          className="btn tiny primary-ish"
                          type="button"
                          disabled={busyId === row.id}
                          onClick={() => void assignVehicle(row.id)}
                        >
                          {busyId === row.id ? 'Assigning…' : 'Assign vehicle'}
                        </button>
                      </div>
                    </article>
                  )
                })}
              </div>
            )}
          </div>
        </div>
      ) : null}
    </div>
  )
}
