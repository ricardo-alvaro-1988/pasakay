import { FormEvent, useEffect, useState } from 'react'
import {
  api,
  BookBody,
  ChatMessage,
  CustomerTrip,
  CustomerTripDetail,
  Desk,
  Gender,
  PaymentMethod,
  chatFromRider,
  isOperatorCoverageError,
  mediaUrl,
  paymentLabel,
  peso,
  kmLabel,
  phWhen,
  Stop,
  tripHeadline,
  VehicleType,
} from './api'
import { VEHICLE_ART } from './vehicle-art'
import { BookingHistoryRating, RateRidePanel } from './rate-ride'
import { NoOperatorNotice, useNoOperatorNotice } from './no-operator-notice'

const PAYMENT_METHODS: PaymentMethod[] = ['Cash', 'GCash', 'Maya', 'Other']

export function PaymentBar({
  payment,
  refNo,
  onPayment,
  onRefNo,
  allowed,
}: {
  payment: PaymentMethod
  refNo: string
  onPayment: (method: PaymentMethod) => void
  onRefNo: (value: string) => void
  allowed?: PaymentMethod[]
}) {
  const methods = allowed?.length ? PAYMENT_METHODS.filter((method) => allowed.includes(method)) : PAYMENT_METHODS
  return (
    <>
      <div className="pays">
        {methods.map((method) => (
          <button key={method} type="button" className={payment === method ? 'on' : ''} onClick={() => onPayment(method)}>
            {paymentLabel(method)}
          </button>
        ))}
      </div>
      {payment !== 'Cash' && (
        <label className="field pay-ref">
          <span>REF NO</span>
          <input
            value={refNo}
            onChange={(e) => onRefNo(e.target.value)}
            placeholder={payment === 'Other' ? 'Ref no. / other payment' : 'Ref no.'}
          />
        </label>
      )}
    </>
  )
}

export function BookingScreen({ desk, onDesk }: { desk: Desk; onDesk: (desk: Desk) => void }) {
  const [error, setError] = useState('')
  const [rateTrip, setRateTrip] = useState<CustomerTrip | null>(null)
  const [openHistoryId, setOpenHistoryId] = useState<string | null>(null)
  const history = desk.recent.filter((trip) => trip.status === 'Completed' || trip.status === 'Cancelled')
  const needsRating = desk.pendingRating ?? desk.recent.find((trip) => trip.canRate) ?? null

  async function cancel(id: string) {
    try { onDesk(await api.cancel(id)); setError('') }
    catch (err) { setError(err instanceof Error ? err.message : 'Could not cancel.') }
  }

  return (
    <div className="page">
      <h2>Booking</h2>
      {error && <p className="error">{error}</p>}
      {(rateTrip || needsRating) && (
        <div style={{ marginBottom: 16 }}>
          <RateRidePanel
            trip={rateTrip ?? needsRating!}
            onDesk={onDesk}
            onError={setError}
            onDone={() => setRateTrip(null)}
            allowSkip={Boolean(rateTrip)}
          />
        </div>
      )}
      <h3 className="section-title">Active booking</h3>
      {desk.activeTrip ? <TripCard trip={desk.activeTrip} onCancel={cancel} /> : <p className="muted">No active ride right now.</p>}
      <h3 className="section-title">Scheduled</h3>
      {(desk.scheduled ?? []).length === 0 && <p className="muted">No upcoming scheduled rides.</p>}
      {(desk.scheduled ?? []).map((trip) => <TripCard key={trip.id} trip={trip} onCancel={cancel} />)}
      <h3 className="section-title">History</h3>
      {history.length === 0 && <p className="muted">Completed and cancelled trips will show here.</p>}
      {history.map((trip) => (
        <HistoryTripCard
          key={trip.id}
          trip={trip}
          open={openHistoryId === trip.id}
          onToggle={() => setOpenHistoryId((id) => id === trip.id ? null : trip.id)}
          onRate={trip.canRate ? () => setRateTrip(trip) : undefined}
          onError={setError}
        />
      ))}
    </div>
  )
}

function TripCard({
  trip,
  onCancel,
}: {
  trip: CustomerTrip
  onCancel?: (id: string) => void
}) {
  return (
    <article className="card">
      <BookingSummary trip={trip} />
      <BookingRoute trip={trip} />
      <div className="booking-meta">
        <div className="booking-meta-row">
          <span>Trip details</span>
          <p>{kmLabel(trip.distanceKm)} · {trip.vehicleType} · {paymentLabel(trip.paymentMethod, trip.paymentMethodOther)}</p>
        </div>
        {trip.riderName && (
          <div className="booking-meta-row">
            <span>Rider</span>
            <p>{trip.riderName}{trip.plateNumber ? ` · ${trip.plateNumber}` : ''}{trip.riderPhone ? ` · ${trip.riderPhone}` : ''}</p>
          </div>
        )}
      </div>
      {onCancel && trip.canCancel && <button className="danger" style={{ marginTop: 10 }} onClick={() => onCancel(trip.id)}>Cancel</button>}
    </article>
  )
}

function HistoryTripCard({
  trip,
  open,
  onToggle,
  onRate,
  onError,
}: {
  trip: CustomerTrip
  open: boolean
  onToggle: () => void
  onRate?: () => void
  onError: (text: string) => void
}) {
  const [detail, setDetail] = useState<CustomerTripDetail | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!open || detail) return
    let cancelled = false
    setBusy(true)
    api.tripDetail(trip.id)
      .then((next) => { if (!cancelled) setDetail(next) })
      .catch((err) => { if (!cancelled) onError(err instanceof Error ? err.message : 'Could not load booking details.') })
      .finally(() => { if (!cancelled) setBusy(false) })
    return () => { cancelled = true }
  }, [open, detail, trip.id, onError])

  return (
    <article className={`card history-card${open ? ' open' : ''}`}>
      <button className="booking-summary-btn" type="button" aria-expanded={open} onClick={onToggle}>
        <BookingSummary trip={trip} chevron />
      </button>
      {open && (
        <div className="booking-expand">
          {busy && !detail && <p className="muted">Loading booking details…</p>}
          {detail ? (
            <BookingDetailBody detail={detail} />
          ) : !busy ? (
            <>
              <BookingRoute trip={trip} />
              <div className="booking-meta">
                <div className="booking-meta-row">
                  <span>Trip details</span>
                  <p>{kmLabel(trip.distanceKm)} · {trip.vehicleType} · {paymentLabel(trip.paymentMethod, trip.paymentMethodOther)}</p>
                </div>
                {trip.riderName && (
                  <div className="booking-meta-row">
                    <span>Rider</span>
                    <p>{trip.riderName}{trip.plateNumber ? ` · ${trip.plateNumber}` : ''}</p>
                  </div>
                )}
              </div>
            </>
          ) : null}
          <BookingHistoryRating trip={trip} />
          {onRate && <button className="secondary" style={{ marginTop: 10 }} type="button" onClick={onRate}>Rate ride</button>}
        </div>
      )}
    </article>
  )
}

function BookingSummary({ trip, chevron }: { trip: CustomerTrip | CustomerTripDetail; chevron?: boolean }) {
  return (
    <>
      <div className="card-head">
        <span className="booking-ref">{trip.reference}</span>
        <b className="price">{peso(trip.fare)}</b>
      </div>
      <div className="booking-summary-meta">
        <span className={`tag ${String(trip.status).toLowerCase()}`}>{tripHeadline(String(trip.status))}</span>
        <span className="booking-when">{trip.scheduledAtUtc ? phWhen(trip.scheduledAtUtc) : phWhen(trip.requestedAtUtc)}</span>
        {chevron ? <span className="history-chevron" aria-hidden="true">›</span> : null}
      </div>
    </>
  )
}

function BookingRoute({ trip }: { trip: Pick<CustomerTrip, 'pickup' | 'dropoff'> }) {
  return (
    <div className="route-mini">
      <span>Pickup</span>
      <p>{trip.pickup}</p>
      <span>Drop-off</span>
      <p>{trip.dropoff}</p>
    </div>
  )
}

function BookingDetailBody({ detail }: { detail: CustomerTripDetail }) {
  return (
    <>
      <BookingRoute trip={detail} />
      <div className="booking-meta">
        <div className="booking-meta-row">
          <span>Trip details</span>
          <p>{kmLabel(detail.distanceKm)} · {detail.vehicleType} · {paymentLabel(detail.paymentMethod, detail.paymentMethodOther)}</p>
        </div>
        <div className="booking-meta-row">
          <span>Rider</span>
          <p>{detail.riderName}{detail.plateNumber ? ` · ${detail.plateNumber}` : ''}{detail.riderPhone ? ` · ${detail.riderPhone}` : ''}</p>
        </div>
        {detail.notes && (
          <div className="booking-meta-row">
            <span>Notes</span>
            <p>{detail.notes}</p>
          </div>
        )}
      </div>
      {detail.cancelReason && <p className="error" style={{ margin: '8px 0 0' }}>Cancel reason: {detail.cancelReason}</p>}
      <div className="booking-meta booking-chat">
        <span>Chat history</span>
        <p className="booking-chat-note">Read only</p>
        <div className="booking-chat-log">
          {detail.chat.length === 0 && <p className="booking-chat-empty">No chat messages on this booking.</p>}
          {detail.chat.map((msg) => (
            <BookingChatBubble key={msg.id} msg={msg} />
          ))}
        </div>
      </div>
    </>
  )
}

function BookingChatBubble({ msg }: { msg: ChatMessage }) {
  return (
    <div className={`chat-bubble ${chatFromRider(msg.sender) ? 'theirs' : 'mine'}`}>
      {msg.photoUrl ? <img className="chat-photo" src={mediaUrl(msg.photoUrl)} alt="" /> : null}
      {msg.body ? <p>{msg.body}</p> : null}
      <small>{phWhen(msg.sentAtUtc)}</small>
    </div>
  )
}

export function ScheduleScreen({
  desk,
  onDesk,
  pickup,
  dropoff,
  onPickPickup,
  onPickDropoff,
}: {
  desk: Desk
  onDesk: (desk: Desk) => void
  pickup: Stop | null
  dropoff: Stop | null
  onPickPickup: () => void
  onPickDropoff: () => void
}) {
  const [vehicle, setVehicle] = useState<VehicleType>('Motorcycle')
  const [payment, setPayment] = useState<PaymentMethod>('Cash')
  const [paymentRef, setPaymentRef] = useState('')
  const [when, setWhen] = useState(() => toLocalInput(new Date(Date.now() + 60 * 60 * 1000)))
  const [error, setError] = useState('')
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [coverageHint, setCoverageHint] = useState(false)
  const noOperator = useNoOperatorNotice(pickup, dropoff, true, coverageHint)

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!pickup || !dropoff) {
      setError('Set pickup and drop-off first.')
      return
    }
    const scheduled = new Date(when)
    if (Number.isNaN(scheduled.getTime())) {
      setError('Choose a valid date and time.')
      return
    }
    if (scheduled.getTime() < Date.now() + 10 * 60 * 1000) {
      setError('Schedule the booking at least 10 minutes from now.')
      return
    }
    setBusy(true)
    setError('')
    setNote('')
    try {
      onDesk(await api.book({
        ...bookBody(vehicle, pickup, dropoff, payment, paymentRef),
        scheduledAtUtc: scheduled.toISOString(),
      }))
      setNote('Scheduled booking requested. Riders in the area will see it closer to that time.')
      setWhen(toLocalInput(new Date(Date.now() + 60 * 60 * 1000)))
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Could not schedule.'
      if (isOperatorCoverageError(message)) {
        setCoverageHint(true)
        setError('')
      } else {
        setError(message)
      }
    } finally {
      setBusy(false)
    }
  }

  async function cancelScheduled(id: string) {
    setError('')
    setNote('')
    try { onDesk(await api.cancel(id)) }
    catch (err) { setError(err instanceof Error ? err.message : 'Could not cancel.') }
  }

  return (
    <form className="page" onSubmit={submit}>
      <h2>Schedule</h2>
      <p className="muted">Request a booking for later. We broadcast it to riders in the operator service area.</p>
      <label className="field">
        <span>WHEN</span>
        <input
          type="datetime-local"
          value={when}
          min={toLocalInput(new Date(Date.now() + 10 * 60 * 1000))}
          onChange={(e) => setWhen(e.target.value)}
        />
      </label>
      <div className="stop">
        <div className="stop-row">
          <span className="pin beat"><span className="dot a" /></span>
          <button type="button" className="addr" onClick={onPickPickup}>
            <small>Pickup</small>
            {pickup?.label ?? 'Search or pin pickup'}
          </button>
        </div>
        <div className="stop-row">
          <span className="pin beat"><span className="dot b" /></span>
          <button type="button" className="addr" onClick={onPickDropoff}>
            <small>Drop-off</small>
            {dropoff?.label ?? 'Search or pin drop-off'}
          </button>
        </div>
      </div>
      <p className="section-title">Vehicle</p>
      <div className="vehicles">
        <button type="button" className={`vehicle ${vehicle === 'Motorcycle' ? 'on' : ''}`} onClick={() => setVehicle('Motorcycle')}>
          <span className="icon moto"><img src={VEHICLE_ART.Motorcycle} alt="" /></span>
          <span className="copy"><b>Motorcycle</b></span>
        </button>
        <button type="button" className={`vehicle ${vehicle === 'Tricycle' ? 'on' : ''}`} onClick={() => setVehicle('Tricycle')}>
          <span className="icon"><img src={VEHICLE_ART.Tricycle} alt="" /></span>
          <span className="copy"><b>Tricycle</b></span>
        </button>
      </div>
      <p className="section-title">Payment</p>
      <PaymentBar
        payment={payment}
        refNo={paymentRef}
        onPayment={(method) => {
          setPayment(method)
          if (method === 'Cash') setPaymentRef('')
        }}
        onRefNo={setPaymentRef}
      />
      {error && <p className="error">{error}</p>}
      <NoOperatorNotice show={noOperator.uncovered} />
      {note && <p className="muted">{note}</p>}
      <button
        className={`primary${noOperator.searching ? ' searching pulse' : ''}`}
        disabled={busy || noOperator.searching || noOperator.uncovered || !pickup || !dropoff || (payment === 'Other' && !paymentRef.trim())}
      >
        {busy || noOperator.searching ? 'Finding a ride…' : 'Request scheduled booking'}
      </button>
      <h3 className="section-title">Upcoming</h3>
      {(desk.scheduled ?? []).length === 0 && <p className="muted">None yet.</p>}
      {(desk.scheduled ?? []).map((trip) => (
        <TripCard key={trip.id} trip={trip} onCancel={cancelScheduled} />
      ))}
    </form>
  )
}

function bookBody(vehicle: VehicleType, pickup: Stop, dropoff: Stop, payment: PaymentMethod, refNo = ''): BookBody {
  return {
    vehicleType: vehicle,
    pickupBarangayId: pickup.barangayId,
    pickupDetails: pickup.details || pickup.label,
    pickupLat: pickup.lat,
    pickupLng: pickup.lng,
    dropoffBarangayId: dropoff.barangayId,
    dropoffDetails: dropoff.details || dropoff.label,
    dropoffLat: dropoff.lat,
    dropoffLng: dropoff.lng,
    paymentMethod: payment,
    paymentMethodOther: payment === 'Cash' ? undefined : (refNo.trim() || undefined),
  }
}

function toLocalInput(value: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}T${pad(value.getHours())}:${pad(value.getMinutes())}`
}

export type AccountPage = 'menu' | 'profile' | 'pin' | 'mobile' | 'delete' | 'terms' | 'privacy'

export function AccountHub({
  desk,
  page,
  onPage,
  onDesk,
  onLogout,
}: {
  desk: Desk
  page: AccountPage
  onPage: (page: AccountPage) => void
  onDesk: (desk: Desk) => void
  onLogout: () => void
}) {
  if (page === 'profile') return <ProfileForm desk={desk} onDesk={onDesk} onBack={() => onPage('menu')} />
  if (page === 'pin') return <PinForm desk={desk} onDesk={onDesk} onBack={() => onPage('menu')} />
  if (page === 'mobile') return <MobileForm desk={desk} onDesk={onDesk} onBack={() => onPage('menu')} />
  if (page === 'delete') return <DeleteForm desk={desk} onDesk={onDesk} onBack={() => onPage('menu')} />
  if (page === 'terms') return <Legal title="Terms and Condition" body={TERMS} onBack={() => onPage('menu')} />
  if (page === 'privacy') return <Legal title="Privacy Policy" body={PRIVACY} onBack={() => onPage('menu')} />

  return (
    <div className="page">
      <h2>Account</h2>
      <article className="card profile-card">
        <div className="avatar lg">{(desk.fullName || 'C').trim().charAt(0).toUpperCase()}</div>
        <div className="profile-details">
          <b>{desk.fullName}</b>
          <div className="muted">{desk.phoneNumber}</div>
          <div className="muted">{desk.email || 'No email yet'}</div>
        </div>
      </article>
      <p className="section-title">Account management</p>
      <button className="menu-row" type="button" onClick={() => onPage('profile')}>Profile</button>
      <button className="menu-row" type="button" onClick={() => onPage('pin')}>{desk.hasPin ? 'Change PIN' : 'Set PIN'}</button>
      <button className="menu-row" type="button" onClick={() => onPage('mobile')}>Change Mobile</button>
      <button className="menu-row danger-row" type="button" onClick={() => onPage('delete')}>Account Deletion</button>
      <button className="menu-row" type="button" onClick={onLogout}>Logout</button>
      <p className="section-title">Legal</p>
      <button className="menu-row" type="button" onClick={() => onPage('terms')}>Terms and Condition</button>
      <button className="menu-row" type="button" onClick={() => onPage('privacy')}>Privacy Policy</button>
    </div>
  )
}

function ProfileForm({ desk, onDesk, onBack }: { desk: Desk; onDesk: (desk: Desk) => void; onBack: () => void }) {
  const [firstName, setFirstName] = useState(desk.firstName)
  const [lastName, setLastName] = useState(desk.lastName)
  const [gender, setGender] = useState<Gender>(desk.gender ?? 'Male')
  const [email, setEmail] = useState(desk.email ?? '')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      onDesk(await api.updateProfile({ firstName, lastName, gender, email }))
      onBack()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="page account-form" onSubmit={submit}>
      <button className="ghost" type="button" onClick={onBack}>Back</button>
      <h2>Profile</h2>
      <label className="field"><span>FIRST NAME</span><input value={firstName} onChange={(e) => setFirstName(e.target.value)} /></label>
      <label className="field"><span>LAST NAME</span><input value={lastName} onChange={(e) => setLastName(e.target.value)} /></label>
      <label className="field">
        <span>GENDER</span>
        <select value={gender} onChange={(e) => setGender(e.target.value as Gender)}>
          <option value="Male">Male</option>
          <option value="Female">Female</option>
          <option value="Other">Other</option>
        </select>
      </label>
      <label className="field"><span>EMAIL</span><input type="email" value={email} onChange={(e) => setEmail(e.target.value)} /></label>
      {error && <p className="error">{error}</p>}
      <button className="primary" disabled={busy}>{busy ? 'Saving…' : 'Save profile'}</button>
    </form>
  )
}

function PinForm({ desk, onDesk, onBack }: { desk: Desk; onDesk: (desk: Desk) => void; onBack: () => void }) {
  const [currentPin, setCurrentPin] = useState('')
  const [pin, setPin] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      onDesk(await api.setPin(pin, desk.hasPin ? currentPin : undefined))
      onBack()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save PIN.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="page account-form" onSubmit={submit}>
      <button className="ghost" type="button" onClick={onBack}>Back</button>
      <h2>{desk.hasPin ? 'Change PIN' : 'Set PIN'}</h2>
      <p className="muted">Use 4 to 6 digits. This PIN protects account changes.</p>
      {desk.hasPin && <label className="field"><span>CURRENT PIN</span><input inputMode="numeric" value={currentPin} onChange={(e) => setCurrentPin(e.target.value)} /></label>}
      <label className="field"><span>NEW PIN</span><input inputMode="numeric" value={pin} onChange={(e) => setPin(e.target.value)} /></label>
      {error && <p className="error">{error}</p>}
      <button className="primary" disabled={busy}>{busy ? 'Saving…' : 'Save PIN'}</button>
    </form>
  )
}

function MobileForm({ desk, onDesk, onBack }: { desk: Desk; onDesk: (desk: Desk) => void; onBack: () => void }) {
  const [newPhone, setNewPhone] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      onDesk(await api.updateMobile(newPhone))
      onBack()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not change mobile.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="page account-form" onSubmit={submit}>
      <button className="ghost" type="button" onClick={onBack}>Back</button>
      <h2>Change Mobile</h2>
      <p className="muted">Current number: {desk.phoneNumber}</p>
      <label className="field"><span>NEW MOBILE</span><input value={newPhone} onChange={(e) => setNewPhone(e.target.value)} inputMode="tel" placeholder="09XX XXX XXXX" /></label>
      {error && <p className="error">{error}</p>}
      <button className="primary" disabled={busy}>{busy ? 'Saving…' : 'Save number'}</button>
    </form>
  )
}

function DeleteForm({ desk, onDesk, onBack }: { desk: Desk; onDesk: (desk: Desk) => void; onBack: () => void }) {
  const [reason, setReason] = useState('')
  const [pin, setPin] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      onDesk(await api.deleteAccount(reason, desk.hasPin ? pin : undefined))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not submit request.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="page account-form" onSubmit={submit}>
      <button className="ghost" type="button" onClick={onBack}>Back</button>
      <h2>Account Deletion</h2>
      {desk.deleteStatus === 'Pending' ? (
        <p>Your request is pending Super Admin review. You can still use the app until it is approved.</p>
      ) : (
        <>
          <p className="muted">This asks Super Admin to close the account. It is not instant.</p>
          <label className="field"><span>REASON</span><input value={reason} onChange={(e) => setReason(e.target.value)} /></label>
          {desk.hasPin && <label className="field"><span>PIN</span><input inputMode="numeric" value={pin} onChange={(e) => setPin(e.target.value)} /></label>}
          {error && <p className="error">{error}</p>}
          <button className="danger" disabled={busy}>{busy ? 'Submitting…' : 'Request deletion'}</button>
        </>
      )}
    </form>
  )
}

function Legal({ title, body, onBack }: { title: string; body: string; onBack: () => void }) {
  return (
    <div className="page">
      <button className="ghost" type="button" onClick={onBack}>Back</button>
      <h2>{title}</h2>
      {body.split('\n\n').map((para) => <p key={para.slice(0, 24)} className="legal">{para}</p>)}
    </div>
  )
}

const TERMS = `Ya! Pasakay is a ride-hailing platform that connects customers with motorcycle and tricycle riders operated by independent Operators.

By creating an account you confirm that the name, mobile number, and email you provide are yours, and that you will keep your Google account and PIN confidential.

Fares are quoted before you confirm a booking. Payment is collected according to the method you select (CASH, GCASH, MAYA, or OTHERS). The assigned rider must accept that method.

You may cancel a booking before the trip is ongoing. SOS alerts your Operator and Super Admin with your location during an active trip.

Scheduled bookings must be set at least 10 minutes in the future. Operators may assign or broadcast those jobs to riders in their service area.

Ya! Pasakay may suspend accounts that abuse SOS, skip payment, or provide false identity details.`

const PRIVACY = `We collect your name, gender, mobile number, email, booking locations, and trip history to operate the service.

Location is used to set pickup, find nearby riders, and send SOS. We do not sell your personal data.

Operators in your trip see the pickup, drop-off, and contact details needed to complete the ride. Super Admin can review account deletion requests and safety alerts.

You may request account deletion from Account Management. Super Admin reviews the request before the account is closed.

PINs are stored as irreversible hashes. Customers sign in with Google.`
