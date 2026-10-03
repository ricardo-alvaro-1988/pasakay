import { useEffect, useState } from 'react'
import { api } from './api'

type Inquiry = {
  id: string
  customerName: string
  mobileNumber: string
  vehicleType: string
  scheduleFromUtc: string
  scheduleToUtc: string
  locationDetails: string
  locationLat: number
  locationLng: number
  notes: string | null
  status: string
  createdAtUtc: string
}

type Listing = {
  id: string
  customerName: string
  mobileNumber: string
  vehicleType: string
  plateNumber: string
  seater: number
  frontImageUrl: string | null
  backImageUrl: string | null
  leftImageUrl: string | null
  rightImageUrl: string | null
  insideImageUrl: string | null
  availableDays: string[]
  status: string
  createdAtUtc: string
}

function fmt(value: string) {
  try {
    return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
  } catch {
    return value
  }
}

export function OperatorRentalsPage() {
  const [status, setStatus] = useState('')
  const [inquiries, setInquiries] = useState<Inquiry[]>([])
  const [listings, setListings] = useState<Listing[]>([])
  const [pendingInquiries, setPendingInquiries] = useState(0)
  const [pendingListings, setPendingListings] = useState(0)
  const [error, setError] = useState('')
  const [busyId, setBusyId] = useState<string | null>(null)
  const [tab, setTab] = useState<'inquiries' | 'listings'>('inquiries')

  async function load() {
    const data = await api.operatorRentals(status || undefined)
    setInquiries(data.inquiries)
    setListings(data.listings)
    setPendingInquiries(data.pendingInquiries)
    setPendingListings(data.pendingListings)
  }

  useEffect(() => {
    let cancelled = false
    load()
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load rentals.')
      })
    return () => {
      cancelled = true
    }
  }, [status])

  async function setInquiryStatus(id: string, next: string) {
    setBusyId(id)
    setError('')
    try {
      await api.setRentalInquiryStatus(id, next)
      await load()
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
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not update status.')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <div className="form-sections">
      <div className="card">
        <h2 style={{ marginTop: 0 }}>Rentals</h2>
        <p className="muted">
          Customer rental requests and cars listed for rent. Pending: {pendingInquiries} requests · {pendingListings} listings.
        </p>
        <div className="chips" style={{ marginTop: 12 }}>
          <button type="button" className={tab === 'inquiries' ? 'on' : ''} onClick={() => setTab('inquiries')}>
            Rental requests
          </button>
          <button type="button" className={tab === 'listings' ? 'on' : ''} onClick={() => setTab('listings')}>
            Listed cars
          </button>
        </div>
        <div className="chips" style={{ marginTop: 8 }}>
          {['', 'Pending', 'Contacted', 'Closed'].map((value) => (
            <button
              key={value || 'all'}
              type="button"
              className={status === value ? 'on' : ''}
              onClick={() => setStatus(value)}
            >
              {value || 'All'}
            </button>
          ))}
        </div>
        {error ? <p className="error">{error}</p> : null}
      </div>

      {tab === 'inquiries' ? (
        <div className="card">
          <h2 style={{ marginTop: 0 }}>Rental Car requests</h2>
          {inquiries.length === 0 ? (
            <p className="muted">No rental requests yet.</p>
          ) : (
            <div className="list">
              {inquiries.map((row) => (
                <div className="row" key={row.id}>
                  <div>
                    <strong>
                      {row.customerName} · {row.vehicleType}
                    </strong>
                    <div className="muted">
                      {fmt(row.scheduleFromUtc)} → {fmt(row.scheduleToUtc)} · {row.mobileNumber}
                    </div>
                    <div>{row.locationDetails}</div>
                    {row.notes ? <div className="muted">{row.notes}</div> : null}
                    <small className="muted">
                      {fmt(row.createdAtUtc)} · {row.status}
                    </small>
                  </div>
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                    {row.status !== 'Contacted' ? (
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
                </div>
              ))}
            </div>
          )}
        </div>
      ) : (
        <div className="card">
          <h2 style={{ marginTop: 0 }}>List Your Car</h2>
          {listings.length === 0 ? (
            <p className="muted">No car listings yet.</p>
          ) : (
            <div className="list">
              {listings.map((row) => (
                <div className="row" key={row.id} style={{ alignItems: 'flex-start' }}>
                  <div style={{ flex: 1 }}>
                    <strong>
                      {row.customerName} · {row.vehicleType} · {row.plateNumber}
                    </strong>
                    <div className="muted">
                      {row.seater} seater · {row.availableDays.join(', ') || '—'} · {row.mobileNumber}
                    </div>
                    <div className="rental-op-thumbs">
                      {[row.frontImageUrl, row.backImageUrl, row.leftImageUrl, row.rightImageUrl, row.insideImageUrl]
                        .filter(Boolean)
                        .map((url) => (
                          <a key={url!} href={url!} target="_blank" rel="noreferrer">
                            <img src={url!} alt="" />
                          </a>
                        ))}
                    </div>
                    <small className="muted">
                      {fmt(row.createdAtUtc)} · {row.status}
                    </small>
                  </div>
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                    {row.status !== 'Contacted' ? (
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
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
