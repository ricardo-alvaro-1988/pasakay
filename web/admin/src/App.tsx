import { FormEvent, ReactNode, useCallback, useEffect, useMemo, useState } from 'react'
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  Cell,
  Line,
  LineChart,
  Pie,
  PieChart,
  PolarAngleAxis,
  RadialBar,
  RadialBarChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import {
  api,
  BarangayOption,
  clearAuth,
  CustomerListItem,
  CustomerDetail,
  getToken,
  IdName,
  Me,
  OperatorArea,
  OperatorDetail,
  OperatorListItem,
  Overview,
  PageId,
  RideDetail,
  RideCommissionBreakdown,
  RideChatMessage,
  RideListItem,
  RideQuery,
  TerritoryListItem,
  OperatorFareMatrix,
  OperatorFareListItem,
  DeriveFareZoneDetail,
  DeriveFareZoneListItem,
  DeriveFareRates,
  DeriveFareLatLng,
  BillingOperator,
  BillingOperatorDetail,
  OperatorBill,
  BillStatus,
  Announcement,
  FareRates,
  FareSurcharge,
  RiderDetail,
  RiderListItem,
  RiderRides,
  saveAuth,
  SearchHit,
  SuggestItem,
  SupportInbox,
  SupportKind,
  SupportStatus,
  SupportTicket,
  SupportTicketDetail,
  AuditAction,
  AuditLog,
  AccessGroup,
  AccessPage,
  AccessStaff,
  TripStatus,
  VehicleType,
  PaymentMethod,
  PAYMENT_METHODS,
  normalizePaymentMethod,
  paymentMethodCssKey,
  paymentMethodLabelUpper,
  parsePaymentMethodInput,
  normalizeWalletKind,
  normalizeWalletStatus,
  WalletTransaction,
  WalletTransactionKind,
  WalletHistoryItem,
  WalletRequest,
  RiderWalletDetail,
  OperatorWalletOverview,
  OperatorCashInBank,
  OperatorCashInDestinations,
  OperatorPromoItem,
  OperatorOverview,
  OperatorNavAlerts,
  OperatorInboxItem,
  OperatorFleet,
  OperatorBookingBoard,
  OperatorBookingListItem,
  ScheduledBooking,
  CommissionReportItem,
  CommissionReportResponse,
  RiderReportItem,
  RiderReportResponse,
  RiderInviteLink,
  RiderApplicationListItem,
  RiderApplicationDetail,
  SurchargeKind,
  fleetDuty,
  fleetDutyLabel,
  FleetDuty,
} from './api'
import { readSidebarCollapsed, readTheme, setSidebarCollapsed, setTheme, Theme } from './theme'
import { useOpsAlerts } from './use-ops-alerts'
import { SOS_ALERT_EVENT, type OpsAlert } from './ops-hub'
import {
  armSosAudio,
  isSosAlarmPending,
  isSosAlarmPlaying,
  isSosAudioArmed,
  playSosAlarm,
  stopSosAlarm,
  subscribeSosAlarm,
} from './sos-alert'
import logoCircle from './asset/logo-circle.png'
import FleetMap from './FleetMap'
import TripLiveMap from './TripLiveMap'
import { DeriveZoneMap } from './DeriveZoneMap'
import {
  ADMIN_MENU_GROUPS,
  OPERATOR_MENU_GROUPS,
  SideNav,
  filterMenuGroups,
  flattenMenuGroups,
} from './side-nav'
import { BrandingSettingsPage } from './BrandingSettings'
import {
  applyBrand,
  DEFAULT_BRAND_NAME,
  type BrandingConfig,
} from './brand-themes'

const MENUS = flattenMenuGroups(ADMIN_MENU_GROUPS)
const OPERATOR_MENUS = flattenMenuGroups(OPERATOR_MENU_GROUPS)

const COMING_SOON: Record<string, string> = {}

type SettingsSection = 'general' | 'branding'

export default function App() {
  const [me, setMe] = useState<Me | null>(null)
  const [booting, setBooting] = useState(!!getToken())
  const [branding, setBranding] = useState<BrandingConfig | null>(null)
  const [hash, setHash] = useState(() => window.location.hash)

  useEffect(() => {
    const onHash = () => setHash(window.location.hash)
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [])

  useEffect(() => {
    api
      .publicBranding()
      .then((data) => {
        applyBrand(data, { titleSuffix: ' Admin' })
        setBranding(data)
      })
      .catch(() => {
        /* keep bundled defaults */
      })
  }, [])

  useEffect(() => {
    if (!getToken()) {
      return
    }
    api
      .me()
      .then((user) => {
        if (user.role !== 'Admin' && user.role !== 'Operator') {
          clearAuth()
          return
        }
        setMe(user)
      })
      .catch(() => clearAuth())
      .finally(() => setBooting(false))
  }, [])

  const brandName = branding?.brandName || DEFAULT_BRAND_NAME
  const brandLogo = branding?.logoUrl || logoCircle
  const publicRoute = parsePublicRiderHash(hash)

  if (publicRoute?.kind === 'join') {
    return <PublicRiderJoinPage token={publicRoute.token} brandName={brandName} brandLogo={brandLogo} />
  }
  if (publicRoute?.kind === 'status') {
    return <PublicRiderStatusPage brandName={brandName} brandLogo={brandLogo} />
  }

  if (booting) {
    return (
      <div className="login">
        <div className="login-card">
          <img className="brand-mark" src={brandLogo} alt={brandName} />
          Loading {brandName}…
        </div>
      </div>
    )
  }

  if (!me) {
    return <Login onSignedIn={setMe} brandName={brandName} brandLogo={brandLogo} />
  }

  if (me.role === 'Operator') {
    const tab = parseCommissionHash(hash)
    if (tab?.mode === 'operator') {
      return (
        <CommissionBookingTab
          mode="operator"
          operatorId={tab.operatorId}
          bookingId={tab.bookingId}
          onClose={() => {
            window.location.hash = ''
          }}
        />
      )
    }
    return (
      <OperatorShell
        me={me}
        onLogout={() => { clearAuth(); setMe(null) }}
        brandName={brandName}
        brandLogo={brandLogo}
      />
    )
  }

  const adminTab = parseCommissionHash(hash)
  if (adminTab?.mode === 'admin') {
    return (
      <CommissionBookingTab
        mode="admin"
        operatorId={adminTab.operatorId}
        bookingId={adminTab.bookingId}
        onClose={() => {
          window.location.hash = ''
        }}
      />
    )
  }

  return (
    <Shell
      me={me}
      onLogout={() => { clearAuth(); setMe(null) }}
      branding={branding}
      onBranding={setBranding}
      brandName={brandName}
      brandLogo={brandLogo}
    />
  )
}

function parsePublicRiderHash(rawHash = window.location.hash) {
  const raw = rawHash.replace(/^#\/?/, '').replace(/^\//, '')
  const join = /^rider-join\/([^/?#]+)/.exec(raw)
  if (join) {
    return { kind: 'join' as const, token: decodeURIComponent(join[1]) }
  }
  if (raw === 'rider-status' || raw.startsWith('rider-status?') || raw.startsWith('rider-status/')) {
    return { kind: 'status' as const }
  }
  return null
}

function parseCommissionHash(rawHash = window.location.hash) {
  const raw = rawHash.replace(/^#\/?/, '').replace(/^\//, '')
  const match = /^commission\/(admin|operator)\/([^/]+)\/([^/]+)$/.exec(raw)
  if (!match) {
    return null
  }
  return {
    mode: match[1] as 'admin' | 'operator',
    operatorId: match[2],
    bookingId: match[3],
  }
}

function CommissionBookingTab({
  mode,
  operatorId,
  bookingId,
  onClose,
}: {
  mode: 'admin' | 'operator'
  operatorId: string
  bookingId: string
  onClose: () => void
}) {
  return (
    <div className="shell" style={{ display: 'block' }}>
      <main className="main" style={{ padding: 24 }}>
        <BookingDetailPage
          loadKey={bookingId}
          load={() => mode === 'admin'
            ? api.adminOperatorBooking(operatorId, bookingId)
            : api.operatorBooking(bookingId)}
          onBack={() => {
            if (window.opener) {
              window.close()
              return
            }
            onClose()
            window.location.hash = ''
            window.location.reload()
          }}
          backLabel="Close"
          allowReassign={mode === 'operator'}
        />
      </main>
    </div>
  )
}

function Login({
  onSignedIn,
  brandName,
  brandLogo,
}: {
  onSignedIn: (me: Me) => void
  brandName: string
  brandLogo: string
}) {
  const [phone, setPhone] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      const auth = await api.login(phone, password)
      if (auth.user.role !== 'Admin' && auth.user.role !== 'Operator') {
        throw new Error('This portal is for operators and administrators.')
      }
      saveAuth(auth)
      onSignedIn(auth.user)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sign in failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="login">
      <form className="login-card" onSubmit={submit}>
        <img className="brand-mark login-logo" src={brandLogo} alt={brandName} />
        <h1>{brandName}</h1>
        <p>Sign in with your phone and password.</p>
        <label className="field">
          <span>Phone</span>
          <input value={phone} onChange={(e) => setPhone(e.target.value)} autoComplete="tel" />
        </label>
        <label className="field">
          <span>Password</span>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
        </label>
        <p className="muted">Forgot password? Ask an administrator to reset it.</p>
        {error && <p className="error">{error}</p>}
        <button className="btn" type="submit" disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}

function SosBanner({
  count,
  flash,
  detail,
  onOpenSupport,
}: {
  count: number
  flash: OpsAlert | null
  detail: string
  onOpenSupport?: () => void
}) {
  const [hidden, setHidden] = useState(false)
  const [armed, setArmed] = useState(isSosAudioArmed)
  const [playing, setPlaying] = useState(isSosAlarmPlaying)
  const [pending, setPending] = useState(isSosAlarmPending)
  const flashKey = `${flash?.ticketId ?? ''}:${flash?.reference ?? ''}:${flash?.atUtc ?? ''}`

  useEffect(() => subscribeSosAlarm(() => {
    setArmed(isSosAudioArmed())
    setPlaying(isSosAlarmPlaying())
    setPending(isSosAlarmPending())
  }), [])

  useEffect(() => {
    if (flashKey !== '::') {
      setHidden(false)
      void playSosAlarm()
    }
  }, [flashKey])

  if (hidden || (count <= 0 && !flash)) {
    return null
  }

  function hide() {
    stopSosAlarm()
    setHidden(true)
  }

  async function enableAndPlay() {
    await armSosAudio()
    await playSosAlarm()
  }

  return (
    <div className="card sos-alert sos-alert-blink">
      <div className="panel-head">
        <div>
          <h2 style={{ margin: 0 }}>SOS alert</h2>
          <p className="muted" style={{ margin: '6px 0 0' }}>
            {detail}
            {flash?.reference ? ` Latest: ${flash.reference}.` : ''}
          </p>
          {!armed || pending || !playing ? (
            <p style={{ margin: '8px 0 0', color: '#b42318', fontWeight: 700 }}>
              Alarm sound is off. Click “Enable alarm sound” (browser requires a click).
            </p>
          ) : (
            <p style={{ margin: '8px 0 0', color: '#b42318', fontWeight: 700 }}>
              Alarm sounding…
            </p>
          )}
        </div>
        <div className="sos-alert-actions">
          <button className="btn tiny" type="button" onClick={() => void enableAndPlay()}>
            Enable alarm sound
          </button>
          <button className="btn tiny" type="button" onClick={stopSosAlarm}>Stop alarm</button>
          {onOpenSupport ? (
            <button className="btn tiny" type="button" onClick={onOpenSupport}>Open Support</button>
          ) : null}
          <button className="sos-close" type="button" onClick={hide} title="Hide notification" aria-label="Hide SOS notification">
            ×
          </button>
        </div>
      </div>
    </div>
  )
}

function SosSoundArmBanner() {
  const [armed, setArmed] = useState(isSosAudioArmed)

  useEffect(() => subscribeSosAlarm(() => setArmed(isSosAudioArmed())), [])

  if (armed) {
    return null
  }

  return (
    <div className="card" style={{ borderColor: '#f04438', marginBottom: 12 }}>
      <div className="panel-head">
        <div>
          <h2 style={{ margin: 0 }}>Enable SOS alarm sound</h2>
          <p className="muted" style={{ margin: '6px 0 0' }}>
            Browsers block sound until you click once. Enable this so SOS alerts ring automatically.
          </p>
        </div>
        <button className="btn" type="button" onClick={() => void armSosAudio()}>
          Enable alarm sound
        </button>
      </div>
    </div>
  )
}

function Shell({
  me,
  onLogout,
  branding,
  onBranding,
  brandName,
  brandLogo,
}: {
  me: Me
  onLogout: () => void
  branding: BrandingConfig | null
  onBranding: (brand: BrandingConfig) => void
  brandName: string
  brandLogo: string
}) {
  const allowedGroups = useMemo(() => {
    if (me.isMainAdmin) {
      return ADMIN_MENU_GROUPS
    }
    const pages = new Set(me.accessPages ?? [])
    return filterMenuGroups(
      ADMIN_MENU_GROUPS,
      new Set(
        flattenMenuGroups(ADMIN_MENU_GROUPS)
          .map((item) => item.id)
          .filter((id) =>
            (id === 'roles' || id === 'admins')
              ? pages.has(id)
              : pages.has(id),
          ),
      ),
    )
  }, [me.isMainAdmin, me.accessPages])
  const allowedMenus = useMemo(() => flattenMenuGroups(allowedGroups), [allowedGroups])
  const firstPage = allowedMenus[0]?.id ?? 'overview'
  const [page, setPage] = useState<PageId>(
    allowedMenus.some((item) => item.id === 'overview') ? 'overview' : firstPage,
  )
  const [operatorId, setOperatorId] = useState<string | null>(null)
  const [operatorView, setOperatorView] = useState<'list' | 'create' | 'detail' | 'edit' | 'bookings'>('list')
  const [customerId, setCustomerId] = useState<string | null>(null)
  const [collapsed, setCollapsed] = useState(readSidebarCollapsed)
  const [theme, setThemeState] = useState<Theme>(readTheme)
  const [settingsSection, setSettingsSection] = useState<SettingsSection>('general')
  const [sosAlerts, setSosAlerts] = useState(0)
  const [billingAlerts, setBillingAlerts] = useState(0)
  const [deleteAlerts, setDeleteAlerts] = useState(0)
  const [sosFlash, setSosFlash] = useState<OpsAlert | null>(null)
  const canSupport = allowedMenus.some((item) => item.id === 'support')
  const canBilling = allowedMenus.some((item) => item.id === 'billing')
  const canCustomers = allowedMenus.some((item) => item.id === 'customers')

  const allowedKey = allowedMenus.map((item) => item.id).join(',')

  const loadAlerts = useCallback(() => {
    if (!canSupport && !canBilling && !canCustomers) {
      return
    }
    api.adminAlerts()
      .then((data) => {
        setSosAlerts(Math.max(data.openSos ?? 0, data.unreadSosAlerts ?? 0))
        setBillingAlerts(data.pendingBilling ?? 0)
        setDeleteAlerts(data.pendingAccountDeletes ?? 0)
      })
      .catch(() => {
        setSosAlerts(0)
        setBillingAlerts(0)
        setDeleteAlerts(0)
      })
  }, [canSupport, canBilling, canCustomers])

  useEffect(() => {
    if (!allowedKey.split(',').includes(page)) {
      setPage(firstPage)
    }
  }, [page, firstPage, allowedKey])

  useEffect(() => {
    loadAlerts()
    const handle = window.setInterval(loadAlerts, 15000)
    return () => window.clearInterval(handle)
  }, [loadAlerts])

  useOpsAlerts(loadAlerts)

  useEffect(() => {
    function onSos(event: Event) {
      const detail = (event as CustomEvent<OpsAlert>).detail
      setSosFlash(detail ?? null)
    }
    window.addEventListener(SOS_ALERT_EVENT, onSos)
    return () => window.removeEventListener(SOS_ALERT_EVENT, onSos)
  }, [])

  function go(next: PageId, id?: string, view: 'list' | 'create' | 'detail' | 'edit' | 'bookings' = 'list') {
    if (next !== 'profile' && !allowedMenus.some((item) => item.id === next)) {
      return
    }
    setPage(next)
    setOperatorId(next === 'operators' ? id ?? null : null)
    setOperatorView(next === 'operators' ? (id ? (view === 'list' ? 'detail' : view) : view) : 'list')
    setCustomerId(next === 'customers' ? id ?? null : null)
  }

  function toggleTheme() {
    const next = theme === 'dark' ? 'light' : 'dark'
    setTheme(next)
    setThemeState(next)
  }

  function toggleSidebar() {
    const next = !collapsed
    setCollapsed(next)
    setSidebarCollapsed(next)
  }

  const title =
    page === 'operators' && operatorView === 'create'
      ? 'Create Operator'
      : page === 'operators' && operatorView === 'edit'
        ? 'Edit Operator'
        : page === 'operators' && operatorView === 'bookings'
          ? 'Booking'
        : page === 'operators' && operatorView === 'detail'
          ? 'Operator'
        : page === 'customers' && customerId
          ? 'Customer'
          : page === 'profile'
            ? 'Profile'
          : MENUS.find((m) => m.id === page)?.label ?? 'Overview'

  return (
    <div className={`shell${collapsed ? ' collapsed' : ''}`}>
      <aside className="sidebar">
        <div className="side-brand">
          <img className="brand-mark" src={brandLogo} alt={brandName} />
          <div className="side-copy">
            <strong>{brandName}</strong>
            <span>{me.isMainAdmin ? 'Administrator' : me.accessGroupName || 'Admin'}</span>
          </div>
        </div>
        <SideNav
          scope="admin"
          groups={allowedGroups}
          page={page}
          shellCollapsed={collapsed}
          onNavigate={(id) => go(id)}
          badge={(id) => {
            if (id === 'support') return <NavBadge count={sosAlerts} tone="sos" />
            if (id === 'billing') return <NavBadge count={billingAlerts} tone="billing" />
            if (id === 'customers') return <NavBadge count={deleteAlerts} tone="delete" />
            return null
          }}
        />
        <div className="side-foot">
          <button className="collapse-btn" type="button" onClick={toggleSidebar}>
            {collapsed ? '»' : '« Collapse'}
          </button>
          <button className="collapse-btn" type="button" onClick={onLogout}>
            {collapsed ? '⎋' : 'Log out'}
          </button>
        </div>
      </aside>
      <main className="main">
        <header className="top">
          <div>
            <h1>{title}</h1>
            <p>Platform control for motorcycle and tricycle Operators.</p>
          </div>
          {allowedMenus.some((item) => item.id === 'operators' || item.id === 'customers') ? (
          <GlobalSearch
            onPick={(hit) => {
              if (hit.kind === 'operator') {
                go('operators', hit.id, 'detail')
              } else {
                go('customers', hit.id)
              }
            }}
          />
          ) : <div />}
          <div className="who">
            <button className="icon-btn" type="button" onClick={toggleTheme} title="Toggle theme">
              {theme === 'dark' ? '☀' : '☾'}
            </button>
            <button className="who-link" type="button" onClick={() => go('profile')} title="Open profile">
              <div className="avatar">{me.fullName.slice(0, 1)}</div>
              <div>
                <strong>{me.fullName}</strong>
                <span>{me.phoneNumber}</span>
              </div>
            </button>
          </div>
        </header>
        <SosSoundArmBanner />
        {(sosAlerts > 0 || sosFlash) ? (
          <SosBanner
            count={sosAlerts}
            flash={sosFlash}
            detail={sosAlerts > 0 ? `${sosAlerts} open SOS.` : 'New SOS received.'}
            onOpenSupport={canSupport ? () => go('support') : undefined}
          />
        ) : null}
        {page === 'overview' && (
          <OverviewPage
            onOpenOperator={(id) => go('operators', id, 'detail')}
            onOpenCustomers={() => go('customers')}
          />
        )}
        {page === 'operators' && (
          <OperatorsPage
            view={operatorView}
            selectedId={operatorId}
            onList={() => go('operators')}
            onCreate={() => go('operators', undefined, 'create')}
            onOpen={(id) => go('operators', id, 'detail')}
            onEdit={(id) => go('operators', id, 'edit')}
            onBookings={(id) => go('operators', id, 'bookings')}
          />
        )}
        {page === 'customers' && (
          <CustomersPage
            selectedId={customerId}
            onList={() => go('customers')}
            onOpen={(id) => go('customers', id)}
          />
        )}
        {page === 'territories' && <TerritoriesPage />}
        {page === 'fares' && <FaresPage />}
        {page === 'billing' && <BillingPage />}
        {page === 'commission' && <CommissionReportPage mode="admin" />}
        {page === 'announcements' && <AnnouncementsPage />}
        {page === 'support' && <SupportPage />}
        {page === 'audit' && <AuditPage />}
        {page === 'roles' ? <RolesPage /> : null}
        {page === 'admins' ? <AdminUsersPage /> : null}
        {page === 'settings' && (
          <SettingsPage
            theme={theme}
            onTheme={toggleTheme}
            me={me}
            section={settingsSection}
            onSection={setSettingsSection}
            branding={branding}
            onBranding={onBranding}
          />
        )}
        {page === 'profile' ? <AdminProfilePage me={me} /> : null}
        {page !== 'profile' && !MENUS.find((m) => m.id === page)?.live && (
          <ComingSoon title={title} body={COMING_SOON[page]} />
        )}
      </main>
    </div>
  )
}

function StatusTag({ active }: { active: boolean }) {
  return <span className={`tag status ${active ? 'active' : 'inactive'}`}>{active ? 'Active' : 'Inactive'}</span>
}

function TripStatusTag({ status }: { status: TripStatus }) {
  const label = status === 'Completed' ? 'Complete' : status
  return <span className={`tag trip ${status.toLowerCase()}`}>{label}</span>
}

function PromoTag({
  isPromoSponsored,
  promoCode,
  discountPercent,
}: {
  isPromoSponsored?: boolean
  promoCode?: string | null
  discountPercent?: number | null
}) {
  if (!isPromoSponsored) return null
  const code = promoCode || (discountPercent != null ? `Save${discountPercent}` : null)
  if (!code) return <span className="tag promo">Promo</span>
  const pct = discountPercent != null ? discountPercent : null
  return (
    <span className="tag promo" title={pct != null ? `${pct}% off` : undefined}>
      {code}{pct != null ? ` · ${pct}%` : ''}
    </span>
  )
}

const TRIP_STATUS_FILTERS: { value: TripStatus | ''; label: string }[] = [
  { value: '', label: 'All' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Waiting', label: 'Waiting' },
  { value: 'Ongoing', label: 'Ongoing' },
  { value: 'Completed', label: 'Complete' },
  { value: 'Cancelled', label: 'Cancelled' },
]

function TripStatusFilter({
  value,
  onChange,
}: {
  value: TripStatus | ''
  onChange: (status: TripStatus | '') => void
}) {
  return (
    <div className="chips trip-status-filter">
      {TRIP_STATUS_FILTERS.map((item) => (
        <button
          key={item.value || 'all'}
          type="button"
          className={value === item.value ? 'on' : ''}
          onClick={() => onChange(item.value)}
        >
          {item.label}
        </button>
      ))}
    </div>
  )
}

function FleetDutyTag({
  status,
  isOnline,
  lastLocationAtUtc,
}: {
  status: TripStatus | null
  isOnline?: boolean
  lastLocationAtUtc?: string
}) {
  const duty = fleetDuty(status, isOnline, lastLocationAtUtc)
  return <span className={`tag trip ${duty}`}>{fleetDutyLabel(duty)}</span>
}

function peso(value: number) {
  return `₱${value.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

/** Drop barangay/city suffix historically appended after the Google place. */
function stopAddress(value: string | null | undefined) {
  const text = (value ?? '').trim()
  if (!text) return '—'
  const idx = text.search(/Philippines/i)
  if (idx >= 0) {
    return text.slice(0, idx + 'Philippines'.length).replace(/[,\s;]+$/, '')
  }
  const parts = text.split(',').map((p) => p.trim()).filter(Boolean)
  if (parts.length >= 4) {
    for (let take = 3; take >= 2; take -= 1) {
      if (parts.length <= take) continue
      const tail = parts.slice(-take)
      const head = parts.slice(0, -take)
      if (tail.every((part) => head.some((h) => h.toLowerCase() === part.toLowerCase()))) {
        return head.join(', ')
      }
    }
  }
  return text
}

function percent(value: number) {
  return `${value.toLocaleString('en-PH', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}%`
}

const PH_TZ = 'Asia/Manila'

/** API often emits UTC DateTimes without a Z (Unspecified). Treat those as UTC. */
function parseApiDate(value: string | Date): Date {
  if (value instanceof Date) {
    return value
  }
  const raw = String(value).trim()
  if (!raw) {
    return new Date(NaN)
  }
  if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) {
    return new Date(`${raw}T00:00:00+08:00`)
  }
  if (/[zZ]$|[+-]\d{2}:?\d{2}$/.test(raw)) {
    return new Date(raw)
  }
  return new Date(raw.includes('T') ? `${raw}Z` : `${raw}T00:00:00Z`)
}

function phDateTime(value: string | Date | null | undefined) {
  if (!value) {
    return '—'
  }
  return parseApiDate(value).toLocaleString('en-PH', {
    timeZone: PH_TZ,
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}

function phDate(value: string | Date | null | undefined) {
  if (!value) {
    return '—'
  }
  return parseApiDate(value).toLocaleDateString('en-PH', {
    timeZone: PH_TZ,
    dateStyle: 'medium',
  })
}

function chartDay(value: string) {
  return new Date(`${value}T00:00:00+08:00`).toLocaleDateString('en-PH', {
    timeZone: PH_TZ,
    month: 'short',
    day: 'numeric',
  })
}

function phClock(time: string | null | undefined) {
  if (!time) {
    return ''
  }
  const [hour, minute] = time.split(':').map(Number)
  if (!Number.isFinite(hour) || !Number.isFinite(minute)) {
    return time
  }
  const stamp = new Date(Date.UTC(2026, 0, 1, hour, minute))
  return stamp.toLocaleTimeString('en-PH', { timeZone: 'UTC', hour: 'numeric', minute: '2-digit' })
}

function surchargeLine(item: FareSurcharge) {
  if (item.kind === 'TimeWindow') {
    return `${phClock(item.windowStart)} – ${phClock(item.windowEnd)} daily`
  }
  return `${phDateTime(item.rangeStartUtc)} – ${phDateTime(item.rangeEndUtc)}`
}

type RelatedSurcharge = FareSurcharge & { vehicleType: VehicleType }

type FareTierDraft = {
  passengerCount: string
  baseFare: string
  perKm: string
  minimumFare: string
  includedKm: string
}

type FareDraft = {
  passengerTiers: FareTierDraft[]
  operatorCommissionPercent: string
  driverCommissionPercent: string
  isActive: boolean
}

function relatedSurcharges(data: OperatorFareMatrix): RelatedSurcharge[] {
  return [
    ...(data.motorcycle?.surcharges ?? []).map((item) => ({ ...item, vehicleType: 'Motorcycle' as const })),
    ...(data.tricycle?.surcharges ?? []).map((item) => ({ ...item, vehicleType: 'Tricycle' as const })),
  ]
}

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

function commissionSum(system: number, draft: FareDraft) {
  return roundPercent(system + Number(draft.operatorCommissionPercent || 0) + Number(draft.driverCommissionPercent || 0))
}

function defaultTierDraft(): FareTierDraft {
  return {
    passengerCount: '1',
    baseFare: '50',
    perKm: '12',
    minimumFare: '50',
    includedKm: '1',
  }
}

function tierDraftsFromRates(rates: FareRates | null, singlePassenger = false): FareTierDraft[] {
  const tiers = rates?.passengerTiers?.length
    ? rates.passengerTiers
    : rates
      ? [{
          passengerCount: 1,
          baseFare: rates.baseFare,
          perKm: rates.perKm,
          minimumFare: rates.minimumFare,
          includedKm: rates.includedKm,
        }]
      : null
  if (!tiers) {
    return [defaultTierDraft()]
  }
  const sorted = tiers
    .slice()
    .sort((a, b) => a.passengerCount - b.passengerCount)
  const picked = singlePassenger
    ? [sorted.find((tier) => tier.passengerCount === 1) ?? sorted[0]]
    : sorted
  return picked.map((tier) => ({
    passengerCount: singlePassenger ? '1' : String(tier.passengerCount),
    baseFare: String(tier.baseFare),
    perKm: String(tier.perKm),
    minimumFare: String(tier.minimumFare),
    includedKm: String(tier.includedKm),
  }))
}

function fareDraft(rates: FareRates | null, system = 10, singlePassenger = false): FareDraft {
  const operatorShare = rates?.operatorCommissionPercent ?? Math.min(20, Math.max(0, roundPercent(100 - system)))
  const driverShare = rates?.driverCommissionPercent ?? roundPercent(Math.max(0, 100 - system - operatorShare))
  return {
    passengerTiers: tierDraftsFromRates(rates, singlePassenger),
    operatorCommissionPercent: String(operatorShare),
    driverCommissionPercent: String(driverShare),
    isActive: rates?.isActive ?? true,
  }
}

function sameTiers(a: FareTierDraft[], b: FareTierDraft[]) {
  if (a.length !== b.length) return false
  return a.every((tier, i) =>
    tier.passengerCount === b[i].passengerCount
    && tier.baseFare === b[i].baseFare
    && tier.perKm === b[i].perKm
    && tier.minimumFare === b[i].minimumFare
    && tier.includedKm === b[i].includedKm)
}

function sameDraft(a: FareDraft, b: FareDraft) {
  return sameTiers(a.passengerTiers, b.passengerTiers) && a.isActive === b.isActive
}

function parseDraft(draft: FareDraft, singlePassenger = false) {
  let passengerTiers = draft.passengerTiers
    .map((tier) => ({
      passengerCount: Math.max(1, Math.floor(Number(tier.passengerCount) || 1)),
      baseFare: Number(tier.baseFare),
      perKm: Number(tier.perKm),
      minimumFare: Number(tier.minimumFare),
      includedKm: Number(tier.includedKm),
    }))
    .sort((a, b) => a.passengerCount - b.passengerCount)
  if (singlePassenger) {
    const primary = passengerTiers.find((tier) => tier.passengerCount === 1) ?? passengerTiers[0]
    passengerTiers = [{
      passengerCount: 1,
      baseFare: primary?.baseFare ?? 50,
      perKm: primary?.perKm ?? 12,
      minimumFare: primary?.minimumFare ?? 50,
      includedKm: primary?.includedKm ?? 1,
    }]
  }
  const primary = passengerTiers[0] ?? {
    passengerCount: 1,
    baseFare: 50,
    perKm: 12,
    minimumFare: 50,
    includedKm: 1,
  }
  return {
    baseFare: primary.baseFare,
    perKm: primary.perKm,
    minimumFare: primary.minimumFare,
    includedKm: primary.includedKm,
    operatorCommissionPercent: Number(draft.operatorCommissionPercent),
    driverCommissionPercent: Number(draft.driverCommissionPercent),
    isActive: draft.isActive,
    passengerTiers,
  }
}

function commissionRates(motorcycle: number, tricycle: number) {
  return (
    <div>
      <div>Motorcycle {percent(motorcycle)}</div>
      <div>Tricycle {percent(tricycle)}</div>
    </div>
  )
}

function VehicleTag({ type, count }: { type: string; count?: number }) {
  const mc = type === 'Motorcycle'
  return (
    <span className={`tag vehicle ${mc ? 'mc' : 'trike'}`}>
      {mc ? (
        <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
          <circle cx="6" cy="17" r="2.4" fill="currentColor" />
          <circle cx="18" cy="17" r="2.4" fill="currentColor" />
          <path d="M8 17h6.2l2-5H10L8.4 9H6" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      ) : (
        <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
          <circle cx="5.5" cy="17" r="2.2" fill="currentColor" />
          <circle cx="14" cy="17" r="2.2" fill="currentColor" />
          <circle cx="19.5" cy="17" r="2.2" fill="currentColor" />
          <path d="M7 17h5M12 17V8h6.5v9M12 8H8L6.5 12" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      )}
      {mc ? 'Motorcycle' : 'Tricycle'}
      {typeof count === 'number' ? <em>{count}</em> : null}
    </span>
  )
}

function paymentMethodLabel(method: unknown, other?: string | null) {
  const normalized = normalizePaymentMethod(method)
  if (!normalized) return '—'
  if (normalized === 'Other' && other?.trim()) return other.trim()
  return paymentMethodLabelUpper(normalized)
}

function PaymentMethodTag({ method, other }: { method: unknown; other?: string | null }) {
  const normalized = normalizePaymentMethod(method)
  if (!normalized) return null
  return (
    <span className={`tag payment payment-${paymentMethodCssKey(normalized)}`}>
      {paymentMethodLabel(normalized, other)}
    </span>
  )
}

function PaymentMethodPicker({
  value,
  onChange,
}: {
  value: PaymentMethod[]
  onChange: (next: PaymentMethod[]) => void
}) {
  function toggle(method: PaymentMethod) {
    onChange(value.includes(method) ? value.filter((item) => item !== method) : [...value, method])
  }

  return (
    <div className="field wide">
      <span>Payment methods accepted</span>
      <p className="muted">How this rider receives fare payment from customers.</p>
      <div className="chips" style={{ marginTop: 8 }}>
        {PAYMENT_METHODS.map((method) => (
          <button key={method} type="button" className={value.includes(method) ? 'on' : ''} onClick={() => toggle(method)}>
            {paymentMethodLabelUpper(method)}
          </button>
        ))}
      </div>
    </div>
  )
}

function walletKindLabel(kind: unknown, walletView: 'operator' | 'rider' = 'operator') {
  switch (normalizeWalletKind(kind)) {
    case 'CashIn': return 'Cash in'
    case 'CashOut': return 'Cash out'
    case 'Commission': return walletView === 'operator' ? 'System' : 'System'
    default: return '—'
  }
}

function walletKindClass(kind: unknown) {
  switch (normalizeWalletKind(kind)) {
    case 'CashIn': return 'wallet-cash-in'
    case 'CashOut': return 'wallet-cash-out'
    case 'Commission': return 'wallet-commission'
    default: return ''
  }
}

function walletStatusClass(status: unknown) {
  switch (normalizeWalletStatus(status)) {
    case 'Pending': return 'pending'
    case 'Approved': return 'completed'
    case 'Rejected': return 'cancelled'
    default: return ''
  }
}

function NavBadge({ count, tone }: { count: number; tone?: 'sos' | 'billing' | 'wallet' | 'delete' }) {
  if (count <= 0) return null
  return <span className={`nav-badge${tone ? ` ${tone}` : ''}`}>{count > 99 ? '99+' : count}</span>
}

function walletStatusLabel(status: unknown) {
  return normalizeWalletStatus(status) ?? '—'
}

function WalletKindTag({ kind }: { kind: unknown }) {
  const normalized = normalizeWalletKind(kind)
  if (!normalized) return null
  return <span className={`tag wallet-kind ${walletKindClass(normalized)}`}>{walletKindLabel(normalized)}</span>
}

function WalletTransactionsTable({
  rows,
  onApprove,
  onReject,
  showRider = false,
  onOpenRider,
  walletView = 'operator',
}: {
  rows: Array<WalletTransaction & Partial<Pick<WalletHistoryItem, 'riderId' | 'riderName' | 'riderPhone' | 'plateNumber'>>>
  onApprove?: (id: string) => void
  onReject?: (id: string) => void
  showRider?: boolean
  onOpenRider?: (riderId: string) => void
  walletView?: 'operator' | 'rider'
}) {
  if (rows.length === 0) {
    return <p className="muted">No wallet transactions yet.</p>
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>When</th>
            {showRider ? <th>Rider</th> : null}
            <th>Type</th>
            <th>Payment</th>
            <th>Amount</th>
            {walletView === 'operator' ? (
              <>
                <th>Admin</th>
                <th>Operator</th>
              </>
            ) : null}
            <th>Balance</th>
            <th>Status</th>
            <th>Details</th>
            {onApprove ? <th /> : null}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const kind = normalizeWalletKind(row.kind)
            const isDebit = kind === 'CashOut' || kind === 'Commission'
            const showSplit = walletView === 'operator' && kind === 'Commission'
            return (
            <tr key={row.id}>
              <td>{phDateTime(row.createdAtUtc)}</td>
              {showRider ? (
                <td>
                  {row.riderName ? (
                    onOpenRider && row.riderId ? (
                      <button type="button" className="linkish" onClick={() => onOpenRider(row.riderId!)}>
                        <strong>{row.riderName}</strong>
                      </button>
                    ) : (
                      <strong>{row.riderName}</strong>
                    )
                  ) : '—'}
                  {row.plateNumber ? <div><small>{row.plateNumber}{row.riderPhone ? ` · ${row.riderPhone}` : ''}</small></div> : null}
                </td>
              ) : null}
              <td><WalletKindTag kind={row.kind} /></td>
              <td>{row.paymentMethod ? <PaymentMethodTag method={row.paymentMethod} /> : '—'}</td>
              <td className={isDebit ? 'wallet-debit' : 'wallet-credit'}>{isDebit ? `−${peso(row.amount)}` : peso(row.amount)}</td>
              {walletView === 'operator' ? (
                <>
                  <td className={showSplit ? 'wallet-debit' : undefined}>
                    {showSplit && row.adminAmount != null ? `−${peso(row.adminAmount)}` : '—'}
                  </td>
                  <td className={showSplit ? 'wallet-debit' : undefined}>
                    {showSplit && row.operatorAmount != null ? `−${peso(row.operatorAmount)}` : '—'}
                  </td>
                </>
              ) : null}
              <td>{row.balanceAfter != null ? peso(row.balanceAfter) : '—'}</td>
              <td><span className={`tag status ${walletStatusClass(row.status)}`}>{walletStatusLabel(row.status)}</span></td>
              <td>
                {row.tripReference ? <small>Booking {row.tripReference}</small> : null}
                {row.note ? <div><small>{row.note}</small></div> : null}
                {row.rejectionReason ? <div><small className="error">{row.rejectionReason}</small></div> : null}
                {row.resolvedAtUtc && normalizeWalletStatus(row.status) !== 'Pending' ? (
                  <div><small className="muted">Resolved {phDateTime(row.resolvedAtUtc)}</small></div>
                ) : null}
              </td>
              {onApprove ? (
                <td>
                  {normalizeWalletStatus(row.status) === 'Pending' && kind !== 'Commission' ? (
                    <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                      <button className="btn tiny" type="button" onClick={() => onApprove(row.id)}>Approve</button>
                      <button className="btn tiny danger" type="button" onClick={() => onReject?.(row.id)}>Reject</button>
                    </div>
                  ) : null}
                </td>
              ) : null}
            </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

function RiderWalletPanel({ riderId }: { riderId: string; acceptedMethods?: PaymentMethod[] }) {
  const [wallet, setWallet] = useState<RiderWalletDetail | null>(null)
  const [kindFilter, setKindFilter] = useState<WalletTransactionKind | ''>('')
  const [historyPage, setHistoryPage] = useState(1)
  const [historyRows, setHistoryRows] = useState<WalletHistoryItem[]>([])
  const [historyTotal, setHistoryTotal] = useState(0)
  const [historyError, setHistoryError] = useState('')
  const [requestKind, setRequestKind] = useState<'CashIn' | 'CashOut' | null>(null)
  const [amount, setAmount] = useState('')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Cash')
  const [note, setNote] = useState('')
  const [approved, setApproved] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const historyPageSize = 10

  const walletMethods = PAYMENT_METHODS

  function loadWallet() {
    api.operatorRiderWallet(riderId).then(setWallet).catch((err: Error) => setError(err.message))
  }

  function loadHistory() {
    api.operatorWalletHistory({ riderId, kind: kindFilter, page: historyPage, pageSize: historyPageSize })
      .then((data) => {
        setHistoryRows(data.items)
        setHistoryTotal(data.total)
        setHistoryError('')
      })
      .catch((err: Error) => setHistoryError(err.message))
  }

  function reload() {
    loadWallet()
    loadHistory()
  }

  function openRequest(kind: 'CashIn' | 'CashOut') {
    setRequestKind(kind)
    setAmount('')
    setNote('')
    setApproved(true)
    setError('')
    setNotice('')
    if (walletMethods.length > 0) setPaymentMethod(walletMethods[0])
  }

  function closeRequest() {
    if (busy) return
    setRequestKind(null)
  }

  useEffect(() => {
    loadWallet()
  }, [riderId])

  useEffect(() => {
    loadHistory()
  }, [riderId, kindFilter, historyPage])

  useEffect(() => {
    if (walletMethods.length > 0 && !walletMethods.includes(paymentMethod)) {
      setPaymentMethod(walletMethods[0])
    }
  }, [walletMethods, paymentMethod])

  async function submitRequest(event: FormEvent) {
    event.preventDefault()
    if (!requestKind) return
    setError('')
    setNotice('')
    const value = Number(amount)
    if (!value || value <= 0) {
      setError('Enter an amount greater than zero.')
      return
    }
    setBusy(true)
    try {
      const body = { amount: value, paymentMethod, note: note.trim() || undefined, approved }
      if (requestKind === 'CashIn') {
        await api.operatorRiderCashIn(riderId, body)
      } else {
        await api.operatorRiderCashOut(riderId, body)
      }
      const label = requestKind === 'CashIn' ? 'Cash-in' : 'Cash-out'
      setNotice(approved ? `${label} recorded as approved.` : `${label} request submitted for later approval.`)
      setRequestKind(null)
      reload()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not submit wallet request.')
    } finally {
      setBusy(false)
    }
  }

  async function approve(id: string) {
    setError('')
    try {
      await api.approveWalletRequest(id)
      setNotice('Wallet request approved.')
      reload()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not approve request.')
    }
  }

  async function reject(id: string) {
    const reason = window.prompt('Rejection reason (optional)') ?? ''
    setError('')
    try {
      await api.rejectWalletRequest(id, reason.trim() || undefined)
      setNotice('Wallet request rejected.')
      reload()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not reject request.')
    }
  }

  if (!wallet) return error ? <p className="error">{error}</p> : <p>Loading wallet…</p>

  return (
    <div className="detail-card" style={{ marginTop: 16 }}>
      <div className="panel-head" style={{ marginBottom: 12 }}>
        <div>
          <span>{wallet.riderName} · wallet</span>
          <p className="muted" style={{ marginTop: 6 }}>Cash in, cash out, and system deductions for this rider.</p>
        </div>
        <div className="wallet-balance-box">
          <span>Wallet balance</span>
          <strong className={`wallet-balance${wallet.balance < 0 ? ' negative' : ''}`}>{peso(wallet.balance)}</strong>
        </div>
      </div>
      {error && !requestKind ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      {wallet.pendingCount > 0 ? <p className="muted">{wallet.pendingCount} pending request(s)</p> : null}
      <div className="wallet-actions">
        <button type="button" className="btn" onClick={() => openRequest('CashIn')} disabled={walletMethods.length === 0}>
          Cash in
        </button>
        <button type="button" className="btn ghost" onClick={() => openRequest('CashOut')} disabled={walletMethods.length === 0}>
          Cash out
        </button>
      </div>
      <div style={{ marginBottom: 12 }}>
        <h3 style={{ margin: '0 0 8px' }}>Transaction history</h3>
        <div className="chips">
          {(['', 'CashIn', 'CashOut', 'Commission'] as const).map((value) => (
            <button
              key={value || 'all'}
              type="button"
              className={kindFilter === value ? 'on' : ''}
              onClick={() => { setKindFilter(value); setHistoryPage(1) }}
            >
              {value === '' ? 'All' : walletKindLabel(value)}
            </button>
          ))}
        </div>
      </div>
      {historyError ? <p className="error">{historyError}</p> : null}
      <WalletTransactionsTable rows={historyRows} onApprove={approve} onReject={reject} />
      <Pager page={historyPage} pageSize={historyPageSize} total={historyTotal} onPage={setHistoryPage} />
      {requestKind ? (
        <div className="modal-backdrop" role="presentation" onClick={closeRequest}>
          <form
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby="wallet-request-title"
            onClick={(e) => e.stopPropagation()}
            onSubmit={(event) => void submitRequest(event)}
          >
            <div className="modal-head">
              <div>
                <h2 id="wallet-request-title">{requestKind === 'CashIn' ? 'Cash in' : 'Cash out'}</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  Record this manually for {wallet.riderName}. Check This is approved to apply it to the wallet now.
                </p>
              </div>
              <button className="btn tiny" type="button" onClick={closeRequest} disabled={busy}>Close</button>
            </div>
            <div className="form-grid">
              <label className="field">
                <span>Amount</span>
                <input value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="0.00" autoFocus />
              </label>
              <label className="field">
                <span>Payment method</span>
                <PaymentMethodSuggest
                  value={paymentMethod}
                  onChange={setPaymentMethod}
                  options={walletMethods}
                  disabled={walletMethods.length === 0}
                  placeholder="Type CASH, GCASH, MAYA, or OTHERS"
                />
              </label>
              <label className="field wide">
                <span>Note</span>
                <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Reference number or note" />
              </label>
              <div className="field wide">
                <label className="check">
                  <input type="checkbox" checked={approved} onChange={(e) => setApproved(e.target.checked)} />
                  <span>This is approved</span>
                </label>
              </div>
            </div>
            {error ? <p className="error">{error}</p> : null}
            <div className="modal-actions">
              <button className="btn" type="submit" disabled={busy || walletMethods.length === 0}>
                {busy
                  ? 'Saving…'
                  : approved
                    ? requestKind === 'CashIn' ? 'Record approved cash in' : 'Record approved cash out'
                    : 'Submit for later approval'}
              </button>
              <button className="btn ghost" type="button" disabled={busy} onClick={closeRequest}>Cancel</button>
            </div>
          </form>
        </div>
      ) : null}
    </div>
  )
}

function WalletHistorySection({
  riderId,
  onOpenRider,
}: {
  riderId?: string
  onOpenRider?: (riderId: string) => void
}) {
  const [q, setQ] = useState('')
  const [kind, setKind] = useState<WalletTransactionKind | ''>('')
  const [page, setPage] = useState(1)
  const [rows, setRows] = useState<WalletHistoryItem[]>([])
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 20

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.operatorWalletHistory({ q, kind, riderId, page, pageSize })
        .then((data) => { setRows(data.items); setTotal(data.total); setError('') })
        .catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, kind, riderId, page])

  return (
    <div className="card" style={{ marginTop: 16 }}>
      <div className="panel-head">
        <div>
          <h2 style={{ margin: 0 }}>Wallet history</h2>
          <p className="muted">Cash in, cash out, and system deductions across your fleet.</p>
        </div>
      </div>
      <div className="toolbar" style={{ marginBottom: 12 }}>
        <div className="ac">
          <input
            value={q}
            placeholder="Search rider, booking, or note"
            onChange={(e) => { setQ(e.target.value); setPage(1) }}
          />
        </div>
        <div className="chips">
          {(['', 'CashIn', 'CashOut', 'Commission'] as const).map((value) => (
            <button
              key={value || 'all'}
              type="button"
              className={kind === value ? 'on' : ''}
              onClick={() => { setKind(value); setPage(1) }}
            >
              {value === '' ? 'All' : walletKindLabel(value)}
            </button>
          ))}
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <WalletTransactionsTable rows={rows} showRider={!riderId} onOpenRider={onOpenRider} />
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function OperatorWalletPage() {
  const [overview, setOverview] = useState<OperatorWalletOverview | null>(null)
  const [requests, setRequests] = useState<WalletRequest[]>([])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [selectedRiderId, setSelectedRiderId] = useState<string | null>(null)

  function load() {
    Promise.all([api.operatorWalletOverview(), api.operatorWalletRequests()])
      .then(([nextOverview, nextRequests]) => {
        setOverview(nextOverview)
        setRequests(nextRequests)
        setError('')
      })
      .catch((err: Error) => setError(err.message))
  }

  useEffect(() => {
    load()
    const handle = window.setInterval(load, 15000)
    return () => window.clearInterval(handle)
  }, [])

  async function approve(id: string) {
    setError('')
    try {
      await api.approveWalletRequest(id)
      setNotice('Request approved.')
      load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not approve request.')
    }
  }

  async function reject(id: string) {
    const reason = window.prompt('Rejection reason (optional)') ?? ''
    setError('')
    try {
      await api.rejectWalletRequest(id, reason.trim() || undefined)
      setNotice('Request rejected.')
      load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not reject request.')
    }
  }

  if (selectedRiderId) {
    return (
      <div>
        <button className="btn tiny" type="button" onClick={() => setSelectedRiderId(null)} style={{ marginBottom: 12 }}>
          Back to wallet overview
        </button>
        <RiderWalletPanel
          riderId={selectedRiderId}
          acceptedMethods={['Cash', 'GCash', 'Maya']}
        />
      </div>
    )
  }

  return (
    <div className="wallet-page">
      <div className="card">
        <div className="panel-head">
          <div>
            <h2 style={{ margin: 0 }}>Rider wallets</h2>
            <p className="muted">Fleet wallet balances and pending cash-in or cash-out requests.</p>
          </div>
        </div>
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}
        <div className="stats" style={{ marginBottom: 16 }}>
          <div className="card tone-completed">
            <label>Total balance</label>
            <strong>{peso(overview?.totalBalance ?? 0)}</strong>
          </div>
          <Stat label="Pending requests" value={overview?.pendingRequests ?? requests.length} tone="pending" />
          <Stat label="Riders" value={overview?.riders.length ?? 0} tone="waiting" />
        </div>
        {!overview ? <p>Loading wallet balances…</p> : overview.riders.length === 0 ? (
          <p className="muted">No riders in your fleet yet.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Rider</th>
                  <th>Vehicle</th>
                  <th>Status</th>
                  <th>Balance</th>
                  <th>Pending</th>
                </tr>
              </thead>
              <tbody>
                {overview.riders.map((row) => (
                  <tr key={row.riderId} className="clickable" onClick={() => setSelectedRiderId(row.riderId)}>
                    <td>
                      <strong>{row.riderName}</strong>
                      <div><small>{row.plateNumber} · {row.riderPhone}</small></div>
                    </td>
                    <td><VehicleTag type={row.vehicleType} /></td>
                    <td><StatusTag active={row.isActive} /></td>
                    <td><strong className={row.balance < 0 ? 'error' : ''}>{peso(row.balance)}</strong></td>
                    <td>{row.pendingCount > 0 ? <span className="tag status pending">{row.pendingCount}</span> : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <WalletHistorySection onOpenRider={setSelectedRiderId} />

      <div className="card" style={{ marginTop: 16 }}>
        <div className="panel-head">
          <div>
            <h2 style={{ margin: 0 }}>Pending requests</h2>
            <p className="muted">Approve or reject rider cash-in and cash-out requests.</p>
          </div>
          <span className="tag status pending">{requests.length} pending</span>
        </div>
        {requests.length === 0 ? (
          <p className="muted">No pending wallet requests.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>When</th>
                  <th>Rider</th>
                  <th>Type</th>
                  <th>Payment</th>
                  <th>Amount</th>
                  <th>Note</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {requests.map((row) => (
                  <tr key={row.id}>
                    <td>{phDateTime(row.createdAtUtc)}</td>
                    <td>
                      <strong>{row.riderName}</strong>
                      <div><small>{row.plateNumber} · {row.riderPhone}</small></div>
                    </td>
                    <td>{walletKindLabel(row.kind)}</td>
                    <td><PaymentMethodTag method={row.paymentMethod} /></td>
                    <td>{peso(row.amount)}</td>
                    <td>{row.note || '—'}</td>
                    <td>
                      <div style={{ display: 'flex', gap: 6 }}>
                        <button className="btn tiny" type="button" onClick={() => void approve(row.id)}>Approve</button>
                        <button className="btn tiny danger" type="button" onClick={() => void reject(row.id)}>Reject</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <OperatorCashInSettings />
    </div>
  )
}

function OperatorCashInSettings() {
  const [data, setData] = useState<OperatorCashInDestinations | null>(null)
  const [gCashNumber, setGCashNumber] = useState('')
  const [mayaNumber, setMayaNumber] = useState('')
  const [gCashIsActive, setGCashIsActive] = useState(true)
  const [mayaIsActive, setMayaIsActive] = useState(true)
  const [gCashQr, setGCashQr] = useState<File | null>(null)
  const [mayaQr, setMayaQr] = useState<File | null>(null)
  const [clearGCashQr, setClearGCashQr] = useState(false)
  const [clearMayaQr, setClearMayaQr] = useState(false)
  const [bankName, setBankName] = useState('')
  const [accountName, setAccountName] = useState('')
  const [accountNumber, setAccountNumber] = useState('')
  const [bankIsActive, setBankIsActive] = useState(true)
  const [bankQr, setBankQr] = useState<File | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editBankName, setEditBankName] = useState('')
  const [editAccountName, setEditAccountName] = useState('')
  const [editAccountNumber, setEditAccountNumber] = useState('')
  const [editBankIsActive, setEditBankIsActive] = useState(true)
  const [editBankQr, setEditBankQr] = useState<File | null>(null)
  const [clearEditQr, setClearEditQr] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  function apply(next: OperatorCashInDestinations) {
    setData(next)
    setGCashNumber(next.gCashNumber ?? '')
    setMayaNumber(next.mayaNumber ?? '')
    setGCashIsActive(next.gCashIsActive !== false)
    setMayaIsActive(next.mayaIsActive !== false)
  }

  useEffect(() => {
    api.operatorCashInDestinations()
      .then((next) => {
        apply(next)
        setError('')
      })
      .catch((err: Error) => setError(err.message))
  }, [])

  async function saveEwallets(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const body = new FormData()
      body.append('gCashNumber', gCashNumber.trim())
      body.append('mayaNumber', mayaNumber.trim())
      body.append('gCashIsActive', gCashIsActive ? 'true' : 'false')
      body.append('mayaIsActive', mayaIsActive ? 'true' : 'false')
      if (clearGCashQr) body.append('clearGCashQr', 'true')
      if (clearMayaQr) body.append('clearMayaQr', 'true')
      if (gCashQr) body.append('gCashQr', gCashQr)
      if (mayaQr) body.append('mayaQr', mayaQr)
      apply(await api.saveOperatorCashInEwallets(body))
      setGCashQr(null)
      setMayaQr(null)
      setClearGCashQr(false)
      setClearMayaQr(false)
      setNotice('GCash and Maya cash-in details saved. Only Active methods appear for riders.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save e-wallet details.')
    } finally {
      setBusy(false)
    }
  }

  async function addBank(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const body = new FormData()
      body.append('bankName', bankName.trim())
      body.append('accountName', accountName.trim())
      body.append('accountNumber', accountNumber.trim())
      body.append('isActive', bankIsActive ? 'true' : 'false')
      if (bankQr) body.append('qr', bankQr)
      apply(await api.addOperatorCashInBank(body))
      setBankName('')
      setAccountName('')
      setAccountNumber('')
      setBankIsActive(true)
      setBankQr(null)
      setNotice('Bank account added for rider cash-in.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not add bank account.')
    } finally {
      setBusy(false)
    }
  }

  function startEdit(row: OperatorCashInBank) {
    setEditingId(row.id)
    setEditBankName(row.bankName)
    setEditAccountName(row.accountName)
    setEditAccountNumber(row.accountNumber)
    setEditBankIsActive(row.isActive !== false)
    setEditBankQr(null)
    setClearEditQr(false)
  }

  async function saveBank(e: FormEvent) {
    e.preventDefault()
    if (!editingId) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const body = new FormData()
      body.append('bankName', editBankName.trim())
      body.append('accountName', editAccountName.trim())
      body.append('accountNumber', editAccountNumber.trim())
      body.append('isActive', editBankIsActive ? 'true' : 'false')
      if (clearEditQr) body.append('clearQr', 'true')
      if (editBankQr) body.append('qr', editBankQr)
      apply(await api.updateOperatorCashInBank(editingId, body))
      setEditingId(null)
      setNotice('Bank account updated.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not update bank account.')
    } finally {
      setBusy(false)
    }
  }

  async function setBankActive(row: OperatorCashInBank, nextActive: boolean) {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const body = new FormData()
      body.append('bankName', row.bankName)
      body.append('accountName', row.accountName)
      body.append('accountNumber', row.accountNumber)
      body.append('isActive', nextActive ? 'true' : 'false')
      apply(await api.updateOperatorCashInBank(row.id, body))
      setNotice(nextActive ? 'Bank set to Active — riders can see it.' : 'Bank set to Inactive — hidden from riders.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not update bank status.')
    } finally {
      setBusy(false)
    }
  }

  async function removeBank(id: string) {
    if (!window.confirm('Remove this bank account from rider cash-in?')) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      apply(await api.deleteOperatorCashInBank(id))
      if (editingId === id) setEditingId(null)
      setNotice('Bank account removed.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not remove bank account.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card" style={{ marginTop: 16 }}>
      <div className="panel-head">
        <div>
          <h2 style={{ margin: 0 }}>Cash-in QR &amp; accounts</h2>
          <p className="muted">Riders only see methods marked Active. Inactive stays saved here but is hidden in the rider app.</p>
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      {!data ? <p>Loading cash-in settings…</p> : (
        <>
          <form onSubmit={saveEwallets}>
            <h3>GCash</h3>
            <div className="form-grid">
              <label className="field">
                <span>GCash number</span>
                <input value={gCashNumber} onChange={(e) => setGCashNumber(e.target.value)} placeholder="09XXXXXXXXX" />
              </label>
              <label className="field">
                <span>Status</span>
                <select value={gCashIsActive ? 'active' : 'inactive'} onChange={(e) => setGCashIsActive(e.target.value === 'active')}>
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                </select>
              </label>
              <label className="field">
                <span>GCash QR</span>
                <input type="file" accept="image/*" onChange={(e) => { setGCashQr(e.target.files?.[0] ?? null); setClearGCashQr(false) }} />
              </label>
            </div>
            {data.gCashQrUrl && !clearGCashQr ? (
              <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginTop: 8 }}>
                <PhotoThumb src={data.gCashQrUrl} alt="GCash QR" className="id-preview" />
                <button className="btn tiny danger" type="button" onClick={() => setClearGCashQr(true)}>Remove QR</button>
              </div>
            ) : null}
            <h3 style={{ marginTop: 20 }}>Maya</h3>
            <div className="form-grid">
              <label className="field">
                <span>Maya number</span>
                <input value={mayaNumber} onChange={(e) => setMayaNumber(e.target.value)} placeholder="09XXXXXXXXX" />
              </label>
              <label className="field">
                <span>Status</span>
                <select value={mayaIsActive ? 'active' : 'inactive'} onChange={(e) => setMayaIsActive(e.target.value === 'active')}>
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                </select>
              </label>
              <label className="field">
                <span>Maya QR</span>
                <input type="file" accept="image/*" onChange={(e) => { setMayaQr(e.target.files?.[0] ?? null); setClearMayaQr(false) }} />
              </label>
            </div>
            {data.mayaQrUrl && !clearMayaQr ? (
              <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginTop: 8 }}>
                <PhotoThumb src={data.mayaQrUrl} alt="Maya QR" className="id-preview" />
                <button className="btn tiny danger" type="button" onClick={() => setClearMayaQr(true)}>Remove QR</button>
              </div>
            ) : null}
            <div style={{ marginTop: 14, maxWidth: 220 }}>
              <button className="btn" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save GCash & Maya'}</button>
            </div>
          </form>

          <h3 style={{ marginTop: 28 }}>Custom bank accounts</h3>
          <p className="muted" style={{ marginTop: 0 }}>Add one or more bank transfer options for riders. Inactive banks are hidden from riders.</p>
          {data.banks.length === 0 ? <p className="muted">No bank accounts yet.</p> : (
            <div className="table-wrap" style={{ marginBottom: 16 }}>
              <table>
                <thead>
                  <tr>
                    <th>Bank</th>
                    <th>Account name</th>
                    <th>Account no.</th>
                    <th>Status</th>
                    <th>QR</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {data.banks.map((row) => (
                    <tr key={row.id}>
                      <td><strong>{row.bankName}</strong></td>
                      <td>{row.accountName}</td>
                      <td>{row.accountNumber}</td>
                      <td>
                        <span className={row.isActive !== false ? 'ok' : 'muted'}>
                          {row.isActive !== false ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td>{row.qrUrl ? <PhotoThumb src={row.qrUrl} alt={`${row.bankName} QR`} /> : '—'}</td>
                      <td>
                        <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                          <button
                            className="btn tiny"
                            type="button"
                            disabled={busy}
                            onClick={() => void setBankActive(row, row.isActive === false)}
                          >
                            {row.isActive !== false ? 'Set inactive' : 'Set active'}
                          </button>
                          <button className="btn tiny" type="button" onClick={() => startEdit(row)}>Edit</button>
                          <button className="btn tiny danger" type="button" disabled={busy} onClick={() => void removeBank(row.id)}>Remove</button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {editingId ? (
            <form onSubmit={saveBank} style={{ marginBottom: 20 }}>
              <h3>Edit bank account</h3>
              <div className="form-grid">
                <label className="field"><span>Bank name</span><input value={editBankName} onChange={(e) => setEditBankName(e.target.value)} required /></label>
                <label className="field"><span>Account name</span><input value={editAccountName} onChange={(e) => setEditAccountName(e.target.value)} required /></label>
                <label className="field"><span>Account number</span><input value={editAccountNumber} onChange={(e) => setEditAccountNumber(e.target.value)} required /></label>
                <label className="field">
                  <span>Status</span>
                  <select value={editBankIsActive ? 'active' : 'inactive'} onChange={(e) => setEditBankIsActive(e.target.value === 'active')}>
                    <option value="active">Active</option>
                    <option value="inactive">Inactive</option>
                  </select>
                </label>
                <label className="field">
                  <span>Bank QR</span>
                  <input type="file" accept="image/*" onChange={(e) => { setEditBankQr(e.target.files?.[0] ?? null); setClearEditQr(false) }} />
                </label>
              </div>
              {data.banks.find((b) => b.id === editingId)?.qrUrl && !clearEditQr ? (
                <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginTop: 8 }}>
                  <PhotoThumb src={data.banks.find((b) => b.id === editingId)!.qrUrl} alt="Bank QR" className="id-preview" />
                  <button className="btn tiny danger" type="button" onClick={() => setClearEditQr(true)}>Remove QR</button>
                </div>
              ) : null}
              <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
                <button className="btn" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save bank'}</button>
                <button className="btn tiny" type="button" onClick={() => setEditingId(null)}>Cancel</button>
              </div>
            </form>
          ) : null}

          <form onSubmit={addBank}>
            <h3>Add bank account</h3>
            <div className="form-grid">
              <label className="field"><span>Bank name</span><input value={bankName} onChange={(e) => setBankName(e.target.value)} required /></label>
              <label className="field"><span>Account name</span><input value={accountName} onChange={(e) => setAccountName(e.target.value)} required /></label>
              <label className="field"><span>Account number</span><input value={accountNumber} onChange={(e) => setAccountNumber(e.target.value)} required /></label>
              <label className="field">
                <span>Status</span>
                <select value={bankIsActive ? 'active' : 'inactive'} onChange={(e) => setBankIsActive(e.target.value === 'active')}>
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                </select>
              </label>
              <label className="field">
                <span>Bank QR</span>
                <input type="file" accept="image/*" onChange={(e) => setBankQr(e.target.files?.[0] ?? null)} />
              </label>
            </div>
            <div style={{ marginTop: 14, maxWidth: 220 }}>
              <button className="btn" type="submit" disabled={busy}>{busy ? 'Adding…' : 'Add bank'}</button>
            </div>
          </form>
        </>
      )}
    </div>
  )
}

function Pager({
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
      <span>{from}–{to} of {total}</span>
      <div className="pager-btns">
        <button type="button" className="btn tiny" disabled={page <= 1} onClick={() => onPage(page - 1)}>Prev</button>
        <span>Page {page} / {pages}</span>
        <button type="button" className="btn tiny" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</button>
      </div>
    </div>
  )
}

function initialOf(name: string) {
  return (name.trim()[0] ?? '?').toUpperCase()
}

function Avatar({ name, photoUrl, size = 36 }: { name: string; photoUrl?: string | null; size?: number }) {
  if (photoUrl) {
    return <img className="ac-avatar" src={photoUrl} alt="" style={{ width: size, height: size }} />
  }
  return (
    <span className="ac-avatar ac-initial" style={{ width: size, height: size, fontSize: size * 0.4 }}>
      {initialOf(name)}
    </span>
  )
}

function PhotoThumb({
  src,
  alt,
  className = 'id-preview tiny',
}: {
  src?: string | null
  alt: string
  className?: string
}) {
  const [open, setOpen] = useState(false)
  if (!src) {
    return <span className="muted">No photo</span>
  }

  return (
    <>
      <button
        type="button"
        className="photo-link"
        onClick={(e) => {
          e.stopPropagation()
          setOpen(true)
        }}
      >
        <img className={className} src={src} alt={alt} />
      </button>
      {open && (
        <div className="lightbox" onClick={() => setOpen(false)} role="presentation">
          <img src={src} alt={alt} />
        </div>
      )}
    </>
  )
}

function ClickableAvatar({
  name,
  photoUrl,
  size = 36,
}: {
  name: string
  photoUrl?: string | null
  size?: number
}) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button
        type="button"
        className="photo-link"
        title={photoUrl ? 'View profile photo' : undefined}
        onClick={(e) => {
          e.stopPropagation()
          if (photoUrl) {
            setOpen(true)
          }
        }}
      >
        <Avatar name={name} photoUrl={photoUrl} size={size} />
      </button>
      {open && photoUrl && (
        <div className="lightbox" onClick={() => setOpen(false)} role="presentation">
          <img src={photoUrl} alt={name} />
        </div>
      )}
    </>
  )
}

function PersonSuggest({
  value,
  onChange,
  items,
  placeholder,
  onPick,
}: {
  value: string
  onChange: (value: string) => void
  items: SuggestItem[]
  placeholder: string
  onPick: (item: SuggestItem) => void
}) {
  const [open, setOpen] = useState(false)
  const filtered = items.filter((item) => {
    const q = value.trim().toLowerCase()
    if (!q) {
      return true
    }
    return (
      item.name.toLowerCase().includes(q) ||
      item.phone.includes(q) ||
      (item.extra ?? '').toLowerCase().includes(q)
    )
  })

  return (
    <div className="ac">
      <input
        value={value}
        placeholder={placeholder}
        autoComplete="off"
        onChange={(e) => {
          onChange(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => window.setTimeout(() => setOpen(false), 160)}
      />
      {open && (
        <div className="suggest">
          {filtered.length === 0 ? (
            <div className="suggest-empty">No matches</div>
          ) : (
            filtered.map((item) => (
              <button
                key={item.id}
                type="button"
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  onPick(item)
                  setOpen(false)
                }}
              >
                <Avatar name={item.name} photoUrl={item.photoUrl} />
                <span className="ac-text">
                  <span className="suggest-name">{item.name}</span>
                  <small>{item.extra ? `${item.phone} · ${item.extra}` : item.phone}</small>
                </span>
                {item.vehicleType ? <VehicleTag type={item.vehicleType} /> : null}
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}

function LookupSuggest({
  query,
  onQuery,
  items,
  placeholder,
  disabled,
  onPick,
  filterQuery,
  extraFor,
  onBlur,
}: {
  query: string
  onQuery: (value: string) => void
  items: IdName[]
  placeholder: string
  disabled?: boolean
  onPick: (item: IdName) => void
  filterQuery?: string
  extraFor?: (item: IdName) => string | undefined
  onBlur?: () => void
}) {
  const [open, setOpen] = useState(false)
  const q = (filterQuery ?? query).trim().toLowerCase()
  const filtered = items
    .filter((item) => !q || item.name.toLowerCase().includes(q) || (extraFor?.(item) ?? '').toLowerCase().includes(q))
    .sort((a, b) => {
      if (!q) {
        return a.name.localeCompare(b.name)
      }
      const aStarts = a.name.toLowerCase().startsWith(q)
      const bStarts = b.name.toLowerCase().startsWith(q)
      if (aStarts !== bStarts) {
        return aStarts ? -1 : 1
      }
      return a.name.localeCompare(b.name)
    })

  return (
    <div className="ac">
      <input
        value={query}
        placeholder={placeholder}
        disabled={disabled}
        autoComplete="off"
        onChange={(e) => {
          onQuery(e.target.value)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => window.setTimeout(() => {
          setOpen(false)
          onBlur?.()
        }, 160)}
      />
      {open && !disabled && (
        <div className="suggest">
          {filtered.length === 0 ? (
            <div className="suggest-empty">No matches</div>
          ) : (
            filtered.map((item) => {
              const extra = extraFor?.(item)
              return (
                <button
                  key={item.id}
                  type="button"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => {
                    onPick(item)
                    setOpen(false)
                  }}
                >
                  <span className="ac-text">
                    <span className="suggest-name">{item.name}</span>
                    {extra ? <small>{extra}</small> : null}
                  </span>
                </button>
              )
            })
          )}
        </div>
      )}
    </div>
  )
}

function PaymentMethodSuggest({
  value,
  onChange,
  options,
  disabled,
  placeholder = 'Type CASH, GCASH, MAYA, or OTHERS',
}: {
  value: PaymentMethod | ''
  onChange: (method: PaymentMethod) => void
  options: PaymentMethod[]
  disabled?: boolean
  placeholder?: string
}) {
  const [query, setQuery] = useState(value ? paymentMethodLabelUpper(value) : '')

  useEffect(() => {
    setQuery(value ? paymentMethodLabelUpper(value) : '')
  }, [value])

  const items = options.map((method) => ({
    id: method,
    name: paymentMethodLabelUpper(method),
  }))
  const selectedLabel = value ? paymentMethodLabelUpper(value) : ''
  const filterQuery = query.trim().toLowerCase() === selectedLabel.toLowerCase() ? '' : query

  return (
    <LookupSuggest
      query={query}
      filterQuery={filterQuery}
      onQuery={(text) => {
        setQuery(text)
        const parsed = parsePaymentMethodInput(text)
        if (parsed && options.includes(parsed)) {
          onChange(parsed)
        }
      }}
      items={items}
      placeholder={placeholder}
      disabled={disabled}
      onPick={(item) => {
        onChange(item.id as PaymentMethod)
        setQuery(item.name)
      }}
    />
  )
}

function GovernmentIdTypeField({
  value,
  onChange,
}: {
  value: string
  onChange: (value: string) => void
}) {
  const [types, setTypes] = useState<IdName[]>([])
  const [query, setQuery] = useState(value)

  useEffect(() => {
    api.governmentIdTypes()
      .then((items) => setTypes(items.map((name) => ({ id: name, name }))))
      .catch(() => setTypes([]))
  }, [])

  useEffect(() => {
    setQuery(value)
  }, [value])

  return (
    <LookupSuggest
      query={query}
      onQuery={(next) => {
        setQuery(next)
        if (value && next !== value) {
          onChange('')
        }
      }}
      items={types}
      placeholder="Driver's License, Passport…"
      onPick={(item) => {
        setQuery(item.name)
        onChange(item.name)
      }}
    />
  )
}

type AddressValue = {
  province: IdName | null
  municipality: IdName | null
  barangay: IdName | null
  details: string
}

const emptyAddress: AddressValue = { province: null, municipality: null, barangay: null, details: '' }

function AddressPicker({
  value,
  onChange,
  loadProvinces,
  loadMunicipalities,
  loadBarangays,
}: {
  value: AddressValue
  onChange: (value: AddressValue) => void
  loadProvinces?: () => Promise<IdName[]>
  loadMunicipalities?: (provinceId: string) => Promise<IdName[]>
  loadBarangays?: (municipalityId: string) => Promise<BarangayOption[]>
}) {
  const fetchProvinces = loadProvinces ?? (() => api.provinces())
  const fetchMunicipalities = loadMunicipalities ?? ((id: string) => api.municipalities(id))
  const fetchBarangays = loadBarangays ?? ((id: string) => api.barangays(id))
  const [provinces, setProvinces] = useState<IdName[]>([])
  const [municipalities, setMunicipalities] = useState<IdName[]>([])
  const [barangays, setBarangays] = useState<IdName[]>([])
  const [provinceQuery, setProvinceQuery] = useState(value.province?.name ?? '')
  const [municipalityQuery, setMunicipalityQuery] = useState(value.municipality?.name ?? '')
  const [barangayQuery, setBarangayQuery] = useState(value.barangay?.name ?? '')

  useEffect(() => {
    fetchProvinces().then(setProvinces).catch(() => setProvinces([]))
  }, [])

  useEffect(() => {
    setProvinceQuery(value.province?.name ?? '')
  }, [value.province?.id, value.province?.name])

  useEffect(() => {
    setMunicipalityQuery(value.municipality?.name ?? '')
  }, [value.municipality?.id, value.municipality?.name])

  useEffect(() => {
    setBarangayQuery(value.barangay?.name ?? '')
  }, [value.barangay?.id, value.barangay?.name])

  useEffect(() => {
    if (!value.province) {
      setMunicipalities([])
      return
    }
    fetchMunicipalities(value.province.id).then(setMunicipalities).catch(() => setMunicipalities([]))
  }, [value.province])

  useEffect(() => {
    if (!value.municipality) {
      setBarangays([])
      return
    }
    fetchBarangays(value.municipality.id)
      .then((items) => setBarangays(items.map((item) => ({ id: item.id, name: item.name }))))
      .catch(() => setBarangays([]))
  }, [value.municipality])

  return (
    <div className="address-picker">
      <div className="address-lookups">
        <label className="field">
          <span>Province</span>
          <LookupSuggest
            query={provinceQuery}
            onQuery={(query) => {
              setProvinceQuery(query)
              if (value.province && query !== value.province.name) {
                onChange({ province: null, municipality: null, barangay: null, details: value.details })
              }
            }}
            items={provinces}
            placeholder="Select province"
            onPick={(item) => {
              setProvinceQuery(item.name)
              onChange({ province: item, municipality: null, barangay: null, details: value.details })
            }}
          />
        </label>
        <label className="field">
          <span>Municipality / city</span>
          <LookupSuggest
            query={municipalityQuery}
            onQuery={(query) => {
              setMunicipalityQuery(query)
              if (value.municipality && query !== value.municipality.name) {
                onChange({ ...value, municipality: null, barangay: null })
              }
            }}
            items={municipalities}
            placeholder={value.province ? 'Select municipality' : 'Choose a province first'}
            disabled={!value.province}
            onPick={(item) => {
              setMunicipalityQuery(item.name)
              onChange({ ...value, municipality: item, barangay: null })
            }}
          />
        </label>
        <label className="field">
          <span>Barangay</span>
          <LookupSuggest
            query={barangayQuery}
            onQuery={(query) => {
              setBarangayQuery(query)
              if (value.barangay && query !== value.barangay.name) {
                onChange({ ...value, barangay: null })
              }
            }}
            items={barangays}
            placeholder={value.municipality ? 'Select barangay' : 'Choose a municipality first'}
            disabled={!value.municipality}
            onPick={(item) => {
              setBarangayQuery(item.name)
              onChange({ ...value, barangay: item })
            }}
          />
        </label>
      </div>
      <label className="field">
        <span>Specific details</span>
        <textarea
          rows={3}
          placeholder="Street, building, unit, or landmark"
          value={value.details}
          onChange={(e) => onChange({ ...value, details: e.target.value })}
        />
      </label>
    </div>
  )
}

function groupAreasByMunicipality(areas: OperatorArea[]) {
  const groups = new Map<string, { province: string; municipality: string; items: OperatorArea[] }>()
  for (const area of areas) {
    const key = `${area.province}|${area.municipality}`
    const group = groups.get(key) ?? { province: area.province, municipality: area.municipality, items: [] }
    group.items.push(area)
    groups.set(key, group)
  }
  return [...groups.values()].sort((a, b) =>
    a.province.localeCompare(b.province) || a.municipality.localeCompare(b.municipality),
  )
}

function groupAreasByProvince(areas: OperatorArea[]) {
  const provinces = new Map<string, Map<string, OperatorArea[]>>()
  for (const area of areas) {
    const municipals = provinces.get(area.province) ?? new Map<string, OperatorArea[]>()
    const items = municipals.get(area.municipality) ?? []
    items.push(area)
    municipals.set(area.municipality, items)
    provinces.set(area.province, municipals)
  }
  return [...provinces.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([province, municipals]) => ({
      province,
      municipalities: [...municipals.entries()]
        .sort(([a], [b]) => a.localeCompare(b))
        .map(([municipality, items]) => ({
          municipality,
          items: [...items].sort((a, b) => a.barangay.localeCompare(b.barangay)),
        })),
    }))
}

function AreaGroups({ areas }: { areas: OperatorArea[] }) {
  const provinces = groupAreasByProvince(areas)
  if (provinces.length === 0) {
    return <p>—</p>
  }
  return (
    <div className="area-groups">
      {provinces.map((province) => (
        <section key={province.province} className="area-province-group">
          <h4 className="area-province-head">{province.province}</h4>
          {province.municipalities.map((group) => (
            <div key={`${province.province}|${group.municipality}`} className="area-group">
              <div className="area-group-head">
                <strong>{group.municipality}</strong>
                <em>{group.items.length}</em>
              </div>
              <p className="area-group-names">{group.items.map((item) => item.barangay).join(', ')}</p>
            </div>
          ))}
        </section>
      ))}
    </div>
  )
}

function AreaPicker({
  assigned,
  onChange,
}: {
  assigned: OperatorArea[]
  onChange: (areas: OperatorArea[]) => void
}) {
  const [provinces, setProvinces] = useState<IdName[]>([])
  const [municipalities, setMunicipalities] = useState<IdName[]>([])
  const [barangays, setBarangays] = useState<BarangayOption[]>([])
  const [provinceQuery, setProvinceQuery] = useState('')
  const [municipalityQuery, setMunicipalityQuery] = useState('')
  const [province, setProvince] = useState<IdName | null>(null)
  const [municipality, setMunicipality] = useState<IdName | null>(null)
  const [leftSel, setLeftSel] = useState<string[]>([])
  const [rightSel, setRightSel] = useState<string[]>([])
  const [barangayFilter, setBarangayFilter] = useState('')

  useEffect(() => {
    api.provinces().then(setProvinces).catch(() => setProvinces([]))
  }, [])

  useEffect(() => {
    if (!province) {
      setMunicipalities([])
      return
    }
    api.municipalities(province.id).then(setMunicipalities).catch(() => setMunicipalities([]))
  }, [province])

  useEffect(() => {
    if (!municipality) {
      setBarangays([])
      setLeftSel([])
      return
    }
    api.barangays(municipality.id).then(setBarangays).catch(() => setBarangays([]))
    setLeftSel([])
    setBarangayFilter('')
  }, [municipality])

  const assignedIds = new Set(assigned.map((area) => area.barangayId))
  const remaining = barangays.filter((item) => !assignedIds.has(item.id))
  const filter = barangayFilter.trim().toLowerCase()
  const available = remaining.filter((item) => !filter || item.name.toLowerCase().includes(filter))
  const assignedGroups = groupAreasByMunicipality(assigned)

  function toggle(list: string[], id: string, set: (next: string[]) => void) {
    set(list.includes(id) ? list.filter((item) => item !== id) : [...list, id])
  }

  function toArea(item: BarangayOption): OperatorArea {
    return {
      barangayId: item.id,
      barangay: item.name,
      municipality: item.municipality,
      province: item.province,
    }
  }

  function addSelected() {
    onChange([...assigned, ...available.filter((item) => leftSel.includes(item.id)).map(toArea)])
    setLeftSel([])
  }

  function addAll() {
    onChange([...assigned, ...remaining.map(toArea)])
    setLeftSel([])
  }

  function removeSelected() {
    onChange(assigned.filter((area) => !rightSel.includes(area.barangayId)))
    setRightSel([])
  }

  function removeAll() {
    onChange([])
    setRightSel([])
  }

  return (
    <div className="area-picker">
      <div className="area-lookups">
        <label className="field">
          <span>Province</span>
          <LookupSuggest
            query={provinceQuery}
            onQuery={(value) => {
              setProvinceQuery(value)
              if (province && value !== province.name) {
                setProvince(null)
                setMunicipality(null)
                setMunicipalityQuery('')
              }
            }}
            items={provinces}
            placeholder="Select province"
            onPick={(item) => {
              setProvince(item)
              setProvinceQuery(item.name)
              setMunicipality(null)
              setMunicipalityQuery('')
            }}
          />
        </label>
        <label className="field">
          <span>Municipality / city</span>
          <LookupSuggest
            query={municipalityQuery}
            onQuery={(value) => {
              setMunicipalityQuery(value)
              if (municipality && value !== municipality.name) {
                setMunicipality(null)
              }
            }}
            items={municipalities}
            placeholder={province ? 'Select municipality' : 'Choose a province first'}
            disabled={!province}
            onPick={(item) => {
              setMunicipality(item)
              setMunicipalityQuery(item.name)
            }}
          />
        </label>
      </div>
      <p className="area-hint">Move barangays to the right. An Operator can cover any barangay in the Philippines, including more than one city.</p>
      <div className="shuttle">
        <div className="shuttle-col">
          <h4>Available barangays{municipality ? ` (${remaining.length})` : ''}</h4>
          <input
            className="area-brgy-filter"
            value={barangayFilter}
            placeholder={municipality ? 'Search barangay in this city' : 'Choose a city first'}
            disabled={!municipality}
            onChange={(e) => setBarangayFilter(e.target.value)}
          />
          <div className="box-list">
            {available.length === 0 ? (
              <div className="suggest-empty">
                {!municipality
                  ? 'Select a municipality first.'
                  : remaining.length === 0
                    ? 'No barangays left in this city.'
                    : 'No barangay matches that search.'}
              </div>
            ) : available.map((item) => (
              <button
                key={item.id}
                type="button"
                className={leftSel.includes(item.id) ? 'on' : ''}
                onClick={() => toggle(leftSel, item.id, setLeftSel)}
              >
                {item.name}
              </button>
            ))}
          </div>
        </div>
        <div className="shuttle-btns">
          <button className="btn tiny" type="button" onClick={addSelected} disabled={leftSel.length === 0}>Add →</button>
          <button className="btn tiny" type="button" onClick={addAll} disabled={remaining.length === 0}>Add all →</button>
          <button className="btn tiny" type="button" onClick={removeSelected} disabled={rightSel.length === 0}>← Remove</button>
          <button className="btn tiny" type="button" onClick={removeAll} disabled={assigned.length === 0}>← Remove all</button>
        </div>
        <div className="shuttle-col">
          <h4>Area of operation ({assignedGroups.length} {assignedGroups.length === 1 ? 'city' : 'cities'}, {assigned.length} barangays)</h4>
          <div className="box-list">
            {assigned.length === 0 ? (
              <div className="suggest-empty">No barangays assigned yet.</div>
            ) : assignedGroups.map((group) => {
              const ids = group.items.map((item) => item.barangayId)
              const allOn = ids.every((id) => rightSel.includes(id))
              return (
                <div key={`${group.province}|${group.municipality}`} className="area-box-group">
                  <button
                    type="button"
                    className={`group-head ${allOn ? 'on' : ''}`}
                    onClick={() => {
                      setRightSel(allOn
                        ? rightSel.filter((id) => !ids.includes(id))
                        : [...new Set([...rightSel, ...ids])])
                    }}
                  >
                    <span className="suggest-name">{group.municipality}</span>
                    <small>{group.province} · {group.items.length} barangays</small>
                  </button>
                  {group.items.map((item) => (
                    <button
                      key={item.barangayId}
                      type="button"
                      className={`nested ${rightSel.includes(item.barangayId) ? 'on' : ''}`}
                      onClick={() => toggle(rightSel, item.barangayId, setRightSel)}
                    >
                      {item.barangay}
                    </button>
                  ))}
                </div>
              )
            })}
          </div>
        </div>
      </div>
    </div>
  )
}

function GlobalSearch({ onPick }: { onPick: (hit: SearchHit) => void }) {
  const [q, setQ] = useState('')
  const [hits, setHits] = useState<SearchHit[]>([])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.search(q).then(setHits).catch(() => setHits([]))
    }, 150)
    return () => window.clearTimeout(handle)
  }, [q])

  return (
    <PersonSuggest
      value={q}
      onChange={setQ}
      placeholder="Search operators or customers"
      items={hits.map((hit) => ({ id: `${hit.kind}-${hit.id}`, name: hit.name, phone: hit.phone, photoUrl: hit.photoUrl }))}
      onPick={(item) => {
        const hit = hits.find((row) => `${row.kind}-${row.id}` === item.id)
        if (hit) {
          onPick(hit)
        }
        setQ('')
      }}
    />
  )
}

function OverviewPage({
  onOpenOperator,
  onOpenCustomers,
}: {
  onOpenOperator: (id: string) => void
  onOpenCustomers: () => void
}) {
  const [range, setRange] = useState<'weekly' | 'monthly' | 'yearly'>('weekly')
  const [data, setData] = useState<Overview | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    api.overview(range).then(setData).catch((err: Error) => setError(err.message))
  }, [range])

  const pie = useMemo(
    () => [
      { name: 'Motorcycle', value: data?.ridersMotorcycle ?? 0, color: '#e30613' },
      { name: 'Tricycle', value: data?.ridersTricycle ?? 0, color: '#8be34a' },
    ],
    [data],
  )

  if (error) {
    return <p className="error">{error}</p>
  }
  if (!data) {
    return <p>Loading overview…</p>
  }

  const emptyPie = data.riders === 0

  return (
    <>
      {(data.pendingAccountDeletes ?? 0) > 0 ? (
        <div className="card delete-alert" style={{ marginBottom: 16, borderLeft: '4px solid #d48b00' }}>
          <div className="panel-head">
            <div>
              <h2 style={{ margin: 0 }}>Account deletion requested</h2>
              <p className="muted" style={{ margin: '6px 0 0' }}>
                {data.pendingAccountDeletes} customer{data.pendingAccountDeletes === 1 ? '' : 's'} asked to delete their account.
              </p>
            </div>
            <button className="btn tiny" type="button" onClick={onOpenCustomers}>Review customers</button>
          </div>
        </div>
      ) : null}
      <section className="stats">
        <Stat label="Operators" value={data.operators} />
        <div className="card">
          <label>Riders</label>
          <strong>{data.riders}</strong>
          <div className="tag-row">
            <VehicleTag type="Motorcycle" count={data.ridersMotorcycle} />
            <VehicleTag type="Tricycle" count={data.ridersTricycle} />
          </div>
        </div>
        <Stat label="Customers" value={data.customers} />
        <Stat label="Trips today" value={data.tripsToday} />
        <div className="card">
          <label>Admin cut today</label>
          <strong>{peso(data.adminCutToday)}</strong>
        </div>
      </section>
      <section className="grid-2">
        <div className="card">
          <div className="panel-head">
            <h2>Activity</h2>
            <div className="chips">
              {(['weekly', 'monthly', 'yearly'] as const).map((item) => (
                <button key={item} type="button" className={range === item ? 'on' : ''} onClick={() => setRange(item)}>
                  {item[0].toUpperCase() + item.slice(1)}
                </button>
              ))}
            </div>
          </div>
          <div className="chart">
            <ResponsiveContainer>
              <LineChart data={data.series}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                <Tooltip />
                <Line type="monotone" dataKey="operatorsCreated" name="Operators" stroke="#e30613" strokeWidth={2.4} dot={false} />
                <Line type="monotone" dataKey="customersRegistered" name="Customers" stroke="#8be34a" strokeWidth={2} dot={false} />
                <Line type="monotone" dataKey="tripsCompleted" name="Trips" stroke="#7aa2ff" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>
        <div className="card">
          <div className="panel-head">
            <h2>Rider mix</h2>
          </div>
          <div className="chart">
            {emptyPie ? (
              <div className="soon">No riders yet. They will appear under each Operator.</div>
            ) : (
              <ResponsiveContainer>
                <PieChart>
                  <Pie data={pie} dataKey="value" nameKey="name" innerRadius={58} outerRadius={86} paddingAngle={3}>
                    {pie.map((item) => (
                      <Cell key={item.name} fill={item.color} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            )}
          </div>
        </div>
      </section>
      <section className="card">
        <div className="panel-head">
          <h2>Recent Operators</h2>
        </div>
        {data.recentOperators.length === 0 ? (
          <p>No Operators yet. Create the first one in Operators.</p>
        ) : (
          <div className="list">
            {data.recentOperators.map((op) => (
              <div className="row" key={op.id}>
                <div className="person-cell">
                  <Avatar name={op.companyName} photoUrl={op.profilePhotoUrl} />
                  <div>
                    <strong>{op.companyName}</strong>
                    <div style={{ color: 'var(--muted)', fontSize: 13 }}>{op.contactPhone}</div>
                  </div>
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <StatusTag active={op.isActive} />
                  <button className="btn tiny" type="button" onClick={() => onOpenOperator(op.id)}>
                    Open
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </>
  )
}

function Stat({ label, value, hint, tone }: { label: string; value: number; hint?: string; tone?: string }) {
  return (
    <div className={`card${tone ? ` tone-${tone}` : ''}`}>
      <label>{label}</label>
      <strong>{value}</strong>
      {hint && <div className="delta">{hint}</div>}
    </div>
  )
}

function OperatorsPage({
  view,
  selectedId,
  onList,
  onCreate,
  onOpen,
  onEdit,
  onBookings,
}: {
  view: 'list' | 'create' | 'detail' | 'edit' | 'bookings'
  selectedId: string | null
  onList: () => void
  onCreate: () => void
  onOpen: (id: string) => void
  onEdit: (id: string) => void
  onBookings: (id: string) => void
}) {
  if (view === 'create') {
    return <OperatorFormPage onDone={onList} onCancel={onList} />
  }
  if (view === 'edit' && selectedId) {
    return <OperatorFormPage operatorId={selectedId} onDone={() => onOpen(selectedId)} onCancel={() => onOpen(selectedId)} />
  }
  if (view === 'bookings' && selectedId) {
    return <AdminOperatorBookingsPage operatorId={selectedId} onBack={() => onOpen(selectedId)} />
  }
  if (view === 'detail' && selectedId) {
    return <OperatorDetailPage id={selectedId} onBack={onList} onEdit={() => onEdit(selectedId)} onBookings={() => onBookings(selectedId)} />
  }
  return <OperatorsListPage onCreate={onCreate} onOpen={onOpen} onEdit={onEdit} />
}

function OperatorsListPage({
  onCreate,
  onOpen,
  onEdit,
}: {
  onCreate: () => void
  onOpen: (id: string) => void
  onEdit: (id: string) => void
}) {
  const [q, setQ] = useState('')
  const [items, setItems] = useState<OperatorListItem[]>([])
  const [suggest, setSuggest] = useState<OperatorListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.operators(q, page, pageSize)
        .then((data) => {
          setItems(data.items)
          setTotal(data.total)
        })
        .catch((err: Error) => setError(err.message))
      api.operators(q, 1, 8).then((data) => setSuggest(data.items)).catch(() => setSuggest([]))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, page])

  return (
    <div className="card">
      <div className="toolbar">
        <h2 style={{ margin: 0 }}>Operators</h2>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
          <PersonSuggest
            value={q}
            onChange={(value) => { setQ(value); setPage(1) }}
            placeholder="Search company or phone"
            items={suggest.map((row) => ({
              id: row.id,
              name: row.companyName,
              phone: row.contactPhone,
              photoUrl: row.profilePhotoUrl,
            }))}
            onPick={(item) => {
              setQ(item.name)
              setPage(1)
              onOpen(item.id)
            }}
          />
          <button className="btn" type="button" onClick={onCreate} style={{ width: 'auto', whiteSpace: 'nowrap' }}>
            Create Operator
          </button>
        </div>
      </div>
      {error && <p className="error">{error}</p>}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Company</th>
              <th>Contact</th>
              <th>Phone</th>
              <th>Fleet</th>
              <th>Commission</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {items.map((row) => (
              <tr key={row.id} className="clickable" onClick={() => onOpen(row.id)}>
                <td>
                  <div className="person-cell">
                    <Avatar name={row.companyName} photoUrl={row.profilePhotoUrl} />
                    <span>{row.companyName}</span>
                  </div>
                </td>
                <td>{row.contactName}</td>
                <td>{row.contactPhone}</td>
                <td>
                  <div className="tag-row">
                    <VehicleTag type="Motorcycle" count={row.ridersMotorcycle} />
                    <VehicleTag type="Tricycle" count={row.ridersTricycle} />
                  </div>
                </td>
                <td>{commissionRates(row.motorcycleCommissionPercent, row.tricycleCommissionPercent)}</td>
                <td><StatusTag active={row.isActive} /></td>
                <td>
                  <button className="btn tiny" type="button" onClick={(e) => { e.stopPropagation(); onEdit(row.id) }}>
                    Edit
                  </button>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr><td colSpan={7}>No Operators match that search.</td></tr>
            )}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function OperatorFormPage({
  operatorId,
  onDone,
  onCancel,
}: {
  operatorId?: string
  onDone: () => void
  onCancel: () => void
}) {
  const isEdit = !!operatorId
  const [form, setForm] = useState({
    companyName: '',
    contactName: '',
    phone: '',
    governmentIdType: '',
    governmentId: '',
    motorcycleCommissionPercent: '10',
    tricycleCommissionPercent: '5',
  })
  const [address, setAddress] = useState<AddressValue>({
    province: null,
    municipality: null,
    barangay: null,
    details: '',
  })
  const [areas, setAreas] = useState<OperatorArea[]>([])
  const [profilePhoto, setProfilePhoto] = useState<File | null>(null)
  const [govPhoto, setGovPhoto] = useState<File | null>(null)
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [existing, setExisting] = useState<OperatorDetail | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!operatorId) {
      return
    }
    api.operator(operatorId).then((op) => {
      setExisting(op)
      setForm({
        companyName: op.companyName,
        contactName: op.contactName,
        phone: op.contactPhone,
        governmentIdType: op.governmentIdType ?? '',
        governmentId: op.governmentId,
        motorcycleCommissionPercent: String(op.motorcycleCommissionPercent ?? 10),
        tricycleCommissionPercent: String(op.tricycleCommissionPercent ?? 5),
      })
      setAddress({
        province: op.address?.provinceId ? { id: op.address.provinceId, name: op.address.province } : null,
        municipality: op.address?.municipalityId ? { id: op.address.municipalityId, name: op.address.municipality } : null,
        barangay: op.address?.barangayId ? { id: op.address.barangayId, name: op.address.barangay } : null,
        details: op.address?.details ?? '',
      })
      setAreas(op.areas ?? [])
    }).catch((err: Error) => setError(err.message))
  }, [operatorId])

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!form.governmentIdType) {
      setError('Choose a government ID type such as Driver’s License or Passport.')
      return
    }
    if (!address.barangay || !address.details.trim()) {
      setError('Choose province, municipality, and barangay, then add specific address details.')
      return
    }
    if (areas.length === 0) {
      setError('Assign at least one barangay to the area of operation.')
      return
    }
    const motorcycleCommission = Number(form.motorcycleCommissionPercent)
    const tricycleCommission = Number(form.tricycleCommissionPercent)
    if (!Number.isFinite(motorcycleCommission) || motorcycleCommission < 0 || motorcycleCommission > 100) {
      setError('Motorcycle commission must be a number from 0 to 100.')
      return
    }
    if (!Number.isFinite(tricycleCommission) || tricycleCommission < 0 || tricycleCommission > 100) {
      setError('Tricycle commission must be a number from 0 to 100.')
      return
    }
    if (!isEdit && password.trim().length < 6) {
      setError('Set a password of at least 6 characters.')
      return
    }
    if (password.trim().length > 0 && password !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }
    if (isEdit && password.trim().length > 0 && password.trim().length < 6) {
      setError('Password must be at least 6 characters.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const data = new FormData()
      data.append('companyName', form.companyName)
      data.append('contactName', form.contactName)
      data.append('phone', form.phone)
      if (password.trim()) data.append('password', password.trim())
      data.append('addressBarangayId', address.barangay.id)
      data.append('addressDetails', address.details.trim())
      data.append('governmentIdType', form.governmentIdType)
      data.append('governmentId', form.governmentId)
      data.append('motorcycleCommissionPercent', String(motorcycleCommission))
      data.append('tricycleCommissionPercent', String(tricycleCommission))
      for (const area of areas) {
        data.append('barangayIds', area.barangayId)
      }
      if (profilePhoto) {
        data.append('profilePhoto', profilePhoto)
      }
      if (govPhoto) {
        data.append('governmentIdPhoto', govPhoto)
      }
      if (isEdit && operatorId) {
        await api.updateOperator(operatorId, data)
      } else {
        await api.createOperator(data)
      }
      onDone()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save operator.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="card" onSubmit={submit}>
      <div className="panel-head">
        <div>
          <button className="btn tiny" type="button" onClick={onCancel}>Back to list</button>
          <h2 style={{ marginTop: 12 }}>{isEdit ? 'Edit Operator' : 'Create Operator'}</h2>
          <p className="muted">They sign in with their phone number and this password.</p>
        </div>
      </div>
      <div className="form-sections">
        <section className="form-section">
          <h3>Company details</h3>
          <div className="form-grid">
            <label className="field">
              <span>Company</span>
              <input value={form.companyName} onChange={(e) => setForm({ ...form, companyName: e.target.value })} />
            </label>
            <label className="field">
              <span>Contact name</span>
              <input value={form.contactName} onChange={(e) => setForm({ ...form, contactName: e.target.value })} />
            </label>
            <label className="field">
              <span>Phone</span>
              <input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
            </label>
            <label className="field">
              <span>{isEdit ? 'New password (optional)' : 'Password'}</span>
              <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" />
            </label>
            <label className="field">
              <span>Confirm password</span>
              <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
            </label>
            <label className="field">
              <span>Profile photo</span>
              {existing?.profilePhotoUrl && !profilePhoto && (
                <div className="person-cell" style={{ marginBottom: 8 }}>
                  <Avatar name={existing.companyName} photoUrl={existing.profilePhotoUrl} size={48} />
                  <small>Current photo. Choose a file to replace.</small>
                </div>
              )}
              <input type="file" accept="image/*" onChange={(e) => setProfilePhoto(e.target.files?.[0] ?? null)} />
            </label>
          </div>
        </section>

        <section className="form-section">
          <h3>Government ID</h3>
          <div className="form-grid">
            <div className="field">
              <span>ID type</span>
              <GovernmentIdTypeField
                value={form.governmentIdType}
                onChange={(governmentIdType) => setForm({ ...form, governmentIdType })}
              />
            </div>
            <label className="field">
              <span>ID number</span>
              <input value={form.governmentId} onChange={(e) => setForm({ ...form, governmentId: e.target.value })} placeholder="As printed on the ID" />
            </label>
            <label className="field wide">
              <span>ID photo</span>
              {existing?.governmentIdPhotoUrl && !govPhoto && (
                <img className="id-preview" src={existing.governmentIdPhotoUrl} alt="Government ID" style={{ marginBottom: 8 }} />
              )}
              <input type="file" accept="image/*" onChange={(e) => setGovPhoto(e.target.files?.[0] ?? null)} />
            </label>
          </div>
        </section>

        <section className="form-section">
          <h3>Address and area</h3>
          <div className="form-grid">
            <div className="field wide">
              <span>Full address</span>
              <AddressPicker value={address} onChange={setAddress} />
            </div>
            <div className="field wide">
              <span>Area of operation</span>
              <AreaPicker assigned={areas} onChange={setAreas} />
            </div>
          </div>
        </section>

        <section className="form-section commission">
          <h3>Platform commission</h3>
          <p className="form-hint">Set separately for each vehicle type. This is the platform cut of every completed trip fare.</p>
          <div className="form-grid">
            <label className="field">
              <span>Motorcycle (%)</span>
              <input
                type="number"
                min={0}
                max={100}
                step="0.01"
                value={form.motorcycleCommissionPercent}
                onChange={(e) => setForm({ ...form, motorcycleCommissionPercent: e.target.value })}
              />
            </label>
            <label className="field">
              <span>Tricycle (%)</span>
              <input
                type="number"
                min={0}
                max={100}
                step="0.01"
                value={form.tricycleCommissionPercent}
                onChange={(e) => setForm({ ...form, tricycleCommissionPercent: e.target.value })}
              />
            </label>
          </div>
        </section>
      </div>
      {error && <p className="error">{error}</p>}
      <div style={{ display: 'flex', gap: 10, maxWidth: 360 }}>
        <button className="btn" type="submit" disabled={busy}>
          {isEdit ? 'Save changes' : 'Create Operator'}
        </button>
        <button className="btn ghost" type="button" onClick={onCancel}>Cancel</button>
      </div>
    </form>
  )
}

function OperatorDetailPage({ id, onBack, onEdit, onBookings }: { id: string; onBack: () => void; onEdit: () => void; onBookings: () => void }) {
  const [op, setOp] = useState<OperatorDetail | null>(null)
  const [error, setError] = useState('')
  const [listError, setListError] = useState('')
  const [q, setQ] = useState('')
  const [riders, setRiders] = useState<RiderListItem[]>([])
  const [suggest, setSuggest] = useState<RiderListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [riderId, setRiderId] = useState<string | null>(null)
  const [notice, setNotice] = useState('')
  const [actionError, setActionError] = useState('')
  const [resetOpen, setResetOpen] = useState(false)
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [resetBusy, setResetBusy] = useState(false)
  const pageSize = 10

  async function load() {
    try {
      setOp(await api.operator(id))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Operator not found.')
    }
  }

  useEffect(() => {
    void load()
  }, [id])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.operatorRiders(id, q, page, pageSize)
        .then((data) => {
          setRiders(data.items)
          setTotal(data.total)
          setListError('')
        })
        .catch((err: Error) => {
          setRiders([])
          setTotal(0)
          setListError(err.message)
        })
      api.operatorRiders(id, q, 1, 8).then((data) => setSuggest(data.items)).catch(() => setSuggest([]))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [id, q, page])

  async function toggle() {
    if (!op) {
      return
    }
    await api.setOperatorActive(op.id, !op.isActive)
    await load()
  }

  async function resetPassword() {
    if (newPassword.trim().length < 6) {
      setActionError('Password must be at least 6 characters.')
      return
    }
    if (newPassword !== confirmPassword) {
      setActionError('Passwords do not match.')
      return
    }
    setActionError('')
    setNotice('')
    setResetBusy(true)
    try {
      const result = await api.resetOperatorPassword(id, newPassword.trim())
      setNotice(result.message)
      setResetOpen(false)
      setNewPassword('')
      setConfirmPassword('')
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Could not reset password.')
    } finally {
      setResetBusy(false)
    }
  }

  if (error) {
    return <p className="error">{error}</p>
  }
  if (!op) {
    return <p>Loading operator…</p>
  }

  if (riderId) {
    return <RiderDetailPage operatorId={id} riderId={riderId} onBack={() => setRiderId(null)} />
  }

  return (
    <div className="card">
      <div className="panel-head">
        <div className="person-cell" style={{ alignItems: 'flex-start' }}>
          <Avatar name={op.companyName} photoUrl={op.profilePhotoUrl} size={56} />
          <div>
            <button className="btn tiny" type="button" onClick={onBack}>Back</button>
            <h2 style={{ marginTop: 12 }}>{op.companyName}</h2>
            <p>{op.contactName} · {op.contactPhone}</p>
            <p>
              {op.address?.details || op.fullAddress || 'No address yet'}
              {op.address?.barangay ? (
                <>
                  <br />
                  {op.address.barangay}, {op.address.municipality}, {op.address.province}
                </>
              ) : null}
            </p>
            <p>Government ID: {[op.governmentIdType, op.governmentId].filter(Boolean).join(' · ') || '—'}</p>
            <p>System Comm moto {percent(op.motorcycleCommissionPercent)} · System Comm tri {percent(op.tricycleCommissionPercent)}</p>
            <div className="area-summary">
              <span>Area of operation</span>
              {(op.areas ?? []).length === 0 ? (
                <p>{op.areaOfOperation || '—'}</p>
              ) : (
                <AreaGroups areas={op.areas} />
              )}
            </div>
            <div className="tag-row" style={{ marginTop: 10 }}>
              <VehicleTag type="Motorcycle" count={op.ridersMotorcycle} />
              <VehicleTag type="Tricycle" count={op.ridersTricycle} />
              <StatusTag active={op.isActive} />
            </div>
          </div>
        </div>
        <div style={{ display: 'grid', gap: 8, justifyItems: 'end' }}>
          {op.governmentIdPhotoUrl && (
            <img className="id-preview" src={op.governmentIdPhotoUrl} alt="Government ID" />
          )}
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
            <button className="btn tiny" type="button" onClick={onBookings}>Booking</button>
            <button className="btn tiny" type="button" onClick={onEdit}>Edit</button>
            <button className="btn tiny" type="button" onClick={() => { setResetOpen((open) => !open); setActionError(''); setNotice('') }}>
              {resetOpen ? 'Cancel reset' : 'Reset password'}
            </button>
            <button className={`btn tiny ${op.isActive ? 'danger' : ''}`} type="button" onClick={toggle}>
              {op.isActive ? 'Deactivate' : 'Activate'}
            </button>
          </div>
        </div>
      </div>
      {actionError ? <p className="error">{actionError}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      {resetOpen ? (
        <div className="form-grid" style={{ marginBottom: 16 }}>
          <label className="field">
            <span>New operator password</span>
            <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} autoComplete="new-password" />
          </label>
          <label className="field">
            <span>Confirm password</span>
            <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
          </label>
          <div style={{ display: 'flex', alignItems: 'end' }}>
            <button className="btn" type="button" disabled={resetBusy} onClick={() => void resetPassword()}>
              {resetBusy ? 'Saving…' : 'Save password'}
            </button>
          </div>
        </div>
      ) : null}
      <div className="toolbar">
        <h3 style={{ margin: 0 }}>Riders under this Operator</h3>
        <PersonSuggest
          value={q}
          onChange={(value) => { setQ(value); setPage(1) }}
          placeholder="Search rider name, phone, or plate"
          items={suggest.map((row) => ({
            id: row.id,
            name: row.fullName,
            phone: row.phoneNumber,
            photoUrl: row.profilePhotoUrl,
            vehicleType: row.vehicleType,
            extra: row.plateNumber,
          }))}
          onPick={(item) => {
            setQ(item.name)
            setPage(1)
          }}
        />
      </div>
      {listError && <p className="error">{listError}</p>}
      {total === 0 ? (
        <p>No riders match that search.</p>
      ) : (
        <>
          <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Phone</th>
                <th>Vehicle</th>
                <th>Plate</th>
                <th>License</th>
                <th>License photo</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {riders.map((rider) => (
                <tr key={rider.id} className="clickable" onClick={() => setRiderId(rider.id)}>
                  <td>
                    <div className="person-cell">
                      <ClickableAvatar name={rider.fullName} photoUrl={rider.profilePhotoUrl} />
                      <span>{rider.fullName}</span>
                    </div>
                  </td>
                  <td>{rider.phoneNumber}</td>
                  <td><VehicleTag type={rider.vehicleType} /></td>
                  <td>{rider.plateNumber}</td>
                  <td>{[rider.licenseType, rider.licenseNumber].filter(Boolean).join(' · ') || '—'}</td>
                  <td><PhotoThumb src={rider.licensePhotoUrl} alt={`${rider.fullName} license`} /></td>
                  <td><StatusTag active={rider.isActive} /></td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
          <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
        </>
      )}
    </div>
  )
}

function RiderDetailPage({
  operatorId,
  riderId,
  onBack,
}: {
  operatorId: string
  riderId: string
  onBack: () => void
}) {
  const [rider, setRider] = useState<RiderDetail | null>(null)
  const [rideId, setRideId] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [actionError, setActionError] = useState('')
  const [resetOpen, setResetOpen] = useState(false)
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [resetBusy, setResetBusy] = useState(false)

  useEffect(() => {
    api.operatorRider(operatorId, riderId)
      .then(setRider)
      .catch((err: Error) => setError(err.message))
  }, [operatorId, riderId])

  async function resetPassword() {
    if (newPassword.trim().length < 6) {
      setActionError('Password must be at least 6 characters.')
      return
    }
    if (newPassword !== confirmPassword) {
      setActionError('Passwords do not match.')
      return
    }
    setActionError('')
    setNotice('')
    setResetBusy(true)
    try {
      const result = await api.resetRiderPassword(operatorId, riderId, newPassword.trim())
      setNotice(result.message)
      setResetOpen(false)
      setNewPassword('')
      setConfirmPassword('')
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Could not reset password.')
    } finally {
      setResetBusy(false)
    }
  }

  if (!rider) {
    return error ? <p className="error">{error}</p> : <p>Loading rider…</p>
  }

  if (rideId) {
    return (
      <BookingDetailPage
        load={() => api.riderRide(operatorId, riderId, rideId)}
        loadKey={rideId}
        onBack={() => setRideId(null)}
        commissionView="rider"
      />
    )
  }

  return (
    <div className="card">
      <div className="panel-head">
        <div className="person-cell" style={{ alignItems: 'flex-start' }}>
          <ClickableAvatar name={rider.fullName} photoUrl={rider.profilePhotoUrl} size={72} />
          <div>
            <button className="btn tiny" type="button" onClick={onBack}>Back to riders</button>
            <h2 style={{ marginTop: 12 }}>{rider.fullName}</h2>
            <p>{rider.phoneNumber}</p>
            <p>
              {rider.address?.details || rider.fullAddress || 'No address yet'}
              {rider.address?.barangay ? (
                <>
                  <br />
                  {rider.address.barangay}, {rider.address.municipality}, {rider.address.province}
                </>
              ) : null}
            </p>
            <p>{rider.vehicleModel ? `${rider.vehicleType} · ${rider.vehicleModel}` : rider.vehicleType} · {rider.plateNumber}</p>
            <p>License: {[rider.licenseType, rider.licenseNumber].filter(Boolean).join(' · ') || '—'}</p>
            <p>Credibility: {rider.credibilityScore ?? 100}{rider.riderCancelCount ? ` · ${rider.riderCancelCount} cancel${rider.riderCancelCount === 1 ? '' : 's'}` : ''}</p>
            <div className="tag-row" style={{ marginTop: 10 }}>
              <VehicleTag type={rider.vehicleType} />
              <StatusTag active={rider.isActive} />
            </div>
            <button
              className="btn tiny"
              type="button"
              style={{ marginTop: 10 }}
              onClick={() => { setResetOpen((open) => !open); setActionError(''); setNotice('') }}
            >
              {resetOpen ? 'Cancel reset' : 'Reset password'}
            </button>
          </div>
        </div>
        <div className="rider-photos">
          <div>
            <span>Profile photo</span>
            {rider.profilePhotoUrl ? (
              <PhotoThumb src={rider.profilePhotoUrl} alt={`${rider.fullName} profile`} className="id-preview large" />
            ) : (
              <p className="muted">No profile photo yet. It will show here once the Operator uploads it.</p>
            )}
          </div>
          <div>
            <span>License photo</span>
            {rider.licensePhotoUrl ? (
              <PhotoThumb src={rider.licensePhotoUrl} alt={`${rider.fullName} license`} className="id-preview large" />
            ) : (
              <p className="muted">No license photo yet. It will show here once the Operator uploads it.</p>
            )}
          </div>
        </div>
      </div>
      {actionError ? <p className="error">{actionError}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      {resetOpen ? (
        <div className="form-grid" style={{ marginBottom: 16 }}>
          <label className="field">
            <span>New password</span>
            <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} autoComplete="new-password" />
          </label>
          <label className="field">
            <span>Confirm password</span>
            <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
          </label>
          <div style={{ display: 'flex', alignItems: 'end' }}>
            <button className="btn" type="button" disabled={resetBusy} onClick={() => void resetPassword()}>
              {resetBusy ? 'Saving…' : 'Save password'}
            </button>
          </div>
        </div>
      ) : null}
      <RidesReport
        sourceKey={`${operatorId}:${riderId}`}
        fetchRides={(opts) => api.riderRides(operatorId, riderId, opts)}
        onOpenRide={setRideId}
        commissionView="rider"
      />
    </div>
  )
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="detail-item">
      <span>{label}</span>
      <p>{value || '—'}</p>
    </div>
  )
}

type CommissionView = 'operator' | 'rider'

function CommissionSection({
  commission,
  view,
  status,
  fare,
}: {
  commission: RideCommissionBreakdown | null | undefined
  view: CommissionView
  status: TripStatus
  fare: number
}) {
  if (status === 'Cancelled' || !commission) {
    return null
  }

  return (
    <section className="commission-section">
      <div className="panel-head" style={{ marginBottom: 8 }}>
        <div>
          <h3 style={{ margin: 0 }}>Commission</h3>
          <p className="muted" style={{ margin: '4px 0 0' }}>
            {status === 'Completed' ? 'Split of the trip fare' : 'Estimated split of the trip fare'} · Fare {peso(fare)}
          </p>
        </div>
      </div>
      {view === 'rider' ? (
        <div className="fare-cards">
          <div className="detail-card">
            <span>System</span>
            <p className="detail-name" style={{ marginTop: 8 }}>
              {peso(commission.systemAmount + commission.operatorAmount)}
            </p>
            <p className="muted">{percent(commission.systemPercent + commission.operatorPercent)}</p>
          </div>
          <div className="detail-card">
            <span>Rider</span>
            <p className="detail-name" style={{ marginTop: 8 }}>{peso(commission.driverAmount)}</p>
            <p className="muted">{percent(commission.driverPercent)}</p>
          </div>
        </div>
      ) : (
        <div className="fare-cards three">
          <div className="detail-card">
            <span>Admin</span>
            <p className="detail-name" style={{ marginTop: 8 }}>{peso(commission.systemAmount)}</p>
            <p className="muted">{percent(commission.systemPercent)}</p>
          </div>
          <div className="detail-card">
            <span>Operator</span>
            <p className="detail-name" style={{ marginTop: 8 }}>{peso(commission.operatorAmount)}</p>
            <p className="muted">{percent(commission.operatorPercent)}</p>
          </div>
          <div className="detail-card">
            <span>Rider</span>
            <p className="detail-name" style={{ marginTop: 8 }}>{peso(commission.driverAmount)}</p>
            <p className="muted">{percent(commission.driverPercent)}</p>
          </div>
        </div>
      )}
    </section>
  )
}

function commissionTableCell(commission: RideCommissionBreakdown | null | undefined, status: TripStatus) {
  if (status === 'Cancelled' || !commission) {
    return '—'
  }
  return peso(commission.systemAmount)
}

function operatorCommissionTableCell(commission: RideCommissionBreakdown | null | undefined, status: TripStatus) {
  if (status === 'Cancelled' || !commission) {
    return '—'
  }
  return peso(commission.operatorAmount)
}

function driverCommissionTableCell(commission: RideCommissionBreakdown | null | undefined, status: TripStatus) {
  if (status === 'Cancelled' || !commission) {
    return '—'
  }
  return peso(commission.driverAmount)
}

function systemCommissionTableCell(commission: RideCommissionBreakdown | null | undefined, status: TripStatus) {
  if (status === 'Cancelled' || !commission) {
    return '—'
  }
  return peso(commission.systemAmount + commission.operatorAmount)
}

function BookingRating({ score, comment, ratedAtUtc, status }: {
  score: number | null
  comment: string | null
  ratedAtUtc: string | null
  status: TripStatus
}) {
  return (
    <div className="detail-item wide">
      <span>Customer rating</span>
      {score ? (
        <>
          <p className="star-rating" aria-label={`${score} out of 5`}>
            {Array.from({ length: 5 }, (_, i) => (
              <span key={i} className={i < score ? 'on' : undefined}>★</span>
            ))}
            <em>{score}/5</em>
          </p>
          {comment ? <small>{comment}</small> : null}
          {ratedAtUtc ? <small>Rated {phDateTime(ratedAtUtc)}</small> : null}
        </>
      ) : (
        <p>{status === 'Completed' ? 'Not rated' : '—'}</p>
      )}
    </div>
  )
}

function BookingDetailsBody({ ride, commissionView = 'operator' }: { ride: RideDetail; commissionView?: CommissionView }) {
  const customerPay = ride.customerFare && ride.customerFare > 0 ? ride.customerFare : ride.fare
  const promoCode = ride.promoCode || (ride.isPromoSponsored && ride.discountPercent != null ? `Save${ride.discountPercent}` : null)
  return (
    <>
      <div className="detail-grid">
        <DetailItem label="Booking number" value={ride.reference} />
        <DetailItem label="Status" value={ride.status} />
        {ride.isPromoSponsored ? (
          <div className="detail-item">
            <span>Promo</span>
            <p>
              <PromoTag isPromoSponsored promoCode={promoCode} discountPercent={ride.discountPercent} />
            </p>
            <small className="muted">
              Code {promoCode || '—'}
              {ride.discountPercent != null ? ` · ${ride.discountPercent}% off fare` : ''}
            </small>
          </div>
        ) : null}
        <DetailItem label="Customer" value={ride.customerName} />
        <DetailItem label="Customer phone" value={ride.customerPhone} />
        <div className="detail-item wide">
          <span>Pickup</span>
          <p>{stopAddress(ride.pickupStop?.details || ride.pickup)}</p>
        </div>
        <div className="detail-item wide">
          <span>Drop-off</span>
          <p>{stopAddress(ride.dropoffStop?.details || ride.dropoff)}</p>
        </div>
        <DetailItem label="Distance" value={`${ride.distanceKm.toFixed(1)} km`} />
        <DetailItem
          label="Passengers"
          value={`${Math.max(1, ride.passengerCount ?? 1)} passenger${(ride.passengerCount ?? 1) === 1 ? '' : 's'}`}
        />
        <DetailItem label="Fare" value={peso(ride.fare)} />
        {(ride.customerBoostAmount ?? 0) > 0 ? (
          <DetailItem label="Customer boost" value={`+${peso(ride.customerBoostAmount!)}`} />
        ) : null}
        {ride.isPromoSponsored ? (
          <>
            <DetailItem label="Customer pays" value={peso(customerPay)} />
            <DetailItem label="Operator owes rider" value={peso(ride.promoDiscountAmount ?? Math.max(0, ride.fare - customerPay))} />
          </>
        ) : null}
        <DetailItem label="Payment" value={paymentMethodLabel(ride.paymentMethod, ride.paymentMethodOther)} />
        <DetailItem label="Duration" value={ride.durationMinutes ? `${ride.durationMinutes} min` : '—'} />
        <DetailItem label="Vehicle" value={ride.vehicleModel ? `${ride.vehicleType} · ${ride.vehicleModel}` : ride.vehicleType} />
        <DetailItem label="Requested" value={phDateTime(ride.requestedAtUtc)} />
        {ride.scheduledAtUtc ? <DetailItem label="Scheduled" value={phDateTime(ride.scheduledAtUtc)} /> : null}
        <DetailItem
          label={
            ride.status === 'Cancelled' ? 'Cancelled'
              : ride.status === 'Completed' ? 'Completed'
                : ride.status === 'Waiting' ? 'Waiting since'
                  : ride.status === 'Pending' ? 'Requested'
                    : 'In progress'
          }
          value={
            ride.status === 'Cancelled' && ride.cancelledAtUtc
              ? phDateTime(ride.cancelledAtUtc)
              : ride.status === 'Completed' && ride.completedAtUtc
                ? phDateTime(ride.completedAtUtc)
                : phDateTime(ride.requestedAtUtc)
          }
        />
        {ride.notes ? <DetailItem label="Notes" value={ride.notes} /> : null}
        {ride.cancelReason ? <DetailItem label="Cancel reason" value={ride.cancelReason} /> : null}
        <BookingRating
          score={ride.rating}
          comment={ride.ratingComment}
          ratedAtUtc={ride.ratedAtUtc}
          status={ride.status}
        />
      </div>
      <CommissionSection
        commission={ride.commission}
        view={commissionView}
        status={ride.status}
        fare={ride.fare}
      />
      <div className="detail-split">
        <div className="detail-card">
          <span>Rider</span>
          <div className="person-cell" style={{ marginTop: 10 }}>
            <Avatar name={ride.riderName} photoUrl={ride.riderPhotoUrl} size={48} />
            <div>
              <p className="detail-name">{ride.riderName}</p>
              <p className="muted">{ride.riderPhone}</p>
              <p className="muted">{ride.plateNumber}{ride.vehicleModel ? ` · ${ride.vehicleModel}` : ''}</p>
            </div>
          </div>
        </div>
        <div className="detail-card">
          <span>Operator</span>
          <p className="detail-name" style={{ marginTop: 10 }}>{ride.operatorName}</p>
          <p className="muted">{ride.operatorPhone}</p>
        </div>
      </div>
      <BookingChat
        messages={ride.chat ?? []}
        customerName={ride.customerName}
        riderName={ride.riderName}
      />
    </>
  )
}

function BookingDetailPage({
  load,
  loadKey,
  onBack,
  backLabel = 'Back to rides',
  extra,
  allowReassign = false,
  commissionView = 'operator',
}: {
  load: () => Promise<RideDetail>
  loadKey: string
  onBack: () => void
  backLabel?: string
  extra?: ReactNode
  allowReassign?: boolean
  commissionView?: CommissionView
}) {
  const [ride, setRide] = useState<RideDetail | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    load()
      .then(setRide)
      .catch((err: Error) => setError(err.message))
  }, [loadKey])

  if (error) {
    return <p className="error">{error}</p>
  }
  if (!ride) {
    return <p>Loading booking…</p>
  }

  return (
    <div className="card">
      <div className="panel-head">
        <div>
          <button className="btn tiny" type="button" onClick={onBack}>{backLabel}</button>
          <h2 style={{ marginTop: 12 }}>{ride.reference || 'Booking'}</h2>
          <p>Complete booking details</p>
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          {extra}
          <PromoTag
            isPromoSponsored={ride.isPromoSponsored}
            promoCode={ride.promoCode}
            discountPercent={ride.discountPercent}
          />
          <PaymentMethodTag method={ride.paymentMethod} other={ride.paymentMethodOther} />
          <TripStatusTag status={ride.status} />
        </div>
      </div>
      <BookingDetailsBody ride={ride} commissionView={commissionView} />
      {allowReassign && (ride.status === 'Pending' || ride.status === 'Waiting') ? (
        <BookingReassign ride={ride} onAssigned={setRide} />
      ) : null}
    </div>
  )
}

function BookingReassign({
  ride,
  onAssigned,
}: {
  ride: RideDetail
  onAssigned: (ride: RideDetail) => void
}) {
  const [query, setQuery] = useState('')
  const [riderId, setRiderId] = useState('')
  const [riderName, setRiderName] = useState('')
  const [riders, setRiders] = useState<RiderListItem[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.opRiders(query, 1, 20)
        .then((data) => setRiders(data.items.filter((row) => row.isActive && row.id !== ride.riderId)))
        .catch(() => setRiders([]))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [query, ride.riderId])

  async function save() {
    if (!riderId) {
      setError('Choose another rider from your fleet.')
      return
    }
    if (!window.confirm(`Reassign this booking to ${riderName}?`)) {
      return
    }
    setBusy(true)
    setError('')
    try {
      const next = await api.reassignOperatorBooking(ride.id, riderId)
      setQuery('')
      setRiderId('')
      setRiderName('')
      onAssigned(next)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not reassign this booking.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="detail-card" style={{ marginTop: 16 }}>
      <span>Reassign rider</span>
      <p className="muted" style={{ marginTop: 6 }}>Move this booking to another active rider. Fare updates if the vehicle type changes.</p>
      {error ? <p className="error">{error}</p> : null}
      <div className="form-grid" style={{ marginTop: 10 }}>
        <label className="field">
          <span>New rider</span>
          <PersonSuggest
            value={query}
            onChange={(value) => { setQuery(value); setRiderId(''); setRiderName('') }}
            placeholder="Search rider name, phone, or plate"
            items={riders.map((row) => ({
              id: row.id,
              name: row.fullName,
              phone: row.phoneNumber,
              photoUrl: row.profilePhotoUrl,
              extra: row.plateNumber,
              vehicleType: row.vehicleType,
            }))}
            onPick={(item) => { setQuery(item.name); setRiderId(item.id); setRiderName(item.name) }}
          />
        </label>
      </div>
      <div style={{ marginTop: 12, maxWidth: 220 }}>
        <button className="btn" type="button" disabled={busy || !riderId} onClick={() => void save()}>
          {busy ? 'Reassigning…' : 'Reassign booking'}
        </button>
      </div>
    </div>
  )
}

function BookingChat({
  messages,
  customerName,
  riderName,
}: {
  messages: RideChatMessage[]
  customerName: string
  riderName: string
}) {
  return (
    <div className="chat-history">
      <div className="chat-history-head">
        <span>Chat history</span>
        <small>{messages.length === 0 ? 'No messages' : `${messages.length} message${messages.length === 1 ? '' : 's'}`}</small>
      </div>
      {messages.length === 0 ? (
        <p className="muted">No customer–rider messages for this booking.</p>
      ) : (
        <div className="chat-thread">
          {messages.map((message) => (
            <div key={message.id} className={`chat-bubble ${message.sender === 'Rider' ? 'rider' : 'customer'}`}>
              <strong>{message.sender === 'Rider' ? riderName : customerName}</strong>
              {message.photoUrl ? <img className="chat-photo" src={message.photoUrl} alt="" /> : null}
              {message.body ? <p>{message.body}</p> : null}
              <small>{phDateTime(message.sentAtUtc)}</small>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function isoDate(value: Date) {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function addDays(value: Date, days: number) {
  const next = new Date(value)
  next.setDate(next.getDate() + days)
  return next
}

function RidesReport({
  sourceKey,
  fetchRides,
  onOpenRide,
  title = 'Bookings',
  commissionView = 'operator',
}: {
  sourceKey: string
  fetchRides: (opts: RideQuery) => Promise<RiderRides>
  onOpenRide: (rideId: string) => void
  title?: string
  commissionView?: CommissionView
}) {
  const today = isoDate(new Date())
  const [mode, setMode] = useState<'weekly' | 'monthly' | 'yearly' | 'date' | 'range'>('weekly')
  const [from, setFrom] = useState(isoDate(addDays(new Date(), -6)))
  const [to, setTo] = useState(today)
  const [bookingQ, setBookingQ] = useState('')
  const [statusFilter, setStatusFilter] = useState<TripStatus | ''>('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<RiderRides | null>(null)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    setPage(1)
  }, [mode, from, to, bookingQ, statusFilter])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      const custom = mode === 'date' || mode === 'range'
      if (!bookingQ.trim() && custom && (!from || !to)) {
        return
      }
      const start = custom && from > to ? to : from
      const end = custom && from > to ? from : to
      fetchRides({
        range: mode === 'date' || mode === 'range' ? undefined : mode,
        from: !bookingQ.trim() && custom ? (mode === 'date' ? end : start) : undefined,
        to: !bookingQ.trim() && custom ? end : undefined,
        q: bookingQ.trim() || undefined,
        status: statusFilter || undefined,
        page,
        pageSize,
      })
        .then(setData)
        .catch((err: Error) => setError(err.message))
    }, bookingQ ? 200 : 0)
    return () => window.clearTimeout(handle)
  }, [sourceKey, mode, from, to, bookingQ, statusFilter, page])

  const chart = useMemo(
    () => (data?.series ?? []).map((point) => ({ ...point, date: point.date.slice(5) })),
    [data],
  )

  function pickMode(next: typeof mode) {
    setMode(next)
    if (next === 'date') {
      setFrom(today)
      setTo(today)
    }
    if (next === 'range') {
      setFrom(isoDate(addDays(new Date(), -6)))
      setTo(today)
    }
  }

  return (
    <div className="ride-report">
      <div className="toolbar">
        <h3 style={{ margin: 0 }}>{title}</h3>
        <div className="ride-filters">
          <div className="chips">
            {(['weekly', 'monthly', 'yearly', 'date', 'range'] as const).map((item) => (
              <button key={item} type="button" className={mode === item ? 'on' : ''} onClick={() => pickMode(item)}>
                {item === 'date' ? 'By date' : item === 'range' ? 'By range' : item[0].toUpperCase() + item.slice(1)}
              </button>
            ))}
          </div>
          {mode === 'date' && (
            <label className="date-search">
              <span>Date</span>
              <input type="date" value={to} max={today} onChange={(e) => { setFrom(e.target.value); setTo(e.target.value) }} />
            </label>
          )}
          {mode === 'range' && (
            <div className="date-search">
              <span>From</span>
              <input type="date" value={from} max={to || today} onChange={(e) => setFrom(e.target.value)} />
              <span>To</span>
              <input type="date" value={to} min={from} max={today} onChange={(e) => setTo(e.target.value)} />
            </div>
          )}
          <label className="date-search">
            <span>Booking no.</span>
            <input
              value={bookingQ}
              placeholder="YP20260816-0001"
              autoComplete="off"
              onChange={(e) => setBookingQ(e.target.value)}
            />
          </label>
        </div>
      </div>
      {error && <p className="error">{error}</p>}
      {!data ? (
        <p>Loading rides…</p>
      ) : (
        <>
          <section className="stats ride-stats">
            <Stat label="Rides" value={data.summary.total} />
            <Stat label="Completed" value={data.summary.completed} />
            <Stat label="Cancelled" value={data.summary.cancelled} />
            {commissionView === 'rider' ? (
              <>
                <div className="card">
                  <label>System</label>
                  <strong>{peso(data.summary.systemAmount + data.summary.operatorAmount)}</strong>
                </div>
                <div className="card">
                  <label>Rider</label>
                  <strong>{peso(data.summary.driverAmount)}</strong>
                </div>
              </>
            ) : (
              <>
                <div className="card">
                  <label>Admin</label>
                  <strong>{peso(data.summary.systemAmount)}</strong>
                </div>
                <div className="card">
                  <label>Operator</label>
                  <strong>{peso(data.summary.operatorAmount)}</strong>
                </div>
                <div className="card">
                  <label>Rider</label>
                  <strong>{peso(data.summary.driverAmount)}</strong>
                </div>
              </>
            )}
            <div className="card">
              <label>Gross fare</label>
              <strong>{peso(data.summary.grossFare)}</strong>
            </div>
          </section>
          <div className="chart ride-chart">
            <ResponsiveContainer>
              <LineChart data={chart}>
                <XAxis dataKey="date" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                <Tooltip />
                <Line type="monotone" dataKey="completed" name="Completed rides" stroke="#e30613" strokeWidth={2.4} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
          <TripStatusFilter value={statusFilter} onChange={setStatusFilter} />
          {data.rides.total === 0 ? (
            <p>{statusFilter ? `No ${TRIP_STATUS_FILTERS.find((item) => item.value === statusFilter)?.label.toLowerCase() ?? statusFilter.toLowerCase()} bookings in this period.` : 'No rides in this period.'}</p>
          ) : (
            <>
              <table>
                <thead>
                  <tr>
                    <th>Booking no.</th>
                    <th>When</th>
                    <th>Customer</th>
                    <th>Route</th>
                    <th>Payment</th>
                    <th>Fare</th>
                    {commissionView === 'rider' ? (
                      <>
                        <th>System</th>
                        <th>Rider</th>
                      </>
                    ) : (
                      <>
                        <th>Admin</th>
                        <th>Operator</th>
                        <th>Rider</th>
                      </>
                    )}
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {data.rides.items.map((ride: RideListItem) => (
                    <tr key={ride.id} className="clickable" onClick={() => onOpenRide(ride.id)}>
                      <td><span className="booking-no">{ride.reference || '—'}</span></td>
                      <td>{phDateTime(ride.requestedAtUtc)}</td>
                      <td>{ride.customerName}</td>
                      <td>
                        <div className="route-cell">
                          <span>Pickup</span>
                          <p>{stopAddress(ride.pickup)}</p>
                          <span>Drop-off</span>
                          <p>{stopAddress(ride.dropoff)}</p>
                        </div>
                      </td>
                      <td><PaymentMethodTag method={ride.paymentMethod} other={ride.paymentMethodOther} /></td>
                      <td>{peso(ride.fare)}</td>
                      {commissionView === 'rider' ? (
                        <>
                          <td>{systemCommissionTableCell(ride.commission, ride.status)}</td>
                          <td>{driverCommissionTableCell(ride.commission, ride.status)}</td>
                        </>
                      ) : (
                        <>
                          <td>{commissionTableCell(ride.commission, ride.status)}</td>
                          <td>{operatorCommissionTableCell(ride.commission, ride.status)}</td>
                          <td>{driverCommissionTableCell(ride.commission, ride.status)}</td>
                        </>
                      )}
                      <td><TripStatusTag status={ride.status} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <Pager page={data.rides.page} pageSize={data.rides.pageSize} total={data.rides.total} onPage={setPage} />
            </>
          )}
        </>
      )}
    </div>
  )
}

function CustomersPage({
  selectedId,
  onList,
  onOpen,
}: {
  selectedId: string | null
  onList: () => void
  onOpen: (id: string) => void
}) {
  if (selectedId) {
    return <CustomerDetailPage customerId={selectedId} onBack={onList} />
  }

  return <CustomerListPage onOpen={onOpen} />
}

function CustomerListPage({ onOpen }: { onOpen: (id: string) => void }) {
  const [q, setQ] = useState('')
  const [items, setItems] = useState<CustomerListItem[]>([])

  useEffect(() => {
    void api.customers('').then(setItems)
  }, [])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.customers(q).then(setItems).catch(() => setItems([]))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q])

  return (
    <div className="card">
      <div className="toolbar">
        <h2 style={{ margin: 0 }}>Customers</h2>
        <PersonSuggest
          value={q}
          onChange={setQ}
          placeholder="Search name or phone"
          items={items.map((row) => ({
            id: row.id,
            name: row.fullName || 'Customer',
            phone: row.phoneNumber,
            photoUrl: row.photoUrl,
          }))}
          onPick={(item) => onOpen(item.id)}
        />
      </div>
      {items.some((row) => row.deleteStatus === 'Pending') ? (
        <div className="card delete-alert" style={{ marginBottom: 16, borderLeft: '4px solid #d48b00' }}>
          <p style={{ margin: 0 }}>
            <strong>{items.filter((row) => row.deleteStatus === 'Pending').length}</strong>
            {' '}customer{items.filter((row) => row.deleteStatus === 'Pending').length === 1 ? '' : 's'} requested account deletion. Open a row tagged Delete requested to approve or reject.
          </p>
        </div>
      ) : null}
      <table>
        <thead>
          <tr>
            <th>First name</th>
            <th>Last name</th>
            <th>Phone</th>
            <th>Registered</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {items.length === 0 ? (
            <tr><td colSpan={5}>No customers yet. They will self-register on the public site later.</td></tr>
          ) : items.map((row) => (
            <tr key={row.id} className="clickable" onClick={() => onOpen(row.id)}>
              <td>
                <div className="person-cell">
                  <Avatar name={row.fullName || 'Customer'} photoUrl={row.photoUrl} />
                  <span>{row.firstName || row.fullName}</span>
                </div>
              </td>
              <td>{row.lastName || '—'}</td>
              <td>{row.phoneNumber}</td>
              <td>{phDate(row.registeredAtUtc)}</td>
              <td>
                <div className="tag-row" style={{ marginTop: 0 }}>
                  <StatusTag active={row.isActive} />
                  {row.deleteStatus === 'Pending' ? <span className="tag pending">Delete requested</span> : null}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function CustomerDetailPage({ customerId, onBack }: { customerId: string; onBack: () => void }) {
  const [customer, setCustomer] = useState<CustomerDetail | null>(null)
  const [rideId, setRideId] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [reason, setReason] = useState('')
  const [note, setNote] = useState('')

  useEffect(() => {
    api.customer(customerId)
      .then(setCustomer)
      .catch((err: Error) => setError(err.message))
  }, [customerId])

  async function resetPassword() {
    setError('')
    setNotice('')
    try {
      const result = await api.resetCustomerPassword(customerId)
      setNotice(result.message)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not reset password.')
    }
  }

  if (!customer) {
    return error ? <p className="error">{error}</p> : <p>Loading customer…</p>
  }

  if (rideId) {
    return (
      <BookingDetailPage
        load={() => api.customerRide(customerId, rideId)}
        loadKey={rideId}
        onBack={() => setRideId(null)}
      />
    )
  }

  async function act(work: () => Promise<CustomerDetail>) {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      setCustomer(await work())
      setReason('')
      setNote('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Request failed.')
    } finally {
      setBusy(false)
    }
  }

  const del = customer.deleteRequest

  return (
    <div className="card">
      <div className="panel-head">
        <div className="person-cell" style={{ alignItems: 'flex-start' }}>
          <ClickableAvatar name={customer.fullName} photoUrl={customer.photoUrl} size={72} />
          <div>
            <button className="btn tiny" type="button" onClick={onBack}>Back to customers</button>
            <h2 style={{ marginTop: 12 }}>{customer.fullName}</h2>
            <p>{customer.phoneNumber}</p>
            <div className="tag-row">
              <StatusTag active={customer.isActive} />
              {del.status !== 'None' ? <span className={`tag ${del.status.toLowerCase()}`}>Delete {del.status.toLowerCase()}</span> : null}
            </div>
            <button className="btn tiny" type="button" style={{ marginTop: 10 }} onClick={() => void resetPassword()}>
              Reset password
            </button>
          </div>
        </div>
        <div className="rider-photos">
          <div>
            <span>Profile photo</span>
            {customer.photoUrl ? (
              <PhotoThumb src={customer.photoUrl} alt={`${customer.fullName} profile`} className="id-preview large" />
            ) : (
              <p className="muted">No profile photo yet.</p>
            )}
          </div>
        </div>
      </div>
      {notice ? <p className="ok">{notice}</p> : null}
      <div className="detail-grid">
        <DetailItem label="First name" value={customer.firstName} />
        <DetailItem label="Last name" value={customer.lastName} />
        <DetailItem label="Phone" value={customer.phoneNumber} />
        <DetailItem label="Registered" value={phDateTime(customer.registeredAtUtc)} />
        <DetailItem label="Status" value={customer.isActive ? 'Active' : 'Inactive'} />
        <DetailItem label="Delete request" value={del.status === 'None' ? 'None' : del.status} />
      </div>
      <div className="delete-box">
        <div>
          <span>Request to delete account</span>
          {del.status === 'None' ? (
            <p className="muted">No delete request yet. Record one if the customer asked through support.</p>
          ) : (
            <>
              <p>{del.reason || 'Customer requested account deletion.'}</p>
              {del.requestedAtUtc ? <small>Requested {phDateTime(del.requestedAtUtc)}</small> : null}
              {del.resolvedAtUtc ? <small>Resolved {phDateTime(del.resolvedAtUtc)}</small> : null}
              {del.resolutionNote ? <small>{del.resolutionNote}</small> : null}
            </>
          )}
        </div>
        {del.status === 'None' ? (
          <div className="delete-actions">
            <label className="field">
              <span>Reason</span>
              <input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Optional reason" />
            </label>
            <button className="btn danger" type="button" disabled={busy} onClick={() => void act(() => api.recordCustomerDelete(customerId, reason))}>
              Record delete request
            </button>
          </div>
        ) : null}
        {del.status === 'Pending' ? (
          <div className="delete-actions">
            <label className="field">
              <span>Admin note</span>
              <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Optional note" />
            </label>
            <div className="tag-row">
              <button className="btn danger" type="button" disabled={busy} onClick={() => void act(() => api.resolveCustomerDelete(customerId, true, note))}>
                Approve and deactivate
              </button>
              <button className="btn ghost" type="button" disabled={busy} onClick={() => void act(() => api.resolveCustomerDelete(customerId, false, note))}>
                Reject request
              </button>
            </div>
          </div>
        ) : null}
      </div>
      {error ? <p className="error">{error}</p> : null}
      <RidesReport
        sourceKey={`customer:${customerId}`}
        fetchRides={(opts) => api.customerRides(customerId, opts)}
        onOpenRide={setRideId}
      />
    </div>
  )
}

function TerritoriesPage() {
  const [q, setQ] = useState('')
  const [items, setItems] = useState<TerritoryListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.territories(q, page, pageSize)
        .then((data) => {
          setItems(data.items)
          setTotal(data.total)
          setError('')
        })
        .catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, page])

  return (
    <div className="card">
      <div className="toolbar">
        <h2 style={{ margin: 0 }}>Territories</h2>
        <div className="ac">
          <input
            value={q}
            onChange={(e) => { setQ(e.target.value); setPage(1) }}
            placeholder="Search province, city, or barangay"
          />
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Province</th>
              <th>Municipality</th>
              <th>Barangays</th>
              <th>Operators</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={4}>{q.trim() ? 'No territories match that search.' : 'No territories seeded yet.'}</td>
              </tr>
            ) : items.map((row) => (
              <tr key={row.id}>
                <td>{row.province}</td>
                <td>{row.municipality}</td>
                <td>
                  <div className="brgy-cell">
                    <p>
                      {row.barangays.length === 0
                        ? '—'
                        : `${row.barangays.join(', ')}${row.barangayCount > row.barangays.length ? '…' : ''}`}
                    </p>
                    <small>{row.barangayCount} barangay{row.barangayCount === 1 ? '' : 's'}</small>
                  </div>
                </td>
                <td>{row.operatorCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function FaresPage() {
  const [selection, setSelection] = useState<{ operatorId: string; municipalityId: string } | null>(null)
  if (selection) {
    return (
      <FareDetailPage
        operatorId={selection.operatorId}
        municipalityId={selection.municipalityId}
        onBack={() => setSelection(null)}
      />
    )
  }
  return <FareListPage onOpen={(operatorId, municipalityId) => setSelection({ operatorId, municipalityId })} />
}

function fareSummary(rates: FareRates | null) {
  if (!rates) {
    return '—'
  }
  return `${peso(rates.baseFare)} + ${peso(rates.perKm)}/km`
}

function surchargeSummary(rates: FareRates | null) {
  if (!rates) {
    return ''
  }
  const bits = (rates.surcharges ?? []).filter((item) => item.isActive).map((item) => `${item.name} ${peso(item.amount)}`)
  return bits.length > 0 ? bits.join(' · ') : 'No surcharges'
}

function FareRatesCell({ rates }: { rates: FareRates | null }) {
  if (!rates) {
    return '—'
  }
  return (
    <div>
      <div>{fareSummary(rates)}</div>
      <small className="muted">{surchargeSummary(rates)}</small>
    </div>
  )
}

function sampleFare(rates: FareRates | null, km: number) {
  const row = rates?.samples.find((item) => item.distanceKm === km)
  return row ? peso(row.fare) : '—'
}

function FareListPage({ onOpen }: { onOpen: (operatorId: string, municipalityId: string) => void }) {
  const [q, setQ] = useState('')
  const [vehicle, setVehicle] = useState<VehicleType | ''>('')
  const [items, setItems] = useState<OperatorFareListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.fares(q, vehicle || undefined, page, pageSize)
        .then((data) => {
          setItems(data.items)
          setTotal(data.total)
          setError('')
        })
        .catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, vehicle, page])

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Fare matrix</h2>
          <p className="muted" style={{ margin: '4px 0 0' }}>Related motorcycle and tricycle rates per municipality. Read-only here. Operators create and edit rates for each city they serve. Time windows use Philippine time.</p>
        </div>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
          <div className="chips">
            <button type="button" className={vehicle === '' ? 'on' : ''} onClick={() => { setVehicle(''); setPage(1) }}>All</button>
            <button type="button" className={vehicle === 'Motorcycle' ? 'on' : ''} onClick={() => { setVehicle('Motorcycle'); setPage(1) }}>Motorcycle</button>
            <button type="button" className={vehicle === 'Tricycle' ? 'on' : ''} onClick={() => { setVehicle('Tricycle'); setPage(1) }}>Tricycle</button>
          </div>
          <div className="ac">
            <input
              value={q}
              onChange={(e) => { setQ(e.target.value); setPage(1) }}
              placeholder="Search operator or municipality"
            />
          </div>
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Operator</th>
              <th>Municipality</th>
              <th>Commission</th>
              <th>Motorcycle</th>
              <th>Tricycle</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={5}>{q.trim() || vehicle ? 'No fare matrices match that filter.' : 'No fare matrices yet.'}</td>
              </tr>
            ) : items.map((row) => (
              <tr key={`${row.operatorId}|${row.municipalityId}`} className="clickable" onClick={() => onOpen(row.operatorId, row.municipalityId)}>
                <td>
                  <div className="person-cell">
                    <span>{row.operatorName}</span>
                    <StatusTag active={row.operatorActive} />
                  </div>
                </td>
                <td>{row.municipalityName}</td>
                <td>{commissionRates(row.motorcycleCommissionPercent, row.tricycleCommissionPercent)}</td>
                <td><FareRatesCell rates={row.motorcycle} /></td>
                <td><FareRatesCell rates={row.tricycle} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function FareDetailPage({
  operatorId,
  municipalityId,
  onBack,
}: {
  operatorId: string
  municipalityId: string
  onBack: () => void
}) {
  const [data, setData] = useState<OperatorFareMatrix | null>(null)
  const [selectedMunicipalityId, setSelectedMunicipalityId] = useState(municipalityId)
  const [error, setError] = useState('')

  useEffect(() => {
    api.operatorFares(operatorId, selectedMunicipalityId)
      .then(setData)
      .catch((err: Error) => setError(err.message))
  }, [operatorId, selectedMunicipalityId])

  if (error) {
    return <p className="error">{error}</p>
  }
  if (!data) {
    return <p>Loading fare matrix…</p>
  }

  return (
    <div className="card">
      <div className="panel-head">
        <div>
          <button className="btn tiny" type="button" onClick={onBack}>Back to fare matrix</button>
          <h2 style={{ marginTop: 12 }}>{data.operatorName}</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 520 }}>
            Read-only rates for {data.municipalityName ?? 'this municipality'}. Operators create and edit these.
          </p>
          {(data.municipalities?.length ?? 0) > 1 ? (
            <label className="field" style={{ maxWidth: 320, marginTop: 14, marginBottom: 0 }}>
              <span>Municipality</span>
              <select
                value={selectedMunicipalityId}
                onChange={(e) => setSelectedMunicipalityId(e.target.value)}
              >
                {data.municipalities.map((item) => (
                  <option key={item.id} value={item.id}>{item.name}</option>
                ))}
              </select>
            </label>
          ) : null}
        </div>
        <StatusTag active={data.operatorActive} />
      </div>
      <RelatedFareRatesTable data={data} />
      <RelatedSurchargeTable items={relatedSurcharges(data)} />
      <RelatedFareSamples data={data} />
    </div>
  )
}

function RelatedFareRatesTable({
  data,
  motorcycle,
  tricycle,
  linked,
  onLinked,
  onChange,
}: {
  data: OperatorFareMatrix
  motorcycle?: FareDraft
  tricycle?: FareDraft
  linked?: boolean
  onLinked?: (value: boolean) => void
  onChange?: (vehicle: VehicleType, patch: Partial<FareDraft>) => void
}) {
  const editable = !!onChange && !!motorcycle && !!tricycle
  const mc = motorcycle ?? fareDraft(data.motorcycle, data.motorcycleCommissionPercent, true)
  const trike = tricycle ?? fareDraft(data.tricycle, data.tricycleCommissionPercent, false)

  function patchTier(vehicle: VehicleType, tiers: FareTierDraft[]) {
    onChange?.(vehicle, { passengerTiers: tiers })
  }

  function updateTier(
    vehicle: VehicleType,
    current: FareTierDraft[],
    index: number,
    key: keyof FareTierDraft,
    value: string,
  ) {
    const next = current.map((tier, i) => (i === index ? { ...tier, [key]: value } : tier))
    patchTier(vehicle, next)
  }

  function addTier(vehicle: VehicleType, current: FareTierDraft[]) {
    const used = new Set(current.map((tier) => Number(tier.passengerCount) || 0))
    let nextCount = 1
    while (used.has(nextCount)) nextCount += 1
    const last = current[current.length - 1] ?? defaultTierDraft()
    patchTier(vehicle, [
      ...current,
      {
        ...last,
        passengerCount: String(nextCount),
      },
    ])
  }

  function removeTier(vehicle: VehicleType, current: FareTierDraft[], index: number) {
    if (current.length <= 1) return
    patchTier(vehicle, current.filter((_, i) => i !== index))
  }

  function motorcycleRateEditor(draft: FareDraft, locked: boolean) {
    const tier = draft.passengerTiers[0] ?? defaultTierDraft()
    function patchRate(key: keyof FareTierDraft, value: string) {
      onChange?.('Motorcycle', {
        passengerTiers: [{ ...tier, passengerCount: '1', [key]: value }],
      })
    }
    return (
      <div className="fare-tier-editor">
        <table className="fare-tier-table">
          <thead>
            <tr>
              <th>Base</th>
              <th>Incl. km</th>
              <th>Per km</th>
              <th>Min</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>
                <input value={tier.baseFare} disabled={locked} onChange={(e) => patchRate('baseFare', e.target.value)} />
              </td>
              <td>
                <input value={tier.includedKm} disabled={locked} onChange={(e) => patchRate('includedKm', e.target.value)} />
              </td>
              <td>
                <input value={tier.perKm} disabled={locked} onChange={(e) => patchRate('perKm', e.target.value)} />
              </td>
              <td>
                <input value={tier.minimumFare} disabled={locked} onChange={(e) => patchRate('minimumFare', e.target.value)} />
              </td>
            </tr>
          </tbody>
        </table>
        <p className="muted" style={{ margin: '8px 0 0', fontSize: 12 }}>Motorcycle is always 1 passenger.</p>
      </div>
    )
  }

  function motorcycleRateReadonly(rates: FareRates | null) {
    if (!rates) return '—'
    const tier = rates.passengerTiers?.find((item) => item.passengerCount === 1)
      ?? rates.passengerTiers?.[0]
      ?? {
        baseFare: rates.baseFare,
        perKm: rates.perKm,
        minimumFare: rates.minimumFare,
        includedKm: rates.includedKm,
      }
    return (
      <div className="fare-tier-readonly">
        <div>
          {peso(tier.baseFare)} / {tier.includedKm} km + {peso(tier.perKm)}/km
          {tier.minimumFare > 0 ? ` · min ${peso(tier.minimumFare)}` : ''}
        </div>
      </div>
    )
  }

  function tiersEditor(vehicle: VehicleType, draft: FareDraft, locked: boolean) {
    const tiers = draft.passengerTiers.length ? draft.passengerTiers : [defaultTierDraft()]
    return (
      <div className="fare-tier-editor">
        <table className="fare-tier-table">
          <thead>
            <tr>
              <th>Persons</th>
              <th>Base</th>
              <th>Incl. km</th>
              <th>Per km</th>
              <th>Min</th>
              {editable ? <th aria-label="Actions" /> : null}
            </tr>
          </thead>
          <tbody>
            {tiers.map((tier, index) => (
              <tr key={`${vehicle}-${index}`}>
                <td>
                  <input
                    value={tier.passengerCount}
                    disabled={locked}
                    onChange={(e) => updateTier(vehicle, tiers, index, 'passengerCount', e.target.value)}
                  />
                </td>
                <td>
                  <input
                    value={tier.baseFare}
                    disabled={locked}
                    onChange={(e) => updateTier(vehicle, tiers, index, 'baseFare', e.target.value)}
                  />
                </td>
                <td>
                  <input
                    value={tier.includedKm}
                    disabled={locked}
                    onChange={(e) => updateTier(vehicle, tiers, index, 'includedKm', e.target.value)}
                  />
                </td>
                <td>
                  <input
                    value={tier.perKm}
                    disabled={locked}
                    onChange={(e) => updateTier(vehicle, tiers, index, 'perKm', e.target.value)}
                  />
                </td>
                <td>
                  <input
                    value={tier.minimumFare}
                    disabled={locked}
                    onChange={(e) => updateTier(vehicle, tiers, index, 'minimumFare', e.target.value)}
                  />
                </td>
                {editable ? (
                  <td className="fare-tier-actions">
                    <button
                      type="button"
                      className="btn tiny"
                      disabled={locked || tiers.length <= 1}
                      onClick={() => removeTier(vehicle, tiers, index)}
                    >
                      Remove
                    </button>
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
        {editable ? (
          <button type="button" className="btn tiny" disabled={locked} onClick={() => addTier(vehicle, tiers)} style={{ width: 'auto', justifySelf: 'start' }}>
            Add person tier
          </button>
        ) : null}
      </div>
    )
  }

  function tiersReadonly(rates: FareRates | null) {
    if (!rates) return '—'
    const tiers = rates.passengerTiers?.length
      ? rates.passengerTiers
      : [{
          passengerCount: 1,
          baseFare: rates.baseFare,
          perKm: rates.perKm,
          minimumFare: rates.minimumFare,
          includedKm: rates.includedKm,
        }]
    return (
      <div className="fare-tier-readonly">
        {tiers.map((tier) => (
          <div key={tier.passengerCount}>
            <strong>{tier.passengerCount} person{tier.passengerCount === 1 ? '' : 's'}</strong>
            {' · '}
            {peso(tier.baseFare)} / {tier.includedKm} km + {peso(tier.perKm)}/km
            {tier.minimumFare > 0 ? ` · min ${peso(tier.minimumFare)}` : ''}
          </div>
        ))}
      </div>
    )
  }

  function vehiclePanel(
    vehicle: VehicleType,
    draft: FareDraft,
    systemPercent: number,
    rates: FareRates | null,
    locked: boolean,
  ) {
    const total = commissionSum(systemPercent, draft)
    const motorcycle = vehicle === 'Motorcycle'
    return (
      <section className="fare-vehicle-panel">
        <header className="fare-vehicle-panel-head">
          <h3>{vehicle}</h3>
          {editable ? (
            <div className="chips">
              <button type="button" className={draft.isActive ? 'on' : ''} disabled={locked} onClick={() => onChange?.(vehicle, { isActive: true })}>Active</button>
              <button type="button" className={!draft.isActive ? 'on' : ''} disabled={locked} onClick={() => onChange?.(vehicle, { isActive: false })}>Off</button>
            </div>
          ) : (
            rates ? <StatusTag active={rates.isActive} /> : <span className="muted">—</span>
          )}
        </header>

        <div className="fare-vehicle-block">
          <div className="fare-vehicle-label">{motorcycle ? 'Rates' : 'Passenger tiers'}</div>
          {editable
            ? (motorcycle ? motorcycleRateEditor(draft, locked) : tiersEditor(vehicle, draft, locked))
            : (motorcycle ? motorcycleRateReadonly(rates) : tiersReadonly(rates))}
        </div>

        <div className="fare-vehicle-block">
          <div className="fare-vehicle-label">Commission</div>
          <div className="fare-commission-grid">
            <div>
              <small>System</small>
              <strong className="fare-matrix-system-value">{percent(systemPercent)}</strong>
            </div>
            <div>
              <small>Operator</small>
              {editable ? (
                <input
                  value={draft.operatorCommissionPercent}
                  disabled={locked}
                  onChange={(e) => onChange?.(vehicle, { operatorCommissionPercent: e.target.value })}
                />
              ) : (
                <strong>{rates ? percent(rates.operatorCommissionPercent) : '—'}</strong>
              )}
            </div>
            <div>
              <small>Driver</small>
              {editable ? (
                <input
                  value={draft.driverCommissionPercent}
                  disabled={locked}
                  onChange={(e) => onChange?.(vehicle, { driverCommissionPercent: e.target.value })}
                />
              ) : (
                <strong>{rates ? percent(rates.driverCommissionPercent) : '—'}</strong>
              )}
            </div>
            <div>
              <small>Total</small>
              <strong className={total === 100 ? '' : 'error'}>{percent(total)}</strong>
            </div>
          </div>
          <p className="muted" style={{ margin: '8px 0 0', fontSize: 12 }}>System is set by Super Admin. The three shares must add up to 100%.</p>
        </div>

        <div className="fare-vehicle-block">
          <div className="fare-vehicle-label">Surcharges</div>
          <div>{surchargeSummary(rates)}</div>
        </div>
      </section>
    )
  }

  return (
    <div className="fare-matrix-layout">
      {editable ? (
        <div className="chips" style={{ marginBottom: 14 }}>
          <button type="button" className={linked ? 'on' : ''} onClick={() => onLinked?.(true)}>Same rates for both</button>
          <button type="button" className={!linked ? 'on' : ''} onClick={() => onLinked?.(false)}>Set each vehicle</button>
        </div>
      ) : null}
      <div className="fare-vehicle-panels">
        {vehiclePanel('Motorcycle', mc, data.motorcycleCommissionPercent, data.motorcycle, false)}
        {vehiclePanel('Tricycle', trike, data.tricycleCommissionPercent, data.tricycle, !!linked && editable)}
      </div>
    </div>
  )
}

function surchargeKindLabel(kind: SurchargeKind) {
  return kind === 'TimeWindow' ? 'Time window' : 'Date range'
}

function RelatedSurchargeTable({
  items,
  onEdit,
  onDelete,
  onToggle,
}: {
  items: RelatedSurcharge[]
  onEdit?: (item: RelatedSurcharge) => void
  onDelete?: (item: RelatedSurcharge) => void
  onToggle?: (item: RelatedSurcharge, isActive: boolean) => void
}) {
  return (
    <div className="table-wrap" style={{ marginTop: 18 }}>
      <p className="muted">Multiple surcharges allowed. Use a daily time window or a date range. Each row can be Active or Off. Times are Philippine time.</p>
      <table>
        <thead>
          <tr>
            <th>Name</th>
            <th>Applies to</th>
            <th>Kind</th>
            <th>Amount</th>
            <th>When</th>
            <th>Status</th>
            {onEdit || onDelete ? <th></th> : null}
          </tr>
        </thead>
        <tbody>
          {items.length === 0 ? (
            <tr>
              <td colSpan={onEdit || onDelete ? 7 : 6}>No surcharges yet.</td>
            </tr>
          ) : items.map((item) => (
            <tr key={`${item.vehicleType}-${item.id}`}>
              <td>{item.name}</td>
              <td><VehicleTag type={item.vehicleType} /></td>
              <td>{surchargeKindLabel(item.kind)}</td>
              <td>{peso(item.amount)}</td>
              <td>{surchargeLine(item)}</td>
              <td>
                {onToggle ? (
                  <div className="chips">
                    <button type="button" className={item.isActive ? 'on' : ''} onClick={() => onToggle(item, true)}>Active</button>
                    <button type="button" className={!item.isActive ? 'on' : ''} onClick={() => onToggle(item, false)}>Off</button>
                  </div>
                ) : (
                  <StatusTag active={item.isActive} />
                )}
              </td>
              {onEdit || onDelete ? (
                <td>
                  {onEdit ? <button className="btn tiny" type="button" onClick={() => onEdit(item)}>Edit</button> : null}
                  {onDelete ? <button className="btn tiny danger" type="button" style={{ marginLeft: 8 }} onClick={() => onDelete(item)}>Remove</button> : null}
                </td>
              ) : null}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function RelatedFareSamples({ data }: { data: OperatorFareMatrix }) {
  const samples = data.motorcycle?.samples ?? data.tricycle?.samples ?? []
  if (samples.length === 0) {
    return <p className="muted" style={{ marginTop: 18 }}>No distance samples yet. Save rates to see motorcycle and tricycle side by side.</p>
  }
  return (
    <div className="table-wrap" style={{ marginTop: 18 }}>
      <p className="muted">Distance samples exclude time-window and date-range surcharges.</p>
      <table>
        <thead>
          <tr>
            <th>Distance</th>
            <th>Motorcycle</th>
            <th>Tricycle</th>
          </tr>
        </thead>
        <tbody>
          {samples.map((sample) => (
            <tr key={sample.distanceKm}>
              <td>{sample.distanceKm.toFixed(0)} km</td>
              <td>{sampleFare(data.motorcycle, sample.distanceKm)}</td>
              <td>{sampleFare(data.tricycle, sample.distanceKm)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function BillStatusTag({ status }: { status: BillStatus }) {
  return <span className="tag kind">{status}</span>
}

function BillRecordsList({ bills, onOpen }: { bills: OperatorBill[]; onOpen: (id: string) => void }) {
  if (bills.length === 0) {
    return <p className="muted">No billing records yet.</p>
  }
  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Billing number</th>
            <th>Status</th>
            <th>Amount</th>
          </tr>
        </thead>
        <tbody>
          {bills.map((bill) => (
            <tr key={bill.id} className="clickable" onClick={() => onOpen(bill.id)}>
              <td>
                <strong>{bill.number}</strong>
                <div className="muted">Issued {phDateTime(bill.createdAtUtc)}</div>
              </td>
              <td><BillStatusTag status={bill.status} /></td>
              <td><strong>{peso(bill.amount)}</strong></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function BillRecordDetail({
  bill,
  onBack,
  backLabel = 'Back to billing records',
}: {
  bill: OperatorBill
  onBack: () => void
  backLabel?: string
}) {
  return (
    <div className="card">
      <div className="panel-head">
        <div>
          <button className="btn tiny" type="button" onClick={onBack}>{backLabel}</button>
          <h2 style={{ marginTop: 12 }}>{bill.number}</h2>
          <p className="muted">
            {bill.tripCount} trip{bill.tripCount === 1 ? '' : 's'} · {phDateTime(bill.periodFromUtc)} – {phDateTime(bill.periodToUtc)}
          </p>
        </div>
        <div className="tag-row" style={{ marginTop: 0 }}>
          <BillStatusTag status={bill.status} />
          <span className="tag kind">{peso(bill.amount)}</span>
        </div>
      </div>
      {bill.note ? <p className="muted">{bill.note}</p> : null}
      {bill.disabledOperator ? <p className="muted">Operator was disabled when this bill was issued.</p> : null}
      <div className="table-wrap" style={{ marginTop: 10 }}>
        <table>
          <thead>
            <tr>
              <th>Date/time</th>
              <th>Rider</th>
              <th>Booking number</th>
              <th>Fare</th>
              <th>Commission</th>
            </tr>
          </thead>
          <tbody>
            {(bill.trips ?? []).length === 0 ? (
              <tr><td colSpan={5}>No trip lines on this bill.</td></tr>
            ) : bill.trips.map((trip, index) => (
              <tr key={`${bill.id}-${trip.bookingNumber}-${index}`}>
                <td>{phDateTime(trip.atUtc)}</td>
                <td>{trip.riderName}</td>
                <td>{trip.bookingNumber}</td>
                <td>{peso(trip.fare)}</td>
                <td><strong>{peso(trip.amount)}</strong></td>
              </tr>
            ))}
          </tbody>
          {(bill.trips ?? []).length > 0 ? (
            <tfoot>
              <tr className="table-total">
                <td colSpan={3}>Total commission</td>
                <td>{peso(bill.trips.reduce((sum, trip) => sum + trip.fare, 0))}</td>
                <td><strong>{peso(bill.amount)}</strong></td>
              </tr>
            </tfoot>
          ) : null}
        </table>
      </div>
    </div>
  )
}

function BillingPage() {
  const [operatorId, setOperatorId] = useState<string | null>(null)
  if (operatorId) {
    return <BillingDetailPage operatorId={operatorId} onBack={() => setOperatorId(null)} />
  }
  return <BillingListPage onOpen={setOperatorId} />
}

function BillingListPage({ onOpen }: { onOpen: (id: string) => void }) {
  const [q, setQ] = useState('')
  const [items, setItems] = useState<BillingOperator[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.billingOperators(q, page, pageSize)
        .then((data) => {
          setItems(data.items)
          setTotal(data.total)
          setError('')
        })
        .catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, page])

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Billing</h2>
          <p className="muted" style={{ margin: '4px 0 0' }}>
            Active Operators sorted by pending platform commission, highest first.
          </p>
        </div>
        <div className="ac">
          <input
            value={q}
            onChange={(e) => { setQ(e.target.value); setPage(1) }}
            placeholder="Search operator"
          />
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Operator</th>
              <th>Commission</th>
              <th>Pending trips</th>
              <th>Pending commission</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={4}>{q.trim() ? 'No active Operators match that search.' : 'No active Operators yet.'}</td>
              </tr>
            ) : items.map((row) => (
              <tr key={row.operatorId} className="clickable" onClick={() => onOpen(row.operatorId)}>
                <td>
                  <div className="person-cell">
                    <Avatar name={row.companyName} photoUrl={row.profilePhotoUrl} />
                    <div>
                      <strong>{row.companyName}</strong>
                      <div className="muted">{row.contactName} · {row.contactPhone}</div>
                    </div>
                  </div>
                </td>
                <td>{commissionRates(row.motorcycleCommissionPercent, row.tricycleCommissionPercent)}</td>
                <td>{row.pendingTripCount}</td>
                <td>
                  <strong>{peso(row.pendingCommission)}</strong>
                  <div className="muted">
                    Motorcycle {peso(row.pendingMotorcycle)} · Tricycle {peso(row.pendingTricycle)}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function BillingDetailPage({ operatorId, onBack }: { operatorId: string; onBack: () => void }) {
  const [data, setData] = useState<BillingOperatorDetail | null>(null)
  const [billId, setBillId] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [note, setNote] = useState('')
  const [disableOperator, setDisableOperator] = useState(false)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.billingOperator(operatorId)
      .then(setData)
      .catch((err: Error) => setError(err.message))
  }, [operatorId])

  const selectedBill = billId ? data?.bills.find((bill) => bill.id === billId) ?? null : null

  if (selectedBill) {
    return (
      <BillRecordDetail
        bill={selectedBill}
        onBack={() => setBillId(null)}
        backLabel="Back to operator billing"
      />
    )
  }

  async function createBill() {
    if (!data || data.pendingCommission <= 0) {
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const next = await api.createBill(operatorId, disableOperator, note)
      setData(next)
      setNote('')
      setDisableOperator(false)
      setNotice(
        disableOperator
          ? `Bill ${next.bills[0]?.number ?? ''} issued and the Operator was notified. This Operator and its riders are disabled and will not receive bookings.`
          : `Bill ${next.bills[0]?.number ?? ''} issued and the Operator was notified.`,
      )
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not create billing record.')
    } finally {
      setBusy(false)
    }
  }

  if (error && !data) {
    return <p className="error">{error}</p>
  }
  if (!data) {
    return <p>Loading billing…</p>
  }

  return (
    <div className="card">
      <div className="panel-head">
        <div className="person-cell" style={{ alignItems: 'flex-start' }}>
          <Avatar name={data.companyName} photoUrl={data.profilePhotoUrl} size={56} />
          <div>
            <button className="btn tiny" type="button" onClick={onBack}>Back to billing</button>
            <h2 style={{ marginTop: 12 }}>{data.companyName}</h2>
            <p>{data.contactName} · {data.contactPhone}</p>
            <div className="tag-row" style={{ marginTop: 8 }}>
              <StatusTag active={data.isActive} />
              <VehicleTag type="Motorcycle" />
              <VehicleTag type="Tricycle" />
            </div>
          </div>
        </div>
      </div>

      <div className="fare-cards">
        <div className="detail-card">
          <span>Pending commission</span>
          <p className="detail-name" style={{ marginTop: 10 }}>{peso(data.pendingCommission)}</p>
          <p className="muted">{data.pendingTripCount} unbilled completed trip{data.pendingTripCount === 1 ? '' : 's'}</p>
          {data.oldestUnbilledUtc && data.newestUnbilledUtc ? (
            <p className="muted">
              {phDate(data.oldestUnbilledUtc)} – {phDate(data.newestUnbilledUtc)}
            </p>
          ) : null}
        </div>
        <div className="detail-card">
          <span>By vehicle</span>
          <p className="muted" style={{ marginTop: 10 }}>Motorcycle {percent(data.motorcycleCommissionPercent)} · {peso(data.pendingMotorcycle)}</p>
          <p className="muted">Tricycle {percent(data.tricycleCommissionPercent)} · {peso(data.pendingTricycle)}</p>
          <p className="muted">{data.riderCount} rider{data.riderCount === 1 ? '' : 's'}</p>
        </div>
      </div>

      {notice ? <p className="ok">{notice}</p> : null}
      {error ? <p className="error">{error}</p> : null}

      {data.isActive && data.pendingCommission > 0 ? (
        <section className={`form-section${disableOperator ? ' danger' : ''}`} style={{ marginTop: 16 }}>
          <h3>Create bill</h3>
          <p className="form-hint">
            This issues a billing record for {peso(data.pendingCommission)} and notifies the Operator.
          </p>
          <label className="field">
            <span>Note (optional)</span>
            <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Shown on the billing record" />
          </label>
          <label className="check">
            <input type="checkbox" checked={disableOperator} onChange={(e) => setDisableOperator(e.target.checked)} />
            <span>
              Disable this Operator and all of its riders. They will not receive bookings.
            </span>
          </label>
          <div style={{ display: 'flex', gap: 10, maxWidth: 360, marginTop: 14 }}>
            <button className={`btn${disableOperator ? ' danger' : ''}`} type="button" disabled={busy} onClick={() => void createBill()}>
              {busy ? 'Billing…' : 'Create bill'}
            </button>
          </div>
        </section>
      ) : (
        <p className="muted" style={{ marginTop: 16 }}>
          {data.isActive ? 'No pending commission to bill.' : 'This Operator is disabled and will not receive bookings.'}
        </p>
      )}

      <div className="toolbar" style={{ marginTop: 18 }}>
        <h3 style={{ margin: 0 }}>Billing records</h3>
      </div>
      <BillRecordsList bills={data.bills} onOpen={setBillId} />
    </div>
  )
}

function AnnouncementsPage() {
  const [q, setQ] = useState('')
  const [items, setItems] = useState<Announcement[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [form, setForm] = useState({
    title: '',
    body: '',
    forOperators: true,
    forRiders: true,
    forCustomers: true,
    startsAt: '',
    endsAt: '',
  })
  const pageSize = 10

  async function load(nextPage = page, nextQ = q) {
    const data = await api.announcements(nextQ, nextPage, pageSize)
    setItems(data.items)
    setTotal(data.total)
  }

  useEffect(() => {
    const handle = window.setTimeout(() => {
      load(page, q).then(() => setError('')).catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, page])

  async function publish() {
    if (!form.title.trim() || !form.body.trim()) {
      setError('Title and body are required.')
      return
    }
    if (!form.forOperators && !form.forRiders && !form.forCustomers) {
      setError('Choose at least one audience.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const notifyOperators = form.forOperators
      await api.createAnnouncement({
        title: form.title.trim(),
        body: form.body.trim(),
        forOperators: form.forOperators,
        forRiders: form.forRiders,
        forCustomers: form.forCustomers,
        startsAtUtc: fromPhInput(form.startsAt),
        endsAtUtc: fromPhInput(form.endsAt),
      })
      setForm({
        title: '',
        body: '',
        forOperators: true,
        forRiders: true,
        forCustomers: true,
        startsAt: '',
        endsAt: '',
      })
      setPage(1)
      await load(1, q)
      setNotice(notifyOperators
        ? 'Announcement published. Operators were notified.'
        : 'Announcement published.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not publish announcement.')
    } finally {
      setBusy(false)
    }
  }

  async function toggle(item: Announcement) {
    await api.setAnnouncementActive(item.id, !item.isActive)
    await load()
  }

  return (
    <div className="form-sections">
      <form
        className="card"
        onSubmit={(e) => {
          e.preventDefault()
          void publish()
        }}
      >
        <div className="panel-head">
          <div>
            <h2 style={{ margin: 0 }}>Publish announcement</h2>
            <p className="muted" style={{ margin: '4px 0 0' }}>
              Platform banner for Operators, riders, and customers. Schedule uses Philippine time.
            </p>
          </div>
        </div>
        <div className="form-grid">
          <label className="field wide">
            <span>Title</span>
            <input maxLength={120} value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
          </label>
          <label className="field wide">
            <span>Body</span>
            <textarea maxLength={2000} value={form.body} onChange={(e) => setForm({ ...form, body: e.target.value })} />
          </label>
          <div className="field wide">
            <span>Audience</span>
            <div className="check-row">
              <label className="check">
                <input type="checkbox" checked={form.forOperators} onChange={(e) => setForm({ ...form, forOperators: e.target.checked })} />
                <span>Operators</span>
              </label>
              <label className="check">
                <input type="checkbox" checked={form.forRiders} onChange={(e) => setForm({ ...form, forRiders: e.target.checked })} />
                <span>Riders</span>
              </label>
              <label className="check">
                <input type="checkbox" checked={form.forCustomers} onChange={(e) => setForm({ ...form, forCustomers: e.target.checked })} />
                <span>Customers</span>
              </label>
            </div>
          </div>
          <label className="field">
            <span>Starts (PH time, optional)</span>
            <input type="datetime-local" value={form.startsAt} onChange={(e) => setForm({ ...form, startsAt: e.target.value })} />
          </label>
          <label className="field">
            <span>Ends (PH time, optional)</span>
            <input type="datetime-local" value={form.endsAt} onChange={(e) => setForm({ ...form, endsAt: e.target.value })} />
          </label>
        </div>
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}
        <div style={{ display: 'flex', gap: 10, maxWidth: 280 }}>
          <button className="btn" type="submit" disabled={busy}>
            {busy ? 'Publishing…' : 'Publish'}
          </button>
        </div>
      </form>

      <div className="card">
        <div className="toolbar">
          <h2 style={{ margin: 0 }}>Announcement inbox</h2>
          <div className="ac">
            <input
              value={q}
              onChange={(e) => { setQ(e.target.value); setPage(1) }}
              placeholder="Search title or body"
            />
          </div>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Announcement</th>
                <th>Audience</th>
                <th>Schedule</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan={5}>{q.trim() ? 'No announcements match that search.' : 'No announcements yet.'}</td>
                </tr>
              ) : items.map((item) => (
                <tr key={item.id}>
                  <td>
                    <strong>{item.title}</strong>
                    <div className="muted">{item.body}</div>
                    <small className="muted">Published {phDateTime(item.createdAtUtc)}</small>
                  </td>
                  <td>{audienceLabel(item)}</td>
                  <td>
                    {item.startsAtUtc || item.endsAtUtc ? (
                      <div>
                        {item.startsAtUtc ? <div>From {phDateTime(item.startsAtUtc)}</div> : null}
                        {item.endsAtUtc ? <div>Until {phDateTime(item.endsAtUtc)}</div> : <div className="muted">No end</div>}
                      </div>
                    ) : (
                      <span className="muted">Immediate</span>
                    )}
                  </td>
                  <td><StatusTag active={item.isActive && !isEnded(item)} /></td>
                  <td>
                    <button className={`btn tiny${item.isActive ? ' danger' : ''}`} type="button" onClick={() => void toggle(item)}>
                      {item.isActive ? 'Deactivate' : 'Activate'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
      </div>
    </div>
  )
}

function audienceLabel(item: Announcement) {
  return [
    item.forOperators ? 'Operators' : null,
    item.forRiders ? 'Riders' : null,
    item.forCustomers ? 'Customers' : null,
  ].filter(Boolean).join(' · ') || '—'
}

function isEnded(item: Announcement) {
  return !!item.endsAtUtc && new Date(item.endsAtUtc).getTime() < Date.now()
}

function fromPhInput(value: string) {
  const raw = value.trim()
  if (!raw) {
    return null
  }
  const normalized = raw.length === 16 ? `${raw}:00` : raw
  const stamp = new Date(`${normalized}+08:00`)
  return Number.isNaN(stamp.getTime()) ? null : stamp.toISOString()
}

function toPhInput(value: string | null | undefined) {
  if (!value) {
    return ''
  }
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

function SettingsPage({
  theme,
  onTheme,
  section,
  onSection,
  onBranding,
}: {
  theme: Theme
  onTheme: () => void
  me: Me
  section: SettingsSection
  onSection: (section: SettingsSection) => void
  branding: BrandingConfig | null
  onBranding: (brand: BrandingConfig) => void
}) {
  return (
    <div className="form-sections">
      <div className="chips" style={{ marginBottom: 4 }}>
        <button type="button" className={section === 'general' ? 'on' : ''} onClick={() => onSection('general')}>
          General
        </button>
        <button type="button" className={section === 'branding' ? 'on' : ''} onClick={() => onSection('branding')}>
          Branding
        </button>
      </div>
      {section === 'branding' ? (
        <BrandingSettingsPage onApplied={onBranding} />
      ) : (
        <div className="grid-2">
          <div className="card">
            <h2>Vehicle types</h2>
            <p>Locked for this product. Operators will assign one of these when they create a rider.</p>
            <div className="chips" style={{ marginTop: 12 }}>
              <button type="button" className="on">Motorcycle</button>
              <button type="button" className="on">Tricycle</button>
            </div>
          </div>
          <div className="card">
            <h2>Appearance</h2>
            <p>Same dashboard in dark and light. Choice is saved on this browser.</p>
            <button className="btn" type="button" onClick={onTheme} style={{ maxWidth: 220, marginTop: 12 }}>
              Switch to {theme === 'dark' ? 'light' : 'dark'} mode
            </button>
          </div>
        </div>
      )}
    </div>
  )
}

function RolesPage() {
  return (
    <div className="form-sections">
      <AccessSettings section="roles" />
    </div>
  )
}

function AdminUsersPage() {
  return (
    <div className="form-sections">
      <AccessSettings section="users" />
    </div>
  )
}

function AdminProfilePage({ me }: { me: Me }) {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (newPassword.trim().length < 6) {
      setError('New password must be at least 6 characters.')
      return
    }
    if (newPassword !== confirmPassword) {
      setError('New passwords do not match.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const result = await api.changeAdminPassword(currentPassword, newPassword.trim())
      setNotice(result.message)
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not change password.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="form-sections">
      <div className="card">
        <div className="panel-head">
          <div className="person-cell" style={{ alignItems: 'center' }}>
            <div className="avatar" style={{ width: 56, height: 56, fontSize: 22 }}>{me.fullName.slice(0, 1)}</div>
            <div>
              <h2 style={{ margin: 0 }}>{me.fullName}</h2>
              <p className="muted" style={{ margin: '4px 0 0' }}>{me.phoneNumber}</p>
              <div className="tag-row" style={{ marginTop: 8 }}>
                <span className="tag status active">{me.isMainAdmin ? 'Administrator' : me.accessGroupName || 'Admin'}</span>
              </div>
            </div>
          </div>
        </div>
        <p className="muted">
          {me.isMainAdmin
            ? 'You have access to every module. Change your password here.'
            : `Your role is ${me.accessGroupName || 'Admin'}. Change your password here.`}
        </p>
      </div>
      <form className="card" onSubmit={submit}>
        <h2 style={{ marginTop: 0 }}>Change password</h2>
        <p className="muted">Use your current password, then choose a new one of at least 6 characters.</p>
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}
        <div className="form-grid">
          <label className="field">
            <span>Current password</span>
            <input type="password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} autoComplete="current-password" />
          </label>
          <label className="field">
            <span>New password</span>
            <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} autoComplete="new-password" />
          </label>
          <label className="field">
            <span>Confirm new password</span>
            <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
          </label>
        </div>
        <div style={{ maxWidth: 220 }}>
          <button className="btn" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save password'}</button>
        </div>
      </form>
    </div>
  )
}

function AccessSettings({ section, mode = 'admin' }: { section: 'roles' | 'users'; mode?: 'admin' | 'operator' }) {
  const operatorMode = mode === 'operator'
  const peopleLabel = operatorMode ? 'Employees' : 'Admin users'
  const personLabel = operatorMode ? 'employee' : 'user'
  const isMain = (row: AccessStaff) => (operatorMode ? !!row.isMainOperator : row.isMainAdmin)
  const [pages, setPages] = useState<AccessPage[]>([])
  const [groups, setGroups] = useState<AccessGroup[]>([])
  const [users, setUsers] = useState<AccessStaff[]>([])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [groupId, setGroupId] = useState<string | null>(null)
  const [groupForm, setGroupForm] = useState({ name: '', description: '', pages: [] as PageId[] })
  const [userId, setUserId] = useState<string | null>(null)
  const [userForm, setUserForm] = useState({ fullName: '', phone: '', accessGroupId: '', groupQuery: '', password: '', confirmPassword: '' })
  const [resetUserId, setResetUserId] = useState<string | null>(null)
  const [resetPassword, setResetPassword] = useState('')
  const [roleQueryByUser, setRoleQueryByUser] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)

  async function load() {
    const [pageRows, groupRows, userRows] = await Promise.all(
      operatorMode
        ? [api.operatorAccessPages(), api.operatorAccessGroups(), api.operatorAccessUsers()]
        : [api.accessPages(), api.accessGroups(), api.accessUsers()],
    )
    setPages(pageRows)
    setGroups(groupRows)
    setUsers(userRows)
  }

  useEffect(() => {
    load().then(() => setError('')).catch((err: Error) => setError(err.message))
  }, [])

  useEffect(() => {
    setError('')
    setNotice('')
  }, [section])

  function resetGroup() {
    setGroupId(null)
    setGroupForm({ name: '', description: '', pages: [] })
  }

  function resetUser() {
    setUserId(null)
    setUserForm({ fullName: '', phone: '', accessGroupId: '', groupQuery: '', password: '', confirmPassword: '' })
  }

  async function saveGroup() {
    if (!groupForm.name.trim() || groupForm.pages.length === 0) {
      setError('Role name and at least one module are required.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const body = {
        name: groupForm.name.trim(),
        description: groupForm.description.trim(),
        pages: groupForm.pages,
      }
      if (groupId) {
        if (operatorMode) await api.updateOperatorAccessGroup(groupId, body)
        else await api.updateAccessGroup(groupId, body)
        setNotice('Role updated.')
      } else {
        if (operatorMode) await api.createOperatorAccessGroup(body)
        else await api.createAccessGroup(body)
        setNotice('Role created.')
      }
      resetGroup()
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save role.')
    } finally {
      setBusy(false)
    }
  }

  async function removeGroup(id: string) {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      if (operatorMode) await api.deleteOperatorAccessGroup(id)
      else await api.deleteAccessGroup(id)
      if (groupId === id) {
        resetGroup()
      }
      setNotice('Role deleted.')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete role.')
    } finally {
      setBusy(false)
    }
  }

  async function saveUser() {
    if (!userForm.fullName.trim() || !userForm.phone.trim() || !userForm.accessGroupId) {
      setError('Name, phone, password, and role are required.')
      return
    }
    if (!userId && userForm.password.trim().length < 6) {
      setError('Set a password of at least 6 characters.')
      return
    }
    if (userForm.password.trim().length > 0 && userForm.password !== userForm.confirmPassword) {
      setError('Passwords do not match.')
      return
    }
    if (userId && userForm.password.trim().length > 0 && userForm.password.trim().length < 6) {
      setError('Password must be at least 6 characters.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const body = {
        fullName: userForm.fullName.trim(),
        phone: userForm.phone.trim(),
        accessGroupId: userForm.accessGroupId,
        password: userForm.password.trim(),
      }
      if (userId) {
        const payload = {
          fullName: body.fullName,
          phone: body.phone,
          accessGroupId: body.accessGroupId,
          ...(body.password ? { password: body.password } : {}),
        }
        if (operatorMode) await api.updateOperatorAccessUser(userId, payload)
        else await api.updateAccessUser(userId, payload)
        setNotice(operatorMode ? 'Employee updated.' : 'User updated.')
      } else {
        if (operatorMode) await api.createOperatorAccessUser(body)
        else await api.createAccessUser(body)
        setNotice(operatorMode
          ? 'Employee created. They sign in with phone and password.'
          : 'User created. They sign in with phone and password.')
      }
      resetUser()
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save user.')
    } finally {
      setBusy(false)
    }
  }

  async function toggleUser(row: AccessStaff) {
    if (isMain(row)) {
      return
    }
    if (operatorMode) await api.setOperatorAccessUserActive(row.id, !row.isActive)
    else await api.setAccessUserActive(row.id, !row.isActive)
    await load()
  }

  async function resetUserPassword(row: AccessStaff) {
    if (resetUserId !== row.id) {
      setResetUserId(row.id)
      setResetPassword('')
      return
    }
    if (resetPassword.trim().length < 6) {
      setError('New password must be at least 6 characters.')
      return
    }
    setError('')
    setNotice('')
    try {
      const result = operatorMode
        ? await api.resetOperatorAccessUserPassword(row.id, resetPassword.trim())
        : await api.resetAccessUserPassword(row.id, resetPassword.trim())
      setNotice(result.message)
      setResetUserId(null)
      setResetPassword('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not reset password.')
    }
  }

  async function changeUserRole(row: AccessStaff, accessGroupId: string) {
    if (isMain(row) || !accessGroupId || accessGroupId === row.accessGroupId) {
      return
    }
    setError('')
    setNotice('')
    try {
      const payload = {
        fullName: row.fullName,
        phone: row.phoneNumber,
        accessGroupId,
      }
      if (operatorMode) await api.updateOperatorAccessUser(row.id, payload)
      else await api.updateAccessUser(row.id, payload)
      const roleName = groups.find((item) => item.id === accessGroupId)?.name ?? 'the selected role'
      setNotice(`${row.fullName} is now ${roleName}.`)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not change role.')
    }
  }

  const selectedRole = groups.find((item) => item.id === userForm.accessGroupId)
  const roleItems = groups.map((role) => ({ id: role.id, name: role.name }))

  function roleModules(roleId: string) {
    const role = groups.find((item) => item.id === roleId)
    if (!role) {
      return 'No modules'
    }
    return role.pages.map((id) => pages.find((item) => item.id === id)?.label ?? id).join(' · ') || 'No modules'
  }

  function toggleModule(id: PageId) {
    setGroupForm((current) => ({
      ...current,
      pages: current.pages.includes(id)
        ? current.pages.filter((page) => page !== id)
        : [...current.pages, id],
    }))
  }

  return (
    <>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      {section === 'roles' ? (
      <form
        className="card"
        onSubmit={(e) => {
          e.preventDefault()
          void saveGroup()
        }}
      >
        <div className="panel-head">
          <div>
            <h2 style={{ margin: 0 }}>{groupId ? 'Edit role' : 'Roles'}</h2>
            <p className="muted" style={{ margin: '4px 0 0' }}>
              Create a role and tick the modules it can open. Then assign it in {peopleLabel}.
            </p>
          </div>
          {groupId ? (
            <button className="btn tiny" type="button" onClick={resetGroup}>New role</button>
          ) : null}
        </div>
        <div className="form-grid">
          <label className="field">
            <span>Role name</span>
            <input value={groupForm.name} onChange={(e) => setGroupForm({ ...groupForm, name: e.target.value })} placeholder="Support" />
          </label>
          <label className="field">
            <span>Description</span>
            <input value={groupForm.description} onChange={(e) => setGroupForm({ ...groupForm, description: e.target.value })} placeholder="Handles tickets and SOS" />
          </label>
        </div>
        <p className="area-hint">Modules this role can access</p>
        <div className="role-modules">
          {pages.map((item) => {
            const on = groupForm.pages.includes(item.id)
            return (
              <button
                key={item.id}
                type="button"
                className={`role-module${on ? ' on' : ''}`}
                onClick={() => toggleModule(item.id)}
              >
                <span className="role-check">{on ? '✓' : ''}</span>
                <span>{item.label}</span>
              </button>
            )
          })}
        </div>
        <div style={{ display: 'flex', gap: 10, maxWidth: 280, marginTop: 4 }}>
          <button className="btn tiny" type="button" onClick={() => setGroupForm({ ...groupForm, pages: pages.map((item) => item.id) })}>
            Select all
          </button>
          <button className="btn tiny" type="button" onClick={() => setGroupForm({ ...groupForm, pages: [] })}>
            Clear
          </button>
        </div>
        <div style={{ display: 'flex', gap: 10, maxWidth: 280, marginTop: 14 }}>
          <button className="btn" type="submit" disabled={busy}>{busy ? 'Saving…' : groupId ? 'Save role' : 'Create role'}</button>
        </div>
        <div className="table-wrap" style={{ marginTop: 16 }}>
          <table>
            <thead>
              <tr>
                <th>Role</th>
                <th>Modules</th>
                <th>{operatorMode ? 'Staff' : 'Users'}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {groups.length === 0 ? (
                <tr><td colSpan={4}>No roles yet. Create one above.</td></tr>
              ) : groups.map((row) => (
                <tr key={row.id} className="clickable" onClick={() => {
                  setGroupId(row.id)
                  setGroupForm({ name: row.name, description: row.description, pages: row.pages })
                }}>
                  <td>
                    <strong>{row.name}</strong>
                    {row.description ? <div className="muted">{row.description}</div> : null}
                  </td>
                  <td>{row.pages.map((id) => pages.find((item) => item.id === id)?.label ?? id).join(' · ') || '—'}</td>
                  <td>{row.userCount}</td>
                  <td>
                    <button className="btn tiny danger" type="button" onClick={(e) => { e.stopPropagation(); void removeGroup(row.id) }}>
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </form>
      ) : (
      <form
        className="card"
        onSubmit={(e) => {
          e.preventDefault()
          void saveUser()
        }}
      >
        <div className="panel-head">
          <div>
            <h2 style={{ margin: 0 }}>{userId ? `Edit ${personLabel}` : peopleLabel}</h2>
            <p className="muted" style={{ margin: '4px 0 0' }}>
              Create staff accounts with a phone number and password. They use both to sign in.
            </p>
          </div>
          {userId ? (
            <button className="btn tiny" type="button" onClick={resetUser}>{operatorMode ? 'New employee' : 'New user'}</button>
          ) : null}
        </div>
        {groups.length === 0 ? (
          <p className="error">Create a role in Roles first, then come back here to assign it.</p>
        ) : null}
        <div className="form-grid">
          <label className="field">
            <span>Full name</span>
            <input value={userForm.fullName} onChange={(e) => setUserForm({ ...userForm, fullName: e.target.value })} placeholder="Full name" />
          </label>
          <label className="field">
            <span>Phone</span>
            <input value={userForm.phone} onChange={(e) => setUserForm({ ...userForm, phone: e.target.value })} placeholder="09XX XXX XXXX" />
          </label>
          <label className="field">
            <span>{userId ? 'New password (optional)' : 'Password'}</span>
            <input type="password" value={userForm.password} onChange={(e) => setUserForm({ ...userForm, password: e.target.value })} autoComplete="new-password" placeholder={userId ? 'Leave blank to keep' : 'At least 6 characters'} />
          </label>
          <label className="field">
            <span>Confirm password</span>
            <input type="password" value={userForm.confirmPassword} onChange={(e) => setUserForm({ ...userForm, confirmPassword: e.target.value })} autoComplete="new-password" placeholder={userId ? 'Leave blank to keep' : 'Repeat password'} />
          </label>
          <label className="field wide">
            <span>Role</span>
            <LookupSuggest
              query={userForm.groupQuery}
              filterQuery={selectedRole && userForm.groupQuery.trim().toLowerCase() === selectedRole.name.toLowerCase() ? '' : userForm.groupQuery}
              onQuery={(value) => {
                const exact = groups.find((item) => item.name.toLowerCase() === value.trim().toLowerCase())
                setUserForm({ ...userForm, groupQuery: value, accessGroupId: exact?.id ?? '' })
              }}
              items={roleItems}
              placeholder="Type a role name"
              extraFor={(item) => roleModules(item.id)}
              onPick={(item) => setUserForm({ ...userForm, accessGroupId: item.id, groupQuery: item.name })}
            />
          </label>
        </div>
        {selectedRole ? (
          <p className="muted" style={{ margin: '0 0 12px' }}>
            {selectedRole.name} can open: {roleModules(selectedRole.id)}
          </p>
        ) : null}
        <div style={{ display: 'flex', gap: 10, maxWidth: 280 }}>
          <button className="btn" type="submit" disabled={busy}>{busy ? 'Saving…' : userId ? `Save ${personLabel}` : `Create ${personLabel}`}</button>
        </div>
        <div className="table-wrap role-table" style={{ marginTop: 16 }}>
          <table>
            <thead>
              <tr>
                <th>{operatorMode ? 'Employee' : 'User'}</th>
                <th>Role</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {users.length === 0 ? (
                <tr><td colSpan={4}>{operatorMode ? 'No employees yet.' : 'No staff users yet.'}</td></tr>
              ) : users.map((row) => (
                <tr key={row.id} className={isMain(row) ? '' : 'clickable'} onClick={() => {
                  if (isMain(row)) {
                    return
                  }
                  setUserId(row.id)
                  setUserForm({
                    fullName: row.fullName,
                    phone: row.phoneNumber,
                    accessGroupId: row.accessGroupId,
                    groupQuery: row.accessGroupName,
                    password: '',
                    confirmPassword: '',
                  })
                }}>
                  <td>
                    <strong>{row.fullName}</strong>
                    <div className="muted">{row.phoneNumber}</div>
                  </td>
                  <td>
                    {isMain(row) ? (
                      <div className="role-pick">
                        <strong>{operatorMode ? 'Main operator' : 'Administrator'}</strong>
                        <small>All modules</small>
                      </div>
                    ) : (
                    <div className="role-pick" onClick={(e) => e.stopPropagation()}>
                      <LookupSuggest
                        query={roleQueryByUser[row.id] ?? row.accessGroupName}
                        filterQuery={
                          (roleQueryByUser[row.id] ?? row.accessGroupName).trim().toLowerCase() === row.accessGroupName.toLowerCase()
                            ? ''
                            : (roleQueryByUser[row.id] ?? row.accessGroupName)
                        }
                        onQuery={(value) => setRoleQueryByUser((current) => ({ ...current, [row.id]: value }))}
                        items={roleItems}
                        placeholder="Type a role"
                        extraFor={(item) => roleModules(item.id)}
                        onPick={(item) => {
                          setRoleQueryByUser((current) => ({ ...current, [row.id]: item.name }))
                          void changeUserRole(row, item.id)
                        }}
                        onBlur={() => setRoleQueryByUser((current) => {
                          const next = { ...current }
                          delete next[row.id]
                          return next
                        })}
                      />
                      <small>{roleModules(row.accessGroupId)}</small>
                    </div>
                    )}
                  </td>
                  <td><StatusTag active={row.isActive} /></td>
                  <td>
                    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', justifyContent: 'flex-end', alignItems: 'center' }}>
                      {resetUserId === row.id ? (
                        <>
                          <input
                            className="inline-input"
                            type="password"
                            value={resetPassword}
                            onClick={(e) => e.stopPropagation()}
                            onChange={(e) => setResetPassword(e.target.value)}
                            placeholder="New password"
                            autoComplete="new-password"
                          />
                          <button className="btn tiny" type="button" onClick={(e) => { e.stopPropagation(); void resetUserPassword(row) }}>
                            Save
                          </button>
                          <button className="btn tiny" type="button" onClick={(e) => { e.stopPropagation(); setResetUserId(null); setResetPassword('') }}>
                            Cancel
                          </button>
                        </>
                      ) : (
                        <button className="btn tiny" type="button" onClick={(e) => { e.stopPropagation(); void resetUserPassword(row) }}>
                          Reset password
                        </button>
                      )}
                      {isMain(row) ? null : (
                      <button className={`btn tiny${row.isActive ? ' danger' : ''}`} type="button" onClick={(e) => { e.stopPropagation(); void toggleUser(row) }}>
                        {row.isActive ? 'Deactivate' : 'Activate'}
                      </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </form>
      )}
    </>
  )
}

function SupportPage() {
  const [ticketId, setTicketId] = useState<string | null>(null)
  if (ticketId) {
    return <SupportDetailPage ticketId={ticketId} onBack={() => setTicketId(null)} />
  }
  return <SupportListPage onOpen={setTicketId} />
}

function SupportListPage({ onOpen }: { onOpen: (id: string) => void }) {
  const [q, setQ] = useState('')
  const [kind, setKind] = useState<SupportKind | ''>('')
  const [status, setStatus] = useState<SupportStatus | ''>('')
  const [items, setItems] = useState<SupportTicket[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [openSos, setOpenSos] = useState(0)
  const [openTickets, setOpenTickets] = useState(0)
  const [closedTickets, setClosedTickets] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    void api.readAllAdminAlerts().catch(() => undefined)
  }, [])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.supportTickets(q, kind, status, page, pageSize)
        .then((data: SupportInbox) => {
          setItems(data.items)
          setTotal(data.total)
          setOpenSos(data.openSos)
          setOpenTickets(data.openTickets)
          setClosedTickets(data.closedTickets)
          setError('')
        })
        .catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, kind, status, page])

  return (
    <div className="form-sections">
      <div className="card">
        <div className="toolbar">
          <div>
            <h2 style={{ margin: 0 }}>Support</h2>
            <p className="muted" style={{ margin: '4px 0 0' }}>
              Read-only. Operators handle tickets and SOS in their municipality.
            </p>
          </div>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
            <div className="chips">
              <button type="button" className={kind === '' && status === '' ? 'on' : ''} onClick={() => { setKind(''); setStatus(''); setPage(1) }}>All</button>
              <button type="button" className={kind === 'Sos' ? 'on' : ''} onClick={() => { setKind('Sos'); setStatus(''); setPage(1) }}>SOS</button>
              <button type="button" className={status === 'Open' && kind === '' ? 'on' : ''} onClick={() => { setKind(''); setStatus('Open'); setPage(1) }}>Open</button>
              <button type="button" className={status === 'Closed' ? 'on' : ''} onClick={() => { setKind(''); setStatus('Closed'); setPage(1) }}>Closed</button>
            </div>
            <div className="ac">
              <input
                value={q}
                onChange={(e) => { setQ(e.target.value); setPage(1) }}
                placeholder="Search operator, booking, or person"
              />
            </div>
          </div>
        </div>
        <div className="fare-cards three" style={{ marginBottom: 16 }}>
          <div className="detail-card">
            <span>Open SOS</span>
            <p className="detail-name" style={{ marginTop: 10 }}>{openSos}</p>
            <p className="muted">Operator must respond in their LGU</p>
          </div>
          <div className="detail-card">
            <span>Open tickets</span>
            <p className="detail-name" style={{ marginTop: 10 }}>{openTickets}</p>
            <p className="muted">Including SOS</p>
          </div>
          <div className="detail-card">
            <span>Closed</span>
            <p className="detail-name" style={{ marginTop: 10 }}>{closedTickets}</p>
            <p className="muted">Resolved by the Operator</p>
          </div>
        </div>
        {error ? <p className="error">{error}</p> : null}
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Ticket</th>
                <th>From</th>
                <th>Operator / municipality</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 ? (
                <tr>
                  <td colSpan={4}>{q.trim() || kind || status ? 'No tickets match that filter.' : 'No support tickets yet.'}</td>
                </tr>
              ) : items.map((item) => (
                <tr key={item.id} className="clickable" onClick={() => onOpen(item.id)}>
                  <td>
                    <div className="tag-row" style={{ marginTop: 0, marginBottom: 6 }}>
                      {item.kind === 'Sos' ? <span className="tag sos">SOS</span> : <span className="tag kind">Support</span>}
                    </div>
                    <strong>{item.subject}</strong>
                    <div className="muted">{item.body}</div>
                    <small className="muted">
                      {phDateTime(item.createdAtUtc)}
                      {item.bookingNumber ? ` · ${item.bookingNumber}` : ''}
                    </small>
                  </td>
                  <td>
                    <strong>{item.openedByName}</strong>
                    <div className="muted">{item.openedBy} · {item.openedByPhone || '—'}</div>
                  </td>
                  <td>
                    <strong>{item.operatorName}</strong>
                    <div className="muted">{item.municipality || '—'}</div>
                    <div className="muted">{item.operatorPhone}</div>
                  </td>
                  <td><SupportStatusTag status={item.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
      </div>
    </div>
  )
}

function SosTicketMapSection({ detail, theme }: { detail: SupportTicketDetail; theme: Theme }) {
  const live = detail.booking?.status === 'Ongoing' || detail.booking?.status === 'Waiting'
  return (
    <div className="sos-map-block">
      <div className="panel-head" style={{ marginBottom: 10 }}>
        <div>
          <h3 style={{ margin: 0 }}>Live booking map</h3>
          <p className="muted" style={{ margin: '6px 0 0' }}>
            {detail.sosLocation
              ? `SOS pressed ${detail.sosLocation.atUtc ? phDateTime(detail.sosLocation.atUtc) : 'during this trip'}.`
              : 'SOS location not available yet.'}
            {live ? ' Rider position refreshes while the trip is active.' : ''}
          </p>
        </div>
      </div>
      <TripLiveMap
        pickupLocation={detail.pickupLocation}
        dropoffLocation={detail.dropoffLocation}
        riderLocation={detail.riderLocation}
        sosLocation={detail.sosLocation}
        theme={theme}
        live={live}
      />
    </div>
  )
}

function SupportDetailPage({ ticketId, onBack }: { ticketId: string; onBack: () => void }) {
  const [detail, setDetail] = useState<SupportTicketDetail | null>(null)
  const [error, setError] = useState('')
  const [theme] = useState<Theme>(readTheme)

  function loadDetail() {
    return api.supportTicket(ticketId).then(setDetail)
  }

  useEffect(() => {
    loadDetail().catch((err: Error) => setError(err.message))
  }, [ticketId])

  useEffect(() => {
    function reload(event: Event) {
      const payload = (event as CustomEvent<OpsAlert>).detail
      if (payload?.ticketId && payload.ticketId !== ticketId) {
        return
      }
      loadDetail().catch(() => undefined)
    }
    window.addEventListener(SOS_ALERT_EVENT, reload)
    return () => window.removeEventListener(SOS_ALERT_EVENT, reload)
  }, [ticketId])

  const live = detail?.booking?.status === 'Ongoing' || detail?.booking?.status === 'Waiting'

  useEffect(() => {
    if (!live) {
      return
    }
    const handle = window.setInterval(() => {
      loadDetail().catch(() => undefined)
    }, 8000)
    return () => window.clearInterval(handle)
  }, [ticketId, live])

  if (error && !detail) {
    return <p className="error">{error}</p>
  }
  if (!detail) {
    return <p>Loading ticket…</p>
  }

  const item = detail.ticket

  return (
    <div className="card">
      <div className="panel-head">
        <div>
          <button className="btn tiny" type="button" onClick={onBack}>Back to support</button>
          <h2 style={{ marginTop: 12 }}>{item.subject}</h2>
          <p className="muted">Opened {phDateTime(item.createdAtUtc)} · {item.openedBy} {item.openedByName}</p>
        </div>
        <div className="tag-row" style={{ marginTop: 0 }}>
          {item.kind === 'Sos' ? <span className="tag sos">SOS</span> : <span className="tag kind">Support</span>}
          <SupportStatusTag status={item.status} />
        </div>
      </div>
      {item.kind === 'Sos' ? (
        <>
          <p className="muted">Operators own support in their municipality. Super Admin can view this ticket but cannot reply or close it.</p>
          <SosTicketMapSection detail={detail} theme={theme} />
          <div className="detail-item wide" style={{ marginTop: 16 }}>
            <span>SOS message</span>
            <p>{item.body}</p>
          </div>
          {detail.booking ? (
            <>
              <h3 style={{ marginTop: 24 }}>Booking details</h3>
              <BookingDetailsBody ride={detail.booking} />
            </>
          ) : null}
          <div className="detail-grid" style={{ marginTop: 16 }}>
            <DetailItem label="Operator" value={`${item.operatorName} · ${item.operatorPhone}`} />
            <DetailItem label="Municipality" value={item.municipality || '—'} />
            <DetailItem label="Opened by" value={`${item.openedByName} · ${item.openedByPhone || '—'}`} />
            <div className="detail-item wide">
              <span>Operator notes</span>
              <p>{item.operatorNotes || 'Operator has not added notes yet.'}</p>
            </div>
            <DetailItem label="Opened" value={phDateTime(item.createdAtUtc)} />
            <DetailItem label="Closed" value={item.closedAtUtc ? phDateTime(item.closedAtUtc) : 'Still open'} />
          </div>
        </>
      ) : (
        <>
          <p className="muted">Operators own support in their municipality. Super Admin can view this ticket but cannot reply or close it.</p>
          <div className="detail-grid">
            <DetailItem label="Operator" value={`${item.operatorName} · ${item.operatorPhone}`} />
            <DetailItem label="Municipality" value={item.municipality || '—'} />
            <DetailItem label="Opened by" value={`${item.openedByName} · ${item.openedByPhone || '—'}`} />
            <DetailItem label="Booking" value={item.bookingNumber || 'Not tied to a booking'} />
            <div className="detail-item wide">
              <span>Message</span>
              <p>{item.body}</p>
            </div>
            <div className="detail-item wide">
              <span>Operator notes</span>
              <p>{item.operatorNotes || 'Operator has not added notes yet.'}</p>
            </div>
            <DetailItem label="Opened" value={phDateTime(item.createdAtUtc)} />
            <DetailItem label="Closed" value={item.closedAtUtc ? phDateTime(item.closedAtUtc) : 'Still open'} />
          </div>
        </>
      )}
    </div>
  )
}

function SupportStatusTag({ status }: { status: SupportStatus }) {
  return <span className={`tag ${status === 'Open' ? 'pending' : 'rejected'}`}>{status}</span>
}

function AuditPage() {
  const [q, setQ] = useState('')
  const [action, setAction] = useState<AuditAction | ''>('')
  const [items, setItems] = useState<AuditLog[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10
  const filters: { id: AuditAction | ''; label: string }[] = [
    { id: '', label: 'All' },
    { id: 'OperatorCreated', label: 'Created' },
    { id: 'OperatorUpdated', label: 'Updated' },
    { id: 'OperatorActivated', label: 'Activated' },
    { id: 'OperatorDeactivated', label: 'Deactivated' },
    { id: 'BillIssued', label: 'Billed' },
  ]

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.auditLogs(q, action, page, pageSize)
        .then((data) => {
          setItems(data.items)
          setTotal(data.total)
          setError('')
        })
        .catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, action, page])

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Operator log</h2>
          <p className="muted" style={{ margin: '4px 0 0' }}>
            Super Admin actions on Operators: create, update, activate, deactivate, and billing.
          </p>
        </div>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
          <div className="chips">
            {filters.map((item) => (
              <button
                key={item.label}
                type="button"
                className={action === item.id ? 'on' : ''}
                onClick={() => { setAction(item.id); setPage(1) }}
              >
                {item.label}
              </button>
            ))}
          </div>
          <div className="ac">
            <input
              value={q}
              onChange={(e) => { setQ(e.target.value); setPage(1) }}
              placeholder="Search operator or action"
            />
          </div>
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>When</th>
              <th>Operator</th>
              <th>Action</th>
              <th>By</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={4}>{q.trim() || action ? 'No log entries match that filter.' : 'No Operator log entries yet.'}</td>
              </tr>
            ) : items.map((item) => (
              <tr key={item.id}>
                <td>{phDateTime(item.createdAtUtc)}</td>
                <td>
                  <strong>{item.operatorName}</strong>
                  <div className="muted">{item.summary}</div>
                </td>
                <td><AuditActionTag action={item.action} label={item.actionLabel} /></td>
                <td>{item.actorName}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function AuditActionTag({ action, label }: { action: AuditAction; label: string }) {
  const tone =
    action === 'OperatorCreated' || action === 'OperatorActivated' ? 'active'
      : action === 'OperatorDeactivated' ? 'rejected'
        : action === 'BillIssued' ? 'pending'
          : 'kind'
  return <span className={`tag ${tone}`}>{label}</span>
}

function ComingSoon({ title, body }: { title: string; body: string }) {
  return (
    <div className="card soon">
      <div>
        <h2>{title}</h2>
        <p>{body}</p>
        <p>Coming soon.</p>
      </div>
    </div>
  )
}

function OperatorShell({
  me,
  onLogout,
  brandName,
  brandLogo,
}: {
  me: Me
  onLogout: () => void
  brandName: string
  brandLogo: string
}) {
  const allowedGroups = useMemo(
    () => (me.isMainOperator
      ? OPERATOR_MENU_GROUPS
      : filterMenuGroups(
          OPERATOR_MENU_GROUPS,
          new Set(
            flattenMenuGroups(OPERATOR_MENU_GROUPS)
              .map((item) => item.id)
              .filter((id) =>
                (id === 'roles' || id === 'employees')
                  ? !!me.isMainOperator
                  : (me.accessPages ?? []).includes(id),
              ),
          ),
        )),
    [me.isMainOperator, me.accessPages],
  )
  const allowedMenus = useMemo(() => flattenMenuGroups(allowedGroups), [allowedGroups])
  const firstPage = allowedMenus[0]?.id ?? 'dashboard'
  const [page, setPage] = useState<PageId>(firstPage)
  const [collapsed, setCollapsed] = useState(readSidebarCollapsed)
  const [theme, setThemeState] = useState<Theme>(readTheme)
  const [alerts, setAlerts] = useState<OperatorNavAlerts>({
    pendingWalletRequests: 0,
    openSos: 0,
    unreadBilling: 0,
    pendingAccountDeletes: 0,
  })
  const [sosFlash, setSosFlash] = useState<OpsAlert | null>(null)

  useEffect(() => {
    if (!allowedMenus.some((item) => item.id === page)) {
      setPage(firstPage)
    }
  }, [page, firstPage, allowedMenus])

  const loadAlerts = useCallback(() => {
    api.operatorAlerts()
      .then(setAlerts)
      .catch(() => setAlerts({ pendingWalletRequests: 0, openSos: 0, unreadBilling: 0, pendingAccountDeletes: 0 }))
  }, [])

  useEffect(() => {
    async function refresh() {
      if (page === 'billing') {
        await api.readOperatorBillingInbox().catch(() => {})
      }
      loadAlerts()
    }
    void refresh()
    const handle = window.setInterval(() => void refresh(), 15000)
    return () => window.clearInterval(handle)
  }, [page, loadAlerts])

  useOpsAlerts(loadAlerts)

  useEffect(() => {
    function onSos(event: Event) {
      const detail = (event as CustomEvent<OpsAlert>).detail
      setSosFlash(detail ?? null)
    }
    window.addEventListener(SOS_ALERT_EVENT, onSos)
    return () => window.removeEventListener(SOS_ALERT_EVENT, onSos)
  }, [])

  function toggleTheme() {
    const next = theme === 'dark' ? 'light' : 'dark'
    setTheme(next)
    setThemeState(next)
  }

  const title = allowedMenus.find((item) => item.id === page)?.label
    ?? OPERATOR_MENUS.find((item) => item.id === page)?.label
    ?? 'Dashboard'

  return (
    <div className={`shell${collapsed ? ' collapsed' : ''}`}>
      <aside className="sidebar">
        <div className="side-brand">
          <img className="brand-mark" src={brandLogo} alt={brandName} />
          <div className="side-copy">
            <strong>{brandName}</strong>
            <span>{me.companyName || 'Operator'}</span>
          </div>
        </div>
        <SideNav
          scope="operator"
          groups={allowedGroups}
          page={page}
          shellCollapsed={collapsed}
          onNavigate={setPage}
          badge={(id) => {
            if (id === 'wallet') return <NavBadge count={alerts.pendingWalletRequests} tone="wallet" />
            if (id === 'support') return <NavBadge count={alerts.openSos} tone="sos" />
            if (id === 'billing') return <NavBadge count={alerts.unreadBilling} tone="billing" />
            if (id === 'customers') return <NavBadge count={alerts.pendingAccountDeletes} tone="delete" />
            return null
          }}
        />
        <div className="side-foot">
          <button className="collapse-btn" type="button" onClick={() => {
            const next = !collapsed
            setCollapsed(next)
            setSidebarCollapsed(next)
          }}>
            {collapsed ? '»' : '« Collapse'}
          </button>
          <button className="collapse-btn" type="button" onClick={onLogout}>
            {collapsed ? '⎋' : 'Log out'}
          </button>
        </div>
      </aside>
      <main className="main">
        <header className="top">
          <div>
            <h1>{title}</h1>
            <p>Your company portal for motorcycle and tricycle bookings.</p>
          </div>
          <div />
          <div className="who">
            <button className="icon-btn" type="button" onClick={toggleTheme} title="Toggle theme">
              {theme === 'dark' ? '☀' : '☾'}
            </button>
            <div className="avatar">{me.fullName.slice(0, 1)}</div>
            <div>
              <strong>{me.fullName}</strong>
              <span>{me.isMainOperator ? 'Main operator' : (me.accessGroupName || me.phoneNumber)}</span>
            </div>
          </div>
        </header>
        <SosSoundArmBanner />
        {alerts.openSos > 0 || sosFlash ? (
          <SosBanner
            count={alerts.openSos}
            flash={sosFlash}
            detail={alerts.openSos > 0 ? `${alerts.openSos} open SOS in your area.` : 'New SOS received in your area.'}
            onOpenSupport={() => setPage('support')}
          />
        ) : null}
        {page === 'dashboard' && <OperatorDashboardPage />}
        {page === 'bookings' && <OperatorBookingsPage />}
        {page === 'overview' && <OperatorOverviewPage onOpen={(next) => setPage(next)} />}
        {page === 'schedule' && <OperatorSchedulePage />}
        {page === 'riders' && <OperatorRidersPage />}
        {page === 'customers' && <OperatorCustomersPage />}
        {page === 'fleet' && <OperatorFleetPage theme={theme} />}
        {page === 'fares' && <OperatorFaresPage />}
        {page === 'derive-fares' && <OperatorDeriveFaresPage />}
        {page === 'surcharges' && <OperatorSurchargesPage />}
        {page === 'support' && <OperatorSupportPage />}
        {page === 'inbox' && (
          <OperatorInboxPage
            onOpenBilling={() => setPage('billing')}
            onOpenCustomers={() => setPage('customers')}
          />
        )}
        {page === 'billing' && <OperatorBillingPage />}
        {page === 'wallet' && <OperatorWalletPage />}
        {page === 'promos' && <OperatorPromosPage />}
        {page === 'commission' && <CommissionReportPage mode="operator" />}
        {page === 'rider-report' && <OperatorRiderReportPage />}
        {page === 'company' && <OperatorCompanyPage />}
        {page === 'roles' && me.isMainOperator ? (
          <div className="form-sections">
            <AccessSettings section="roles" mode="operator" />
          </div>
        ) : null}
        {page === 'employees' && me.isMainOperator ? (
          <div className="form-sections">
            <AccessSettings section="users" mode="operator" />
          </div>
        ) : null}
      </main>
    </div>
  )
}

function OperatorBookingsPage() {
  const [bookingId, setBookingId] = useState<string | null>(null)
  if (bookingId) {
    return <OperatorBookingDetail id={bookingId} onBack={() => setBookingId(null)} />
  }
  return (
    <OperatorBookingList
      load={api.operatorBookingList}
      onOpen={setBookingId}
      hint="All bookings for your company: live, scheduled, completed, and cancelled."
    />
  )
}

function CommissionReportPage({ mode }: { mode: 'admin' | 'operator' }) {
  const today = isoDate(new Date())
  const [dateMode, setDateMode] = useState<'date' | 'range'>('date')
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(today)
  const [bookingNo, setBookingNo] = useState('')
  const [rider, setRider] = useState('')
  const [operatorId, setOperatorId] = useState('')
  const [operators, setOperators] = useState<OperatorListItem[]>([])
  const [page, setPage] = useState(1)
  const [data, setData] = useState<CommissionReportResponse | null>(null)
  const [error, setError] = useState('')
  const pageSize = 10
  const start = from > to ? to : from
  const end = from > to ? from : to
  const fromParam = dateMode === 'date' ? end : start
  const toParam = end

  useEffect(() => {
    if (mode !== 'admin') return
    api.operators('', 1, 200)
      .then((rows) => setOperators(rows.items))
      .catch(() => setOperators([]))
  }, [mode])

  useEffect(() => {
    setPage(1)
  }, [dateMode, from, to, bookingNo, rider, operatorId])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      const opts = {
        bookingNo: bookingNo.trim() || undefined,
        rider: rider.trim() || undefined,
        from: fromParam,
        to: toParam,
        page,
        pageSize,
        operatorId: mode === 'admin' && operatorId ? operatorId : undefined,
      }
      const load = mode === 'admin'
        ? api.adminCommissionReport(opts)
        : api.operatorCommissionReport(opts)
      load
        .then(setData)
        .catch((err: Error) => setError(err.message))
    }, bookingNo || rider ? 220 : 0)
    return () => window.clearTimeout(handle)
  }, [mode, dateMode, fromParam, toParam, bookingNo, rider, operatorId, page])

  const rows = data?.page.items ?? []
  const total = data?.page.total ?? 0
  const pages = Math.max(1, Math.ceil(total / pageSize))

  function openBooking(row: CommissionReportItem) {
    const url = `${window.location.pathname}${window.location.search}#commission/${mode}/${row.operatorId}/${row.id}`
    window.open(url, '_blank', 'noopener,noreferrer')
  }

  return (
    <div className="form-sections">
      <div className="card">
        <div className="toolbar">
          <div>
            <h2 style={{ margin: 0 }}>Commission</h2>
            <p className="muted" style={{ margin: '4px 0 0' }}>
              Completed bookings with rider, system (operator), and admin commission.
            </p>
          </div>
        </div>
        <div className="ride-filters" style={{ marginTop: 12 }}>
          <div className="chips">
            <button type="button" className={dateMode === 'date' ? 'on' : ''} onClick={() => { setDateMode('date'); setFrom(today); setTo(today) }}>By date</button>
            <button type="button" className={dateMode === 'range' ? 'on' : ''} onClick={() => { setDateMode('range'); setFrom(isoDate(addDays(new Date(), -6))); setTo(today) }}>By range</button>
          </div>
          {dateMode === 'date' ? (
            <label className="field" style={{ margin: 0, minWidth: 160 }}>
              <span>Date</span>
              <input type="date" value={to} onChange={(e) => { setFrom(e.target.value); setTo(e.target.value) }} />
            </label>
          ) : (
            <>
              <label className="field" style={{ margin: 0, minWidth: 150 }}>
                <span>From</span>
                <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
              </label>
              <label className="field" style={{ margin: 0, minWidth: 150 }}>
                <span>To</span>
                <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
              </label>
            </>
          )}
          <label className="field" style={{ margin: 0, minWidth: 180 }}>
            <span>Booking no</span>
            <input value={bookingNo} onChange={(e) => setBookingNo(e.target.value)} placeholder="YP…" />
          </label>
          <label className="field" style={{ margin: 0, minWidth: 180 }}>
            <span>Rider</span>
            <input value={rider} onChange={(e) => setRider(e.target.value)} placeholder="Name or plate" />
          </label>
          {mode === 'admin' ? (
            <label className="field" style={{ margin: 0, minWidth: 200 }}>
              <span>Operator</span>
              <select value={operatorId} onChange={(e) => setOperatorId(e.target.value)}>
                <option value="">All operators</option>
                {operators.map((op) => (
                  <option key={op.id} value={op.id}>{op.companyName}</option>
                ))}
              </select>
            </label>
          ) : null}
        </div>
        {error ? <p className="error">{error}</p> : null}
        {data ? (
          <div className="fare-cards" style={{ marginTop: 14 }}>
            <div className="detail-card">
              <span>Bookings</span>
              <p className="detail-name" style={{ marginTop: 8 }}>{data.summary.count}</p>
            </div>
            <div className="detail-card">
              <span>Booking amount</span>
              <p className="detail-name" style={{ marginTop: 8 }}>{peso(data.summary.bookingAmount)}</p>
            </div>
            <div className="detail-card">
              <span>Rider</span>
              <p className="detail-name" style={{ marginTop: 8 }}>{peso(data.summary.riderCommission)}</p>
            </div>
            <div className="detail-card">
              <span>System</span>
              <p className="detail-name" style={{ marginTop: 8 }}>{peso(data.summary.systemCommission)}</p>
            </div>
            <div className="detail-card">
              <span>Admin</span>
              <p className="detail-name" style={{ marginTop: 8 }}>{peso(data.summary.adminCommission)}</p>
            </div>
          </div>
        ) : null}
        <div className="table-wrap" style={{ marginTop: 16 }}>
          <table>
            <thead>
              <tr>
                <th>Book no</th>
                {mode === 'admin' ? <th>Operator</th> : null}
                <th>Rider</th>
                <th>Rider comm</th>
                <th>System comm</th>
                <th>Admin comm</th>
                <th>Booking amount</th>
                <th>Date</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr><td colSpan={mode === 'admin' ? 8 : 7}>No commission rows for this filter.</td></tr>
              ) : rows.map((row) => (
                <tr
                  key={row.id}
                  className="clickable"
                  onClick={() => openBooking(row)}
                >
                  <td><strong>{row.reference}</strong></td>
                  {mode === 'admin' ? <td>{row.operatorName}</td> : null}
                  <td>{row.riderName}</td>
                  <td>{peso(row.riderCommission)}</td>
                  <td>{peso(row.systemCommission)}</td>
                  <td>{peso(row.adminCommission)}</td>
                  <td><strong>{peso(row.bookingAmount)}</strong></td>
                  <td>{phDateTime(row.dateUtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="pager" style={{ marginTop: 12 }}>
          <button className="btn tiny" type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</button>
          <span className="muted">Page {page} of {pages}</span>
          <button className="btn tiny" type="button" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>Next</button>
        </div>
      </div>
    </div>
  )
}

function OperatorRiderReportPage() {
  const today = isoDate(new Date())
  const [dateMode, setDateMode] = useState<'date' | 'range'>('date')
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(today)
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<RiderReportResponse | null>(null)
  const [error, setError] = useState('')
  const [exportBusy, setExportBusy] = useState(false)
  const pageSize = 10
  const start = from > to ? to : from
  const end = from > to ? from : to
  const fromParam = dateMode === 'date' ? end : start
  const toParam = end

  useEffect(() => {
    setPage(1)
  }, [dateMode, from, to, q])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.operatorRiderReport({
        q: q.trim() || undefined,
        from: fromParam,
        to: toParam,
        page,
        pageSize,
      })
        .then(setData)
        .catch((err: Error) => setError(err.message))
    }, q ? 220 : 0)
    return () => window.clearTimeout(handle)
  }, [dateMode, fromParam, toParam, q, page])

  const rows = data?.page.items ?? []
  const total = data?.page.total ?? 0
  const pages = Math.max(1, Math.ceil(total / pageSize))

  async function exportExcel() {
    setExportBusy(true)
    setError('')
    try {
      await api.exportOperatorRiderReport({
        q: q.trim() || undefined,
        from: fromParam,
        to: toParam,
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not export rider report.')
    } finally {
      setExportBusy(false)
    }
  }

  return (
    <div className="form-sections">
      <div className="card">
        <div className="toolbar">
          <div>
            <h2 style={{ margin: 0 }}>Rider</h2>
            <p className="muted" style={{ margin: '4px 0 0' }}>
              Rider roster with completed rides and income for the selected date or range.
            </p>
          </div>
          <button className="btn" type="button" disabled={exportBusy} onClick={() => void exportExcel()}>
            {exportBusy ? 'Exporting…' : 'Export Excel'}
          </button>
        </div>
        <div className="ride-filters" style={{ marginTop: 12 }}>
          <div className="chips">
            <button type="button" className={dateMode === 'date' ? 'on' : ''} onClick={() => { setDateMode('date'); setFrom(today); setTo(today) }}>By date</button>
            <button type="button" className={dateMode === 'range' ? 'on' : ''} onClick={() => { setDateMode('range'); setFrom(isoDate(addDays(new Date(), -6))); setTo(today) }}>By range</button>
          </div>
          {dateMode === 'date' ? (
            <label className="field" style={{ margin: 0, minWidth: 160 }}>
              <span>Date</span>
              <input type="date" value={to} onChange={(e) => { setFrom(e.target.value); setTo(e.target.value) }} />
            </label>
          ) : (
            <>
              <label className="field" style={{ margin: 0, minWidth: 150 }}>
                <span>From</span>
                <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
              </label>
              <label className="field" style={{ margin: 0, minWidth: 150 }}>
                <span>To</span>
                <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
              </label>
            </>
          )}
          <label className="field" style={{ margin: 0, minWidth: 220 }}>
            <span>Search</span>
            <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Name, mobile, or plate" />
          </label>
        </div>
        {error ? <p className="error">{error}</p> : null}
        {data ? (
          <div className="fare-cards" style={{ marginTop: 14 }}>
            <div className="detail-card">
              <span>Riders</span>
              <p className="detail-name" style={{ marginTop: 8 }}>{data.summary.riderCount}</p>
            </div>
            <div className="detail-card">
              <span>Total rides</span>
              <p className="detail-name" style={{ marginTop: 8 }}>{data.summary.totalRides}</p>
            </div>
            <div className="detail-card">
              <span>Rider income</span>
              <p className="detail-name" style={{ marginTop: 8 }}>{peso(data.summary.riderIncome)}</p>
            </div>
            <div className="detail-card">
              <span>Booking amount</span>
              <p className="detail-name" style={{ marginTop: 8 }}>{peso(data.summary.bookingAmount)}</p>
            </div>
          </div>
        ) : null}
        <div className="table-wrap" style={{ marginTop: 16 }}>
          <table>
            <thead>
              <tr>
                <th>Rider name</th>
                <th>Plate</th>
                <th>Registration</th>
                <th>Mobile</th>
                <th>Joined</th>
                <th>Total rides</th>
                <th>Rider income</th>
                <th>Booking amount</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr><td colSpan={8}>No riders for this filter.</td></tr>
              ) : rows.map((row: RiderReportItem) => (
                <tr key={row.riderId}>
                  <td><strong>{row.riderName}</strong>{row.isActive ? null : <span className="muted"> · Off</span>}</td>
                  <td>{row.plateNumber || '—'}</td>
                  <td>{row.vehicleFranchiseNumber || '—'}</td>
                  <td>{row.mobile || '—'}</td>
                  <td>{phDateTime(row.joinedAtUtc)}</td>
                  <td>{row.totalRides}</td>
                  <td>{peso(row.riderIncome)}</td>
                  <td><strong>{peso(row.bookingAmount)}</strong></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="pager" style={{ marginTop: 12 }}>
          <button className="btn tiny" type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</button>
          <span className="muted">Page {page} of {pages}</span>
          <button className="btn tiny" type="button" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>Next</button>
        </div>
      </div>
    </div>
  )
}

function AdminOperatorBookingsPage({ operatorId, onBack }: { operatorId: string; onBack: () => void }) {
  const [bookingId, setBookingId] = useState<string | null>(null)
  const load = useCallback((q: string, page: number, pageSize: number, status: TripStatus | '', from?: string, to?: string) =>
    api.adminOperatorBookings(operatorId, q, page, pageSize, status, from, to), [operatorId])

  if (bookingId) {
    return (
      <BookingDetailPage
        loadKey={bookingId}
        load={() => api.adminOperatorBooking(operatorId, bookingId)}
        onBack={() => setBookingId(null)}
        backLabel="Back to booking"
      />
    )
  }
  return (
    <OperatorBookingList
      load={load}
      onOpen={setBookingId}
      hint="All bookings for this Operator: live, scheduled, completed, and cancelled."
      extra={<button className="btn tiny" type="button" onClick={onBack}>Back to Operator</button>}
    />
  )
}

function OperatorBookingList({
  load,
  onOpen,
  hint,
  extra,
}: {
  load: (q: string, page: number, pageSize: number, status: TripStatus | '', from?: string, to?: string) => Promise<{ items: OperatorBookingListItem[]; total: number }>
  onOpen: (id: string) => void
  hint: string
  extra?: ReactNode
}) {
  const today = isoDate(new Date())
  const [q, setQ] = useState('')
  const [statusFilter, setStatusFilter] = useState<TripStatus | ''>('')
  const [dateMode, setDateMode] = useState<'all' | 'date' | 'range'>('all')
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(today)
  const [items, setItems] = useState<OperatorBookingListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10
  const start = from > to ? to : from
  const end = from > to ? from : to
  const fromParam = dateMode === 'all' ? undefined : dateMode === 'date' ? to : start
  const toParam = dateMode === 'all' ? undefined : dateMode === 'date' ? to : end

  function pickDateMode(next: typeof dateMode) {
    setDateMode(next)
    setPage(1)
    if (next === 'date') {
      setFrom(today)
      setTo(today)
    }
    if (next === 'range') {
      setFrom(isoDate(addDays(new Date(), -6)))
      setTo(today)
    }
  }

  useEffect(() => {
    const handle = window.setTimeout(() => {
      load(q, page, pageSize, statusFilter, fromParam, toParam)
        .then((data) => { setItems(data.items); setTotal(data.total); setError('') })
        .catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, page, statusFilter, load, fromParam, toParam])

  useEffect(() => {
    setPage(1)
  }, [q, statusFilter, fromParam, toParam])

  return (
    <div className="card">
      <div className="toolbar">
        <h2 style={{ margin: 0 }}>Booking</h2>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
          <div className="ac">
            <input
              value={q}
              placeholder="Search booking number, customer, rider, or place"
              onChange={(e) => { setQ(e.target.value); setPage(1) }}
            />
          </div>
          {extra}
        </div>
      </div>
      <p className="muted">{hint}</p>
      <div className="ride-filters" style={{ marginBottom: 12 }}>
        <div className="chips">
          {(['all', 'date', 'range'] as const).map((item) => (
            <button key={item} type="button" className={dateMode === item ? 'on' : ''} onClick={() => pickDateMode(item)}>
              {item === 'all' ? 'All dates' : item === 'date' ? 'By date' : 'By range'}
            </button>
          ))}
        </div>
        {dateMode === 'date' ? (
          <label className="date-search">
            <span>Date</span>
            <input type="date" value={to} onChange={(e) => { setTo(e.target.value); setFrom(e.target.value); setPage(1) }} />
          </label>
        ) : null}
        {dateMode === 'range' ? (
          <div className="date-search">
            <span>From</span>
            <input type="date" value={from} max={to} onChange={(e) => { setFrom(e.target.value); setPage(1) }} />
            <span>To</span>
            <input type="date" value={to} min={from} onChange={(e) => { setTo(e.target.value); setPage(1) }} />
          </div>
        ) : null}
      </div>
      <TripStatusFilter value={statusFilter} onChange={setStatusFilter} />
      {error ? <p className="error">{error}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>When</th>
              <th>Customer</th>
              <th>Rider</th>
              <th>Route</th>
              <th>Payment</th>
              <th>Status</th>
              <th>Fare</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={7}>
                  {statusFilter
                    ? `No ${TRIP_STATUS_FILTERS.find((item) => item.value === statusFilter)?.label.toLowerCase() ?? statusFilter.toLowerCase()} bookings${q.trim() ? ' match that search.' : '.'}`
                    : q.trim() ? 'No bookings match that search.' : 'No bookings yet.'}
                </td>
              </tr>
            ) : items.map((row) => (
              <tr key={row.id} className="clickable" onClick={() => onOpen(row.id)}>
                <td>
                  <strong>{phDateTime(row.scheduledAtUtc ?? row.requestedAtUtc)}</strong>
                  <div><small>{row.reference}</small></div>
                  {row.scheduledAtUtc ? <div><small>Scheduled</small></div> : null}
                </td>
                <td>
                  <strong>{row.customerName}</strong>
                  <div><small>{row.customerPhone}</small></div>
                </td>
                <td>
                  {row.riderName}
                  <div><small>{row.plateNumber}</small></div>
                </td>
                <td>
                  <small>{stopAddress(row.pickup)}</small>
                  <div><small>→ {stopAddress(row.dropoff)}</small></div>
                </td>
                <td><PaymentMethodTag method={row.paymentMethod} other={row.paymentMethodOther} /></td>
                <td>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, alignItems: 'center' }}>
                    <TripStatusTag status={row.status} />
                    <PromoTag
                      isPromoSponsored={row.isPromoSponsored}
                      promoCode={row.promoCode}
                      discountPercent={row.discountPercent}
                    />
                  </div>
                </td>
                <td>{peso(row.fare)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function OperatorBookingDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const [canCancel, setCanCancel] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function cancel() {
    if (!window.confirm('Are you sure you want to cancel this booking?')) {
      return
    }
    setError('')
    setBusy(true)
    try {
      await api.cancelScheduledBooking(id)
      onBack()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not cancel this booking.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      {error ? <p className="error">{error}</p> : null}
      <BookingDetailPage
        loadKey={id}
        load={async () => {
          const ride = await api.operatorBooking(id)
          setCanCancel(!!ride.scheduledAtUtc && (ride.status === 'Pending' || ride.status === 'Waiting'))
          return ride
        }}
        onBack={onBack}
        backLabel="Back to booking"
        allowReassign
        extra={canCancel ? (
          <button className="btn tiny danger" type="button" disabled={busy} onClick={() => void cancel()}>
            {busy ? 'Cancelling…' : 'Cancel booking'}
          </button>
        ) : null}
      />
    </>
  )
}

function OperatorDashboardPage() {
  const today = isoDate(new Date())
  const [mode, setMode] = useState<'today' | 'date' | 'range'>('today')
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(today)
  const [board, setBoard] = useState<OperatorBookingBoard | null>(null)
  const [error, setError] = useState('')
  const [bookingId, setBookingId] = useState<string | null>(null)

  const start = from > to ? to : from
  const end = from > to ? from : to
  const fromParam = mode === 'today' ? today : mode === 'date' ? end : start
  const toParam = mode === 'today' ? today : end

  useEffect(() => {
    if (bookingId) {
      return
    }
    function load() {
      api.operatorBookings(fromParam, toParam)
        .then((next) => {
          setBoard(next)
          setError('')
        })
        .catch((err: Error) => setError(err.message))
    }
    load()
    const handle = window.setInterval(load, 8000)
    return () => window.clearInterval(handle)
  }, [bookingId, fromParam, toParam])

  function pickMode(next: typeof mode) {
    setMode(next)
    if (next === 'today' || next === 'date') {
      setFrom(today)
      setTo(today)
    }
    if (next === 'range') {
      setFrom(isoDate(addDays(new Date(), -6)))
      setTo(today)
    }
  }

  if (bookingId) {
    return (
      <BookingDetailPage
        loadKey={bookingId}
        load={() => api.operatorBooking(bookingId)}
        onBack={() => setBookingId(null)}
        backLabel="Back to dashboard"
        allowReassign
      />
    )
  }

  const columns: { key: keyof OperatorBookingBoard; title: string; hint: string }[] = [
    { key: 'pending', title: 'Pending', hint: 'New bookings waiting to be accepted' },
    { key: 'waiting', title: 'Waiting', hint: 'Rider assigned, heading to pickup' },
    { key: 'ongoing', title: 'Ongoing', hint: 'Trip in progress' },
    { key: 'completed', title: 'Complete', hint: 'Finished trips' },
  ]

  return (
    <div className="dashboard-page">
      <div className="toolbar" style={{ marginBottom: 12 }}>
        <p className="muted" style={{ margin: 0 }}>Filter bookings by date. Complete shows that day only so the board stays clear.</p>
        <div className="ride-filters">
          <div className="chips">
            {(['today', 'date', 'range'] as const).map((item) => (
              <button key={item} type="button" className={mode === item ? 'on' : ''} onClick={() => pickMode(item)}>
                {item === 'today' ? 'Today' : item === 'date' ? 'By date' : 'By range'}
              </button>
            ))}
          </div>
          {mode === 'date' ? (
            <label className="date-search">
              <span>Date</span>
              <input type="date" value={to} max={today} onChange={(e) => { setFrom(e.target.value); setTo(e.target.value) }} />
            </label>
          ) : null}
          {mode === 'range' ? (
            <div className="date-search">
              <span>From</span>
              <input type="date" value={from} max={to || today} onChange={(e) => setFrom(e.target.value)} />
              <span>To</span>
              <input type="date" value={to} min={from} max={today} onChange={(e) => setTo(e.target.value)} />
            </div>
          ) : null}
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="stats">
        <Stat label="Pending" value={board?.pending.total ?? 0} tone="pending" />
        <Stat label="Waiting" value={board?.waiting.total ?? 0} tone="waiting" />
        <Stat label="Ongoing" value={board?.ongoing.total ?? 0} tone="ongoing" />
        <Stat label="Complete" value={board?.completed.total ?? 0} tone="completed" />
      </div>
      {!board ? <p>Loading dashboard…</p> : (
        <div className="booking-board">
          {columns.map((column) => {
            const data = board[column.key]
            return (
              <section key={column.key} className={`card booking-col tone-${column.key}`}>
                <header>
                  <h2>{column.title}</h2>
                  <span>{data.total}</span>
                </header>
                <p className="muted">{column.hint}</p>
                {data.items.length === 0 ? (
                  <p className="muted">No {column.title.toLowerCase()} bookings.</p>
                ) : data.items.map((ride: RideListItem) => (
                  <button
                    key={ride.id}
                    type="button"
                    className="booking-card"
                    onClick={() => setBookingId(ride.id)}
                  >
                    <span className="booking-card-head">
                      <strong>{ride.reference || ride.customerName}</strong>
                      <span className="booking-card-tags">
                        <PromoTag
                          isPromoSponsored={ride.isPromoSponsored}
                          promoCode={ride.promoCode}
                          discountPercent={ride.discountPercent}
                        />
                        <TripStatusTag status={ride.status} />
                      </span>
                    </span>
                    <small>{ride.customerName}</small>
                    <small>{stopAddress(ride.pickup)} → {stopAddress(ride.dropoff)}</small>
                    <span className="booking-card-meta">
                      <VehicleTag type={ride.vehicleType} />
                      <PaymentMethodTag method={ride.paymentMethod} other={ride.paymentMethodOther} />
                      <em>{peso(ride.fare)}</em>
                    </span>
                    <small>{phDateTime(ride.requestedAtUtc)}</small>
                  </button>
                ))}
              </section>
            )
          })}
        </div>
      )}
    </div>
  )
}

function OperatorSchedulePage() {
  const [view, setView] = useState<'list' | 'create' | 'detail'>('list')
  const [bookingId, setBookingId] = useState<string | null>(null)

  if (view === 'create') {
    return (
      <OperatorScheduleForm
        onDone={(id) => { setBookingId(id); setView('detail') }}
        onCancel={() => setView('list')}
      />
    )
  }

  if (view === 'detail' && bookingId) {
    return (
      <OperatorScheduleDetail
        id={bookingId}
        onBack={() => { setBookingId(null); setView('list') }}
      />
    )
  }

  return (
    <OperatorScheduleList
      onCreate={() => setView('create')}
      onOpen={(id) => { setBookingId(id); setView('detail') }}
    />
  )
}

function OperatorScheduleList({
  onCreate,
  onOpen,
}: {
  onCreate: () => void
  onOpen: (id: string) => void
}) {
  const [q, setQ] = useState('')
  const [statusFilter, setStatusFilter] = useState<TripStatus | ''>('')
  const [items, setItems] = useState<ScheduledBooking[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.scheduledBookings(q, page, pageSize, statusFilter)
        .then((data) => { setItems(data.items); setTotal(data.total); setError('') })
        .catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, page, statusFilter])

  useEffect(() => {
    setPage(1)
  }, [q, statusFilter])

  return (
    <div className="card">
      <div className="toolbar">
        <h2 style={{ margin: 0 }}>Schedule booking</h2>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
          <div className="ac">
            <input
              value={q}
              placeholder="Search customer, rider, or booking number"
              onChange={(e) => { setQ(e.target.value); setPage(1) }}
            />
          </div>
          <button className="btn" type="button" onClick={onCreate} style={{ width: 'auto', whiteSpace: 'nowrap' }}>
            Create schedule
          </button>
        </div>
      </div>
      <p className="muted">Customers set a future pickup time and assign a rider. Create the booking here until the customer app is live.</p>
      <TripStatusFilter value={statusFilter} onChange={setStatusFilter} />
      {error ? <p className="error">{error}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Scheduled</th>
              <th>Customer</th>
              <th>Rider</th>
              <th>Route</th>
              <th>Payment</th>
              <th>Status</th>
              <th>Fare</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={7}>
                  {statusFilter
                    ? `No ${TRIP_STATUS_FILTERS.find((item) => item.value === statusFilter)?.label.toLowerCase() ?? statusFilter.toLowerCase()} scheduled bookings${q.trim() ? ' match that search.' : '.'}`
                    : q.trim() ? 'No scheduled bookings match that search.' : 'No scheduled bookings yet.'}
                </td>
              </tr>
            ) : items.map((row) => (
              <tr key={row.id} className="clickable" onClick={() => onOpen(row.id)}>
                <td>
                  <strong>{phDateTime(row.scheduledAtUtc)}</strong>
                  <div><small>{row.reference}</small></div>
                </td>
                <td>
                  <strong>{row.customerName}</strong>
                  <div><small>{row.customerPhone}</small></div>
                </td>
                <td>
                  {row.riderName}
                  <div><small>{row.plateNumber}</small></div>
                </td>
                <td>
                  <small>{stopAddress(row.pickup)}</small>
                  <div><small>→ {stopAddress(row.dropoff)}</small></div>
                </td>
                <td><PaymentMethodTag method={row.paymentMethod} other={row.paymentMethodOther} /></td>
                <td><TripStatusTag status={row.status} /></td>
                <td>{peso(row.fare)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function OperatorScheduleForm({
  onDone,
  onCancel,
}: {
  onDone: (id: string) => void
  onCancel: () => void
}) {
  const [customerName, setCustomerName] = useState('')
  const [phone, setPhone] = useState('')
  const [riderQuery, setRiderQuery] = useState('')
  const [riderId, setRiderId] = useState('')
  const [riders, setRiders] = useState<RiderListItem[]>([])
  const [pickup, setPickup] = useState<AddressValue>(emptyAddress)
  const [dropoff, setDropoff] = useState<AddressValue>(emptyAddress)
  const [scheduledAt, setScheduledAt] = useState(() => toPhInput(new Date(Date.now() + 60 * 60 * 1000).toISOString()))
  const [notes, setNotes] = useState('')
  const [distanceKm, setDistanceKm] = useState('4')
  const [passengerCount, setPassengerCount] = useState('1')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Cash')
  const [paymentMethodOther, setPaymentMethodOther] = useState('')
  const [riderPaymentMethods, setRiderPaymentMethods] = useState<PaymentMethod[]>([])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.opRiders(riderQuery, 1, 20)
        .then((data) => setRiders(data.items.filter((row) => row.isActive)))
        .catch(() => setRiders([]))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [riderQuery])

  async function save(event: FormEvent) {
    event.preventDefault()
    setError('')
    const scheduledAtUtc = fromPhInput(scheduledAt)
    if (!scheduledAtUtc) {
      setError('Set the pickup date and time in Philippine time.')
      return
    }
    if (!riderId) {
      setError('Choose an active rider from your fleet.')
      return
    }
    if (!pickup.barangay || !dropoff.barangay) {
      setError('Choose pickup and drop-off barangays.')
      return
    }
    if (riderPaymentMethods.length === 0) {
      setError('The selected rider has no payment methods configured.')
      return
    }
    if (!riderPaymentMethods.includes(paymentMethod)) {
      setError('Choose a valid payment method: CASH, GCASH, MAYA, or OTHERS.')
      return
    }
    if (paymentMethod === 'Other' && !paymentMethodOther.trim()) {
      setError('Describe the other payment method.')
      return
    }
    setBusy(true)
    try {
      const saved = await api.createScheduledBooking({
        customerName,
        phone,
        riderId,
        pickupBarangayId: pickup.barangay.id,
        pickupDetails: pickup.details,
        dropoffBarangayId: dropoff.barangay.id,
        dropoffDetails: dropoff.details,
        scheduledAtUtc,
        notes: notes.trim() || undefined,
        distanceKm: Number(distanceKm) || 4,
        passengerCount: Math.max(1, Math.floor(Number(passengerCount) || 1)),
        paymentMethod,
        paymentMethodOther: paymentMethod === 'Other' ? paymentMethodOther.trim() : undefined,
      })
      onDone(saved.id)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not create the scheduled booking.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="card" onSubmit={(event) => void save(event)}>
      <div className="panel-head">
        <div>
          <button className="btn tiny" type="button" onClick={onCancel}>Back to schedule</button>
          <h2 style={{ marginTop: 12 }}>Create schedule booking</h2>
          <p className="muted">Set a future pickup time and assign a rider for this customer.</p>
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="form-grid">
        <label className="field"><span>Customer name</span><input value={customerName} onChange={(e) => setCustomerName(e.target.value)} /></label>
        <label className="field"><span>Customer phone</span><input value={phone} onChange={(e) => setPhone(e.target.value)} /></label>
        <label className="field">
          <span>Rider</span>
          <PersonSuggest
            value={riderQuery}
            onChange={(value) => { setRiderQuery(value); setRiderId('') }}
            placeholder="Search rider name, phone, or plate"
            items={riders.map((row) => ({
              id: row.id,
              name: row.fullName,
              phone: row.phoneNumber,
              photoUrl: row.profilePhotoUrl,
              extra: row.plateNumber,
              vehicleType: row.vehicleType,
            }))}
            onPick={(item) => {
              setRiderQuery(item.name)
              setRiderId(item.id)
              const row = riders.find((entry) => entry.id === item.id)
              const methods = (row?.acceptedPaymentMethods ?? [])
                .map((entry) => normalizePaymentMethod(entry))
                .filter((entry): entry is PaymentMethod => entry != null)
              setRiderPaymentMethods(methods)
              setPaymentMethod(methods[0] ?? 'Cash')
              setPaymentMethodOther('')
            }}
          />
        </label>
        <label className="field">
          <span>Payment method</span>
          <PaymentMethodSuggest
            value={paymentMethod}
            onChange={(method) => {
              setPaymentMethod(method)
              if (method !== 'Other') setPaymentMethodOther('')
            }}
            options={riderPaymentMethods}
            disabled={!riderId || riderPaymentMethods.length === 0}
            placeholder={riderId ? 'Type CASH, GCASH, MAYA, or OTHERS' : 'Choose a rider first'}
          />
        </label>
        {paymentMethod === 'Other' ? (
          <label className="field">
            <span>Others payment details</span>
            <input value={paymentMethodOther} onChange={(e) => setPaymentMethodOther(e.target.value)} placeholder="e.g. Bank transfer, PayMaya QR" />
          </label>
        ) : null}
        <label className="field">
          <span>Pickup date and time</span>
          <input type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} />
        </label>
        <label className="field">
          <span>Distance (km)</span>
          <input value={distanceKm} onChange={(e) => setDistanceKm(e.target.value)} />
        </label>
        <label className="field">
          <span>Passengers</span>
          <input
            value={passengerCount}
            onChange={(e) => setPassengerCount(e.target.value)}
            inputMode="numeric"
            min={1}
          />
          <small className="muted">Used for Tricycle fare tiers. Motorcycle bookings stay at 1.</small>
        </label>
        <label className="field wide">
          <span>Notes</span>
          <input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Optional note for the rider" />
        </label>
      </div>
      <div className="field wide" style={{ marginTop: 16 }}>
        <span>Pickup</span>
        <AddressPicker
          value={pickup}
          onChange={setPickup}
          loadProvinces={() => api.operatorProvinces()}
          loadMunicipalities={(id) => api.operatorMunicipalities(id)}
          loadBarangays={(id) => api.operatorBarangays(id)}
        />
      </div>
      <div className="field wide" style={{ marginTop: 16 }}>
        <span>Drop-off</span>
        <AddressPicker
          value={dropoff}
          onChange={setDropoff}
          loadProvinces={() => api.operatorProvinces()}
          loadMunicipalities={(id) => api.operatorMunicipalities(id)}
          loadBarangays={(id) => api.operatorBarangays(id)}
        />
      </div>
      <div style={{ display: 'flex', gap: 10, maxWidth: 320, marginTop: 16 }}>
        <button className="btn" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Create schedule'}</button>
      </div>
    </form>
  )
}

function OperatorScheduleDetail({ id, onBack }: { id: string; onBack: () => void }) {
  const [canCancel, setCanCancel] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  async function cancel() {
    if (!window.confirm('Are you sure you want to cancel the scheduled booking?')) {
      return
    }
    setError('')
    setBusy(true)
    try {
      await api.cancelScheduledBooking(id)
      onBack()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not cancel this scheduled booking.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      {error ? <p className="error">{error}</p> : null}
      <BookingDetailPage
        loadKey={id}
        load={async () => {
          const ride = await api.scheduledBooking(id)
          setCanCancel(ride.status === 'Pending' || ride.status === 'Waiting')
          return ride
        }}
        onBack={onBack}
        backLabel="Back to schedule"
        allowReassign
        extra={canCancel ? (
          <button className="btn tiny danger" type="button" disabled={busy} onClick={() => void cancel()}>
            {busy ? 'Cancelling…' : 'Cancel scheduled booking'}
          </button>
        ) : null}
      />
    </>
  )
}

function OperatorOverviewPage({ onOpen }: { onOpen: (page: PageId) => void }) {
  const [data, setData] = useState<OperatorOverview | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    api.operatorOverview().then(setData).catch((err: Error) => setError(err.message))
  }, [])

  if (error) return <p className="error">{error}</p>
  if (!data) return <p>Loading overview…</p>

  const series = data.series ?? []
  const ongoingMax = Math.max(data.riders, data.ongoingNow, 1)
  const ongoingChart = [{ name: 'Ongoing', value: data.ongoingNow, fill: '#0284c7' }]

  return (
    <>
      <div className="stats">
        <div className="card">
          <label>Sales today</label>
          <strong>{peso(data.salesToday)}</strong>
        </div>
        <Stat label="Pending" value={data.pendingNow} tone="pending" />
        <Stat label="Ongoing" value={data.ongoingNow} tone="ongoing" />
        <Stat label="Complete today" value={data.completeToday} tone="completed" />
      </div>
      <div className="charts-4">
        <div className="card">
          <div className="panel-head">
            <h2>Sales</h2>
            <span className="muted">Last 7 days</span>
          </div>
          <div className="chart chart-sm">
            <ResponsiveContainer>
              <AreaChart data={series}>
                <XAxis dataKey="date" tickFormatter={chartDay} tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip formatter={(value) => peso(Number(value ?? 0))} labelFormatter={(label) => chartDay(String(label))} />
                <Area type="monotone" dataKey="sales" name="Sales" stroke="#1ea36a" fill="rgba(30, 163, 106, 0.22)" strokeWidth={2.2} />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>
        <div className="card">
          <div className="panel-head">
            <h2>Pending</h2>
            <span className="muted">Open bookings by day</span>
          </div>
          <div className="chart chart-sm">
            <ResponsiveContainer>
              <BarChart data={series}>
                <XAxis dataKey="date" tickFormatter={chartDay} tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                <Tooltip labelFormatter={(label) => chartDay(String(label))} />
                <Bar dataKey="pending" name="Pending" fill="#d48b00" radius={[8, 8, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
        <div className="card">
          <div className="panel-head">
            <h2>Ongoing</h2>
            <span className="muted">Live trips now</span>
          </div>
          <div className="chart chart-sm chart-radial">
            <ResponsiveContainer>
              <RadialBarChart data={ongoingChart} innerRadius="68%" outerRadius="100%" startAngle={90} endAngle={-270}>
                <PolarAngleAxis type="number" domain={[0, ongoingMax]} tick={false} />
                <RadialBar dataKey="value" background cornerRadius={8} />
                <Tooltip />
              </RadialBarChart>
            </ResponsiveContainer>
            <div className="chart-radial-label">
              <strong>{data.ongoingNow}</strong>
              <span>of {data.riders} riders</span>
            </div>
          </div>
        </div>
        <div className="card">
          <div className="panel-head">
            <h2>Complete</h2>
            <span className="muted">Finished trips</span>
          </div>
          <div className="chart chart-sm">
            <ResponsiveContainer>
              <LineChart data={series}>
                <XAxis dataKey="date" tickFormatter={chartDay} tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                <Tooltip labelFormatter={(label) => chartDay(String(label))} />
                <Line type="monotone" dataKey="complete" name="Complete" stroke="#4caf2a" strokeWidth={2.4} dot={{ r: 3 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>
      <div className="grid-2" style={{ marginTop: 16 }}>
        <div className="card">
          <h2>{data.companyName}</h2>
          <p className="muted">Pending platform commission {peso(data.pendingCommission)}</p>
          <p className="muted">{data.openSos} open SOS · {data.openTickets} open tickets · {data.unreadInbox} unread inbox</p>
          <div className="tag-row" style={{ marginTop: 12 }}>
            <StatusTag active={data.isActive} />
          </div>
        </div>
        <div className="card">
          <h2>Quick open</h2>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 12 }}>
            <button className="btn tiny" type="button" onClick={() => onOpen('dashboard')}>Dashboard</button>
            <button className="btn tiny" type="button" onClick={() => onOpen('bookings')}>Booking</button>
            <button className="btn tiny" type="button" onClick={() => onOpen('schedule')}>Schedule booking</button>
            <button className="btn tiny" type="button" onClick={() => onOpen('riders')}>Riders</button>
            <button className="btn tiny" type="button" onClick={() => onOpen('customers')}>Customers</button>
            <button className="btn tiny" type="button" onClick={() => onOpen('fleet')}>Fleet</button>
            <button className="btn tiny" type="button" onClick={() => onOpen('support')}>Support</button>
            <button className="btn tiny" type="button" onClick={() => onOpen('inbox')}>Inbox</button>
            <button className="btn tiny" type="button" onClick={() => onOpen('fares')}>Fare matrix</button>
            <button className="btn tiny" type="button" onClick={() => onOpen('surcharges')}>Surcharges</button>
          </div>
        </div>
      </div>
    </>
  )
}

function OperatorFleetPage({ theme }: { theme: Theme }) {
  const [data, setData] = useState<OperatorFleet | null>(null)
  const [error, setError] = useState('')
  const [vehicle, setVehicle] = useState<VehicleType | ''>('')
  const [focusId, setFocusId] = useState<string | null>(null)

  useEffect(() => {
    function load() {
      api.operatorFleet()
        .then((next) => {
          setData(next)
          setError('')
        })
        .catch((err: Error) => setError(err.message))
    }
    load()
    const handle = window.setInterval(load, 8000)
    return () => window.clearInterval(handle)
  }, [])

  const riders = (data?.riders ?? []).filter((item) => !vehicle || item.vehicleType === vehicle)

  return (
    <div className="fleet-page">
      <div className="stats">
        <Stat label="Active riders" value={data?.active ?? 0} hint="Accounts that can take trips" />
        <Stat label="On the map" value={data?.onMap ?? 0} hint="Active riders with a live location" />
        <Stat label="Motorcycle" value={data?.motorcycle ?? 0} />
        <Stat label="Tricycle" value={data?.tricycle ?? 0} />
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="fleet-layout">
        <div className="card fleet-list">
          <div className="chips" style={{ marginBottom: 12 }}>
            <button type="button" className={vehicle === '' ? 'on' : ''} onClick={() => { setVehicle(''); setFocusId(null) }}>All</button>
            <button type="button" className={vehicle === 'Motorcycle' ? 'on' : ''} onClick={() => { setVehicle('Motorcycle'); setFocusId(null) }}>Motorcycle</button>
            <button type="button" className={vehicle === 'Tricycle' ? 'on' : ''} onClick={() => { setVehicle('Tricycle'); setFocusId(null) }}>Tricycle</button>
          </div>
          <p className="muted" style={{ marginTop: 0 }}>Tap a rider to center the map. Status colors match the map pins below.</p>
          {riders.length === 0 ? (
            <p className="muted">{data ? 'No active riders on the map for that filter.' : 'Loading fleet…'}</p>
          ) : riders.map((rider) => {
            const duty = fleetDuty(rider.status, rider.isOnline, rider.lastLocationAtUtc)
            return (
            <button
              key={rider.id}
              type="button"
              className={`fleet-rider tone-${duty}${focusId === rider.id ? ' on' : ''}`}
              onClick={() => setFocusId(rider.id)}
            >
              <Avatar name={rider.fullName} photoUrl={rider.profilePhotoUrl} size={40} />
              <div className="fleet-rider-body">
                <div className="fleet-rider-top">
                  <strong>{rider.fullName}</strong>
                  <FleetDutyTag status={rider.status} isOnline={rider.isOnline} lastLocationAtUtc={rider.lastLocationAtUtc} />
                </div>
                <div className="fleet-rider-meta">{rider.vehicleType} · {rider.plateNumber}</div>
                {rider.bookingReference ? (
                  <div className="fleet-rider-trip">{rider.bookingReference}</div>
                ) : null}
                <div className="fleet-rider-meta">Last seen {phDateTime(rider.lastLocationAtUtc)}</div>
              </div>
            </button>
            )
          })}
        </div>
        <div className="card fleet-map">
          <FleetMap key={vehicle || 'all'} riders={riders} focusId={focusId} theme={theme} />
          <div className="fleet-legend">
            {(['available', 'pending', 'waiting', 'ongoing', 'offline'] as FleetDuty[]).map((duty) => (
              <span key={duty} className={`fleet-legend-item ${duty}`}>
                <span className={`fleet-heart ${duty}`} aria-hidden="true">
                  <span className="fleet-heart-pulse" />
                  <span className="fleet-heart-core" />
                </span>
                {fleetDutyLabel(duty)}
              </span>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}

function OperatorCustomersPage() {
  const [customerId, setCustomerId] = useState<string | null>(null)
  if (customerId) {
    return <OperatorCustomerDetailPage customerId={customerId} onBack={() => setCustomerId(null)} />
  }
  return <OperatorCustomerListPage onOpen={setCustomerId} />
}

function OperatorCustomerListPage({ onOpen }: { onOpen: (id: string) => void }) {
  const [q, setQ] = useState('')
  const [items, setItems] = useState<CustomerListItem[]>([])
  const [error, setError] = useState('')

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.opCustomers(q)
        .then((rows) => {
          setItems(rows)
          setError('')
        })
        .catch((err: Error) => {
          setItems([])
          setError(err.message)
        })
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q])

  return (
    <div className="card">
      <div className="toolbar">
        <h2 style={{ margin: 0 }}>Customers</h2>
        <PersonSuggest
          value={q}
          onChange={setQ}
          placeholder="Search name or phone"
          items={items.map((row) => ({
            id: row.id,
            name: row.fullName || 'Customer',
            phone: row.phoneNumber,
            photoUrl: row.photoUrl,
          }))}
          onPick={(item) => onOpen(item.id)}
        />
      </div>
      <p className="muted" style={{ marginTop: 0 }}>
        Customers who booked with your riders. Open a row for details and trips under your company.
      </p>
      {items.some((row) => row.deleteStatus === 'Pending') ? (
        <div className="card delete-alert" style={{ marginBottom: 16, borderLeft: '4px solid #d48b00' }}>
          <p style={{ margin: 0 }}>
            <strong>{items.filter((row) => row.deleteStatus === 'Pending').length}</strong>
            {' '}customer{items.filter((row) => row.deleteStatus === 'Pending').length === 1 ? '' : 's'} requested account deletion.
          </p>
        </div>
      ) : null}
      {error ? <p className="error">{error}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>First name</th>
              <th>Last name</th>
              <th>Phone</th>
              <th>Registered</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={5}>
                  {q.trim()
                    ? 'No customers match that search.'
                    : 'No customers yet for your riders. They appear after a booking is linked to a customer.'}
                </td>
              </tr>
            ) : items.map((row) => (
              <tr key={row.id} className="clickable" onClick={() => onOpen(row.id)}>
                <td>
                  <div className="person-cell">
                    <Avatar name={row.fullName || 'Customer'} photoUrl={row.photoUrl} />
                    <span>{row.firstName || row.fullName}</span>
                  </div>
                </td>
                <td>{row.lastName || '—'}</td>
                <td>{row.phoneNumber}</td>
                <td>{phDate(row.registeredAtUtc)}</td>
                <td>
                  <div className="tag-row" style={{ marginTop: 0 }}>
                    <StatusTag active={row.isActive} />
                    {row.deleteStatus === 'Pending' ? <span className="tag pending">Delete requested</span> : null}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function OperatorCustomerDetailPage({ customerId, onBack }: { customerId: string; onBack: () => void }) {
  const [customer, setCustomer] = useState<CustomerDetail | null>(null)
  const [rideId, setRideId] = useState<string | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    api.opCustomer(customerId)
      .then(setCustomer)
      .catch((err: Error) => setError(err.message))
  }, [customerId])

  if (!customer) {
    return error ? <p className="error">{error}</p> : <p>Loading customer…</p>
  }

  if (rideId) {
    return (
      <BookingDetailPage
        load={() => api.opCustomerRide(customerId, rideId)}
        loadKey={rideId}
        onBack={() => setRideId(null)}
        allowReassign
      />
    )
  }

  const del = customer.deleteRequest

  return (
    <div className="card">
      <div className="panel-head">
        <div className="person-cell" style={{ alignItems: 'flex-start' }}>
          <ClickableAvatar name={customer.fullName} photoUrl={customer.photoUrl} size={72} />
          <div>
            <button className="btn tiny" type="button" onClick={onBack}>Back to customers</button>
            <h2 style={{ marginTop: 12 }}>{customer.fullName}</h2>
            <p>{customer.phoneNumber}</p>
            <div className="tag-row">
              <StatusTag active={customer.isActive} />
              {del.status !== 'None' ? <span className={`tag ${del.status.toLowerCase()}`}>Delete {del.status.toLowerCase()}</span> : null}
            </div>
          </div>
        </div>
        <div className="rider-photos">
          <div>
            <span>Profile photo</span>
            {customer.photoUrl ? (
              <PhotoThumb src={customer.photoUrl} alt={`${customer.fullName} profile`} className="id-preview large" />
            ) : (
              <p className="muted">No profile photo yet.</p>
            )}
          </div>
        </div>
      </div>
      <div className="detail-grid">
        <DetailItem label="First name" value={customer.firstName} />
        <DetailItem label="Last name" value={customer.lastName} />
        <DetailItem label="Phone" value={customer.phoneNumber} />
        <DetailItem label="Registered" value={phDateTime(customer.registeredAtUtc)} />
        <DetailItem label="Status" value={customer.isActive ? 'Active' : 'Inactive'} />
        <DetailItem label="Delete request" value={del.status === 'None' ? 'None' : del.status} />
      </div>
      {error ? <p className="error">{error}</p> : null}
      <p className="muted">Trips and bookings below are only those assigned to your riders.</p>
      <RidesReport
        sourceKey={`operator-customer:${customerId}`}
        fetchRides={(opts) => api.opCustomerRides(customerId, opts)}
        onOpenRide={setRideId}
      />
    </div>
  )
}

function OperatorRidersPage() {
  const [view, setView] = useState<'list' | 'create' | 'detail' | 'edit' | 'invite' | 'applications' | 'application'>('list')
  const [riderId, setRiderId] = useState<string | null>(null)
  const [applicationId, setApplicationId] = useState<string | null>(null)
  if (view === 'create') {
    return <OperatorRiderForm onDone={(id) => { setRiderId(id); setView('detail') }} onCancel={() => setView('list')} />
  }
  if (view === 'edit' && riderId) {
    return <OperatorRiderForm riderId={riderId} onDone={() => setView('detail')} onCancel={() => setView('detail')} />
  }
  if ((view === 'detail' || view === 'edit') && riderId) {
    return (
      <OperatorRiderDetail
        riderId={riderId}
        onBack={() => { setRiderId(null); setView('list') }}
        onEdit={() => setView('edit')}
      />
    )
  }
  if (view === 'invite') {
    return <OperatorRiderInvitePage onBack={() => setView('list')} />
  }
  if (view === 'application' && applicationId) {
    return (
      <OperatorRiderApplicationDetail
        applicationId={applicationId}
        onBack={() => { setApplicationId(null); setView('applications') }}
        onApproved={(riderProfileId) => {
          setRiderId(riderProfileId)
          setView('detail')
        }}
      />
    )
  }
  if (view === 'applications') {
    return (
      <OperatorRiderApplicationsPage
        onBack={() => setView('list')}
        onOpen={(id) => { setApplicationId(id); setView('application') }}
      />
    )
  }
  return (
    <OperatorRiderList
      onCreate={() => setView('create')}
      onInvite={() => setView('invite')}
      onApplications={() => setView('applications')}
      onOpen={(id) => { setRiderId(id); setView('detail') }}
    />
  )
}

function OperatorRiderList({
  onCreate,
  onInvite,
  onApplications,
  onOpen,
}: {
  onCreate: () => void
  onInvite: () => void
  onApplications: () => void
  onOpen: (id: string) => void
}) {
  const [q, setQ] = useState('')
  const [items, setItems] = useState<RiderListItem[]>([])
  const [suggest, setSuggest] = useState<RiderListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [pendingCount, setPendingCount] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.opRiders(q, page, pageSize)
        .then((data) => { setItems(data.items); setTotal(data.total); setError('') })
        .catch((err: Error) => setError(err.message))
      api.opRiders(q, 1, 8).then((data) => setSuggest(data.items)).catch(() => setSuggest([]))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [q, page])

  useEffect(() => {
    api.operatorRiderApplications({ status: 'Pending', page: 1, pageSize: 1 })
      .then((data) => setPendingCount(data.total))
      .catch(() => setPendingCount(0))
  }, [])

  return (
    <div className="card">
      <div className="toolbar">
        <h2 style={{ margin: 0 }}>Riders</h2>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
          <PersonSuggest
            value={q}
            onChange={(value) => { setQ(value); setPage(1) }}
            placeholder="Search name, phone, or plate"
            items={suggest.map((row) => ({
              id: row.id,
              name: row.fullName,
              phone: row.phoneNumber,
              photoUrl: row.profilePhotoUrl,
              extra: row.plateNumber,
            }))}
            onPick={(item) => { setQ(item.name); setPage(1); onOpen(item.id) }}
          />
          <button className="btn tiny" type="button" onClick={onApplications} style={{ width: 'auto', whiteSpace: 'nowrap' }}>
            Applications{pendingCount > 0 ? ` (${pendingCount})` : ''}
          </button>
          <button className="btn tiny" type="button" onClick={onInvite} style={{ width: 'auto', whiteSpace: 'nowrap' }}>
            Invite link
          </button>
          <button className="btn" type="button" onClick={onCreate} style={{ width: 'auto', whiteSpace: 'nowrap' }}>
            Create rider
          </button>
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Phone</th>
              <th>Vehicle</th>
              <th>Plate</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr><td colSpan={5}>{q.trim() ? 'No riders match that search.' : 'No riders yet. Create the first one.'}</td></tr>
            ) : items.map((row) => (
              <tr key={row.id} className="clickable" onClick={() => onOpen(row.id)}>
                <td>
                  <div className="person-cell">
                    <ClickableAvatar name={row.fullName} photoUrl={row.profilePhotoUrl} />
                    <strong>{row.fullName}</strong>
                  </div>
                </td>
                <td>{row.phoneNumber}</td>
                <td><VehicleTag type={row.vehicleType} /></td>
                <td>{row.plateNumber}</td>
                <td><StatusTag active={row.isActive} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function OperatorRiderInvitePage({ onBack }: { onBack: () => void }) {
  const [invite, setInvite] = useState<RiderInviteLink | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.operatorRiderInvite()
      .then(setInvite)
      .catch((err: Error) => setError(err.message))
  }, [])

  const joinUrl = invite ? `${window.location.origin}${invite.joinPath}` : ''
  const statusUrl = invite ? `${window.location.origin}${invite.statusPath}` : ''

  async function copy(text: string, label: string) {
    try {
      await navigator.clipboard.writeText(text)
      setNotice(`${label} copied.`)
    } catch {
      setNotice('Could not copy. Select the link and copy it manually.')
    }
  }

  async function regenerate() {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const next = await api.regenerateOperatorRiderInvite()
      setInvite(next)
      setNotice('Invite link regenerated. Old links no longer work.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not regenerate invite.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card">
      <div className="panel-head">
        <div>
          <button className="btn tiny" type="button" onClick={onBack}>Back</button>
          <h2 style={{ marginTop: 12 }}>Rider invite link</h2>
          <p className="muted">Share this link so riders can register themselves. Applications stay pending until you approve them.</p>
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="muted">{notice}</p> : null}
      {!invite ? <p>Loading invite…</p> : (
        <>
          <label className="field wide">
            <span>Registration link</span>
            <input readOnly value={joinUrl} onFocus={(e) => e.target.select()} />
          </label>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginTop: 10 }}>
            <button className="btn" type="button" onClick={() => copy(joinUrl, 'Registration link')} style={{ width: 'auto' }}>Copy link</button>
            <button className="btn tiny" type="button" disabled={busy} onClick={regenerate} style={{ width: 'auto' }}>
              {busy ? 'Working…' : 'Regenerate link'}
            </button>
          </div>
          <label className="field wide" style={{ marginTop: 18 }}>
            <span>Status check link</span>
            <input readOnly value={statusUrl} onFocus={(e) => e.target.select()} />
          </label>
          <div style={{ marginTop: 10 }}>
            <button className="btn tiny" type="button" onClick={() => copy(statusUrl, 'Status link')} style={{ width: 'auto' }}>Copy status link</button>
          </div>
        </>
      )}
    </div>
  )
}

function OperatorRiderApplicationsPage({
  onBack,
  onOpen,
}: {
  onBack: () => void
  onOpen: (id: string) => void
}) {
  const [status, setStatus] = useState('Pending')
  const [q, setQ] = useState('')
  const [items, setItems] = useState<RiderApplicationListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    const handle = window.setTimeout(() => {
      api.operatorRiderApplications({ status, q, page, pageSize })
        .then((data) => { setItems(data.items); setTotal(data.total); setError('') })
        .catch((err: Error) => setError(err.message))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [status, q, page])

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <button className="btn tiny" type="button" onClick={onBack}>Back to riders</button>
          <h2 style={{ marginTop: 12, marginBottom: 0 }}>Rider applications</h2>
        </div>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
          <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
            <option value="Pending">Pending</option>
            <option value="Approved">Approved</option>
            <option value="Rejected">Rejected</option>
            <option value="">All</option>
          </select>
          <input
            value={q}
            onChange={(e) => { setQ(e.target.value); setPage(1) }}
            placeholder="Search name, phone, or plate"
            style={{ minWidth: 220 }}
          />
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Phone</th>
              <th>Vehicle</th>
              <th>Plate</th>
              <th>Status</th>
              <th>Submitted</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr><td colSpan={6}>No applications in this filter.</td></tr>
            ) : items.map((row) => (
              <tr key={row.id} className="clickable" onClick={() => onOpen(row.id)}>
                <td><strong>{row.fullName}</strong></td>
                <td>{row.phoneNumber}</td>
                <td><VehicleTag type={row.vehicleType} /></td>
                <td>{row.plateNumber}</td>
                <td>{row.status}</td>
                <td>{new Date(row.createdAtUtc).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
    </div>
  )
}

function OperatorRiderApplicationDetail({
  applicationId,
  onBack,
  onApproved,
}: {
  applicationId: string
  onBack: () => void
  onApproved: (riderProfileId: string) => void
}) {
  const [row, setRow] = useState<RiderApplicationDetail | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [rejectNote, setRejectNote] = useState('')

  useEffect(() => {
    api.operatorRiderApplication(applicationId)
      .then(setRow)
      .catch((err: Error) => setError(err.message))
  }, [applicationId])

  async function approve() {
    setBusy(true)
    setError('')
    try {
      const saved = await api.approveOperatorRiderApplication(applicationId)
      setRow(saved)
      if (saved.riderProfileId) {
        onApproved(saved.riderProfileId)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not approve.')
    } finally {
      setBusy(false)
    }
  }

  async function reject() {
    setBusy(true)
    setError('')
    try {
      const saved = await api.rejectOperatorRiderApplication(applicationId, rejectNote.trim() || undefined)
      setRow(saved)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not reject.')
    } finally {
      setBusy(false)
    }
  }

  if (!row) return error ? <p className="error">{error}</p> : <p>Loading application…</p>

  return (
    <div className="card">
      <div className="panel-head">
        <div className="person-cell" style={{ alignItems: 'flex-start' }}>
          <ClickableAvatar name={row.fullName} photoUrl={row.profilePhotoUrl} size={72} />
          <div>
            <button className="btn tiny" type="button" onClick={onBack}>Back</button>
            <h2 style={{ marginTop: 12 }}>{row.fullName}</h2>
            <p>{row.phoneNumber}</p>
            <p className="muted">Status: {row.status}</p>
          </div>
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="detail-grid">
        <div><span className="muted">Vehicle</span><p><VehicleTag type={row.vehicleType} /> {row.plateNumber}</p></div>
        <div><span className="muted">Franchise no.</span><p>{row.vehicleFranchiseNumber || '—'}</p></div>
        <div><span className="muted">Model</span><p>{row.vehicleModel || '—'}</p></div>
        <div><span className="muted">License</span><p>{row.licenseType} · {row.licenseNumber}</p></div>
        <div><span className="muted">Address</span><p>{row.fullAddress}</p></div>
        <div><span className="muted">Payment methods</span><p>{row.acceptedPaymentMethods.join(', ') || '—'}</p></div>
        <div><span className="muted">Submitted</span><p>{new Date(row.createdAtUtc).toLocaleString()}</p></div>
        {row.reviewNote ? <div><span className="muted">Review note</span><p>{row.reviewNote}</p></div> : null}
      </div>
      {(row.profilePhotoUrl || row.licensePhotoUrl) ? (
        <div className="form-grid" style={{ marginTop: 16 }}>
          {row.profilePhotoUrl ? (
            <div className="field">
              <span>Profile photo</span>
              <a href={row.profilePhotoUrl} target="_blank" rel="noreferrer">
                <img src={row.profilePhotoUrl} alt="Profile" style={{ maxWidth: 180, borderRadius: 8 }} />
              </a>
            </div>
          ) : null}
          {row.licensePhotoUrl ? (
            <div className="field">
              <span>License photo</span>
              <a href={row.licensePhotoUrl} target="_blank" rel="noreferrer">
                <img src={row.licensePhotoUrl} alt="License" style={{ maxWidth: 180, borderRadius: 8 }} />
              </a>
            </div>
          ) : null}
        </div>
      ) : null}
      {row.status === 'Pending' ? (
        <div style={{ marginTop: 18, display: 'grid', gap: 12, maxWidth: 480 }}>
          <label className="field">
            <span>Reject note (optional)</span>
            <input value={rejectNote} onChange={(e) => setRejectNote(e.target.value)} placeholder="Shown on status check if rejected" />
          </label>
          <div style={{ display: 'flex', gap: 10 }}>
            <button className="btn" type="button" disabled={busy} onClick={approve}>{busy ? 'Working…' : 'Approve'}</button>
            <button className="btn tiny" type="button" disabled={busy} onClick={reject}>Reject</button>
          </div>
        </div>
      ) : null}
    </div>
  )
}

function PublicRiderJoinPage({
  token,
  brandName,
  brandLogo,
}: {
  token: string
  brandName: string
  brandLogo: string
}) {
  const [companyName, setCompanyName] = useState('')
  const [statusPath, setStatusPath] = useState('/ops/#/rider-status')
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  const [vehicleType, setVehicleType] = useState<VehicleType>('Motorcycle')
  const [plateNumber, setPlateNumber] = useState('')
  const [vehicleFranchiseNumber, setVehicleFranchiseNumber] = useState('')
  const [vehicleModel, setVehicleModel] = useState('')
  const [licenseType, setLicenseType] = useState('')
  const [licenseNumber, setLicenseNumber] = useState('')
  const [address, setAddress] = useState<AddressValue>({ province: null, municipality: null, barangay: null, details: '' })
  const [profilePhoto, setProfilePhoto] = useState<File | null>(null)
  const [licensePhoto, setLicensePhoto] = useState<File | null>(null)
  const [acceptedPaymentMethods, setAcceptedPaymentMethods] = useState<PaymentMethod[]>(['Cash', 'GCash'])
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(false)
  const licenses: IdName[] = [
    { id: 'Student Permit', name: 'Student Permit' },
    { id: 'Non-Professional', name: 'Non-Professional' },
    { id: 'Professional', name: 'Professional' },
  ]

  useEffect(() => {
    api.publicRiderInvite(token)
      .then((info) => {
        setCompanyName(info.companyName)
        setStatusPath(info.statusPath)
        setError('')
      })
      .catch((err: Error) => setError(err.message))
  }, [token])

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!address.barangay) {
      setError('Choose a full address.')
      return
    }
    if (acceptedPaymentMethods.length === 0) {
      setError('Select at least one payment method you accept.')
      return
    }
    if (!vehicleFranchiseNumber.trim()) {
      setError('Enter the vehicle franchise number.')
      return
    }
    if (password.trim().length < 6) {
      setError('Set a password of at least 6 characters.')
      return
    }
    if (password !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const data = new FormData()
      data.append('fullName', fullName)
      data.append('phone', phone)
      data.append('password', password.trim())
      data.append('vehicleType', vehicleType)
      data.append('plateNumber', plateNumber)
      data.append('vehicleFranchiseNumber', vehicleFranchiseNumber.trim())
      data.append('vehicleModel', vehicleModel)
      data.append('licenseType', licenseType)
      data.append('licenseNumber', licenseNumber)
      data.append('addressBarangayId', address.barangay.id)
      data.append('addressDetails', address.details)
      acceptedPaymentMethods.forEach((method) => data.append('acceptedPaymentMethods', method))
      if (profilePhoto) data.append('profilePhoto', profilePhoto)
      if (licensePhoto) data.append('licensePhoto', licensePhoto)
      await api.publicRiderApply(token, data)
      setDone(true)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not submit registration.')
    } finally {
      setBusy(false)
    }
  }

  if (done) {
    return (
      <div className="login">
        <div className="login-card" style={{ maxWidth: 520 }}>
          <img className="brand-mark" src={brandLogo} alt={brandName} />
          <h1>Registration submitted</h1>
          <p>Your application for <strong>{companyName || 'this operator'}</strong> is pending review.</p>
          <p className="muted">Keep your mobile number and password. You will use them to sign in to the rider app after approval.</p>
          <p><strong>Mobile:</strong> {phone}</p>
          <a className="btn" href={statusPath} style={{ display: 'inline-block', marginTop: 12, textAlign: 'center' }}>
            Check registration status
          </a>
        </div>
      </div>
    )
  }

  return (
    <div className="login" style={{ alignItems: 'flex-start', padding: '32px 16px' }}>
      <form className="login-card" style={{ maxWidth: 720, width: '100%' }} onSubmit={submit}>
        <img className="brand-mark" src={brandLogo} alt={brandName} />
        <h1>Rider registration</h1>
        <p className="muted">{companyName ? `Join ${companyName}` : 'Loading invite…'}</p>
        {error ? <p className="error">{error}</p> : null}
        <div className="form-grid">
          <label className="field"><span>Full name</span><input value={fullName} onChange={(e) => setFullName(e.target.value)} required /></label>
          <label className="field"><span>Mobile number</span><input value={phone} onChange={(e) => setPhone(e.target.value)} required /></label>
          <label className="field">
            <span>Password</span>
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" required />
          </label>
          <label className="field">
            <span>Confirm password</span>
            <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" required />
          </label>
          <label className="field"><span>Plate</span><input value={plateNumber} onChange={(e) => setPlateNumber(e.target.value)} required /></label>
          <label className="field"><span>Franchise no.</span><input value={vehicleFranchiseNumber} onChange={(e) => setVehicleFranchiseNumber(e.target.value)} required /></label>
          <label className="field"><span>Vehicle model</span><input value={vehicleModel} onChange={(e) => setVehicleModel(e.target.value)} /></label>
          <label className="field">
            <span>License type</span>
            <LookupSuggest
              query={licenseType}
              onQuery={setLicenseType}
              items={licenses}
              placeholder="Select license type"
              onPick={(item) => setLicenseType(item.name)}
            />
          </label>
          <label className="field"><span>License number</span><input value={licenseNumber} onChange={(e) => setLicenseNumber(e.target.value)} required /></label>
          <div className="field wide">
            <span>Vehicle</span>
            <div className="chips" style={{ marginTop: 8 }}>
              <button type="button" className={vehicleType === 'Motorcycle' ? 'on' : ''} onClick={() => setVehicleType('Motorcycle')}>Motorcycle</button>
              <button type="button" className={vehicleType === 'Tricycle' ? 'on' : ''} onClick={() => setVehicleType('Tricycle')}>Tricycle</button>
            </div>
          </div>
        </div>
        <PaymentMethodPicker value={acceptedPaymentMethods} onChange={setAcceptedPaymentMethods} />
        <AddressPicker
          value={address}
          onChange={setAddress}
          loadProvinces={() => api.publicProvinces()}
          loadMunicipalities={(id) => api.publicMunicipalities(id)}
          loadBarangays={(id) => api.publicBarangays(id)}
        />
        <div className="form-grid">
          <label className="field">
            <span>Profile photo</span>
            <input type="file" accept="image/*" onChange={(e) => setProfilePhoto(e.target.files?.[0] ?? null)} />
          </label>
          <label className="field">
            <span>License photo</span>
            <input type="file" accept="image/*" onChange={(e) => setLicensePhoto(e.target.files?.[0] ?? null)} />
          </label>
        </div>
        <button className="btn" type="submit" disabled={busy || !companyName} style={{ marginTop: 16 }}>
          {busy ? 'Submitting…' : 'Submit registration'}
        </button>
        <p className="muted" style={{ marginTop: 12 }}>
          Already applied? <a href={statusPath}>Check status</a>
        </p>
      </form>
    </div>
  )
}

function PublicRiderStatusPage({
  brandName,
  brandLogo,
}: {
  brandName: string
  brandLogo: string
}) {
  const [phone, setPhone] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<{ status: string; label: string; companyName: string | null; message: string | null } | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    setResult(null)
    try {
      setResult(await api.publicRiderApplicationStatus(phone))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not check status.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="login">
      <form className="login-card" onSubmit={submit}>
        <img className="brand-mark" src={brandLogo} alt={brandName} />
        <h1>Registration status</h1>
        <p className="muted">Enter the mobile number you used when you registered.</p>
        {error ? <p className="error">{error}</p> : null}
        <label className="field">
          <span>Mobile number</span>
          <input value={phone} onChange={(e) => setPhone(e.target.value)} required />
        </label>
        <button className="btn" type="submit" disabled={busy}>{busy ? 'Checking…' : 'Check status'}</button>
        {result ? (
          <div style={{ marginTop: 18 }}>
            <p><strong>{result.label}</strong>{result.companyName ? ` · ${result.companyName}` : ''}</p>
            {result.message ? <p className="muted">{result.message}</p> : null}
          </div>
        ) : null}
      </form>
    </div>
  )
}

function OperatorRiderForm({
  riderId,
  onDone,
  onCancel,
}: {
  riderId?: string
  onDone: (id: string) => void
  onCancel: () => void
}) {
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  const [vehicleType, setVehicleType] = useState<VehicleType>('Motorcycle')
  const [plateNumber, setPlateNumber] = useState('')
  const [vehicleFranchiseNumber, setVehicleFranchiseNumber] = useState('')
  const [vehicleModel, setVehicleModel] = useState('')
  const [licenseType, setLicenseType] = useState('')
  const [licenseNumber, setLicenseNumber] = useState('')
  const [address, setAddress] = useState<AddressValue>({ province: null, municipality: null, barangay: null, details: '' })
  const [profilePhoto, setProfilePhoto] = useState<File | null>(null)
  const [licensePhoto, setLicensePhoto] = useState<File | null>(null)
  const [acceptedPaymentMethods, setAcceptedPaymentMethods] = useState<PaymentMethod[]>(['Cash', 'GCash'])
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [existing, setExisting] = useState<RiderDetail | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const licenses: IdName[] = [
    { id: 'Student Permit', name: 'Student Permit' },
    { id: 'Non-Professional', name: 'Non-Professional' },
    { id: 'Professional', name: 'Professional' },
  ]

  useEffect(() => {
    if (!riderId) return
    api.opRider(riderId).then((row) => {
      setExisting(row)
      setFullName(row.fullName)
      setPhone(row.phoneNumber)
      setVehicleType(row.vehicleType)
      setPlateNumber(row.plateNumber)
      setVehicleFranchiseNumber(row.vehicleFranchiseNumber ?? '')
      setVehicleModel(row.vehicleModel ?? '')
      setLicenseType(row.licenseType)
      setLicenseNumber(row.licenseNumber)
      setAddress({
        province: row.address.provinceId ? { id: row.address.provinceId, name: row.address.province } : null,
        municipality: row.address.municipalityId ? { id: row.address.municipalityId, name: row.address.municipality } : null,
        barangay: row.address.barangayId ? { id: row.address.barangayId, name: row.address.barangay } : null,
        details: row.address.details,
      })
      setAcceptedPaymentMethods(
        row.acceptedPaymentMethods.length > 0
          ? row.acceptedPaymentMethods
              .map((entry) => normalizePaymentMethod(entry))
              .filter((entry): entry is PaymentMethod => entry != null)
          : ['Cash'],
      )
    }).catch((err: Error) => setError(err.message))
  }, [riderId])

  async function save(e: FormEvent) {
    e.preventDefault()
    if (!address.barangay) {
      setError('Choose a full address.')
      return
    }
    if (acceptedPaymentMethods.length === 0) {
      setError('Select at least one payment method this rider accepts.')
      return
    }
    if (!vehicleFranchiseNumber.trim()) {
      setError('Enter the vehicle franchise number.')
      return
    }
    if (!riderId && password.trim().length < 6) {
      setError('Set a password of at least 6 characters.')
      return
    }
    if (password.trim().length > 0 && password !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }
    if (riderId && password.trim().length > 0 && password.trim().length < 6) {
      setError('Password must be at least 6 characters.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const data = new FormData()
      data.append('fullName', fullName)
      data.append('phone', phone)
      if (password.trim()) data.append('password', password.trim())
      data.append('vehicleType', vehicleType)
      data.append('plateNumber', plateNumber)
      data.append('vehicleFranchiseNumber', vehicleFranchiseNumber.trim())
      data.append('vehicleModel', vehicleModel)
      data.append('licenseType', licenseType)
      data.append('licenseNumber', licenseNumber)
      data.append('addressBarangayId', address.barangay.id)
      data.append('addressDetails', address.details)
      acceptedPaymentMethods.forEach((method) => data.append('acceptedPaymentMethods', method))
      if (profilePhoto) data.append('profilePhoto', profilePhoto)
      if (licensePhoto) data.append('licensePhoto', licensePhoto)
      const saved = riderId
        ? await api.updateOperatorRider(riderId, data)
        : await api.createOperatorRider(data)
      onDone(saved.id)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save rider.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="card" onSubmit={save}>
      <div className="panel-head">
        <div>
          <button className="btn tiny" type="button" onClick={onCancel}>Back</button>
          <h2 style={{ marginTop: 12 }}>{riderId ? 'Edit rider' : 'Create rider'}</h2>
          <p className="muted">They sign in with their phone number and this password.</p>
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <div className="form-grid">
        <label className="field"><span>Full name</span><input value={fullName} onChange={(e) => setFullName(e.target.value)} /></label>
        <label className="field"><span>Phone</span><input value={phone} onChange={(e) => setPhone(e.target.value)} /></label>
        <label className="field">
          <span>{riderId ? 'New password (optional)' : 'Password'}</span>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" />
        </label>
        <label className="field">
          <span>Confirm password</span>
          <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
        </label>
        <label className="field"><span>Plate</span><input value={plateNumber} onChange={(e) => setPlateNumber(e.target.value)} /></label>
        <label className="field"><span>Franchise no.</span><input value={vehicleFranchiseNumber} onChange={(e) => setVehicleFranchiseNumber(e.target.value)} required /></label>
        <label className="field"><span>Vehicle model</span><input value={vehicleModel} onChange={(e) => setVehicleModel(e.target.value)} /></label>
        <label className="field">
          <span>License type</span>
          <LookupSuggest
            query={licenseType}
            onQuery={setLicenseType}
            items={licenses}
            placeholder="Select license type"
            onPick={(item) => setLicenseType(item.name)}
          />
        </label>
        <label className="field"><span>License number</span><input value={licenseNumber} onChange={(e) => setLicenseNumber(e.target.value)} /></label>
        <div className="field wide">
          <span>Vehicle</span>
          <div className="chips" style={{ marginTop: 8 }}>
            <button type="button" className={vehicleType === 'Motorcycle' ? 'on' : ''} onClick={() => setVehicleType('Motorcycle')}>Motorcycle</button>
            <button type="button" className={vehicleType === 'Tricycle' ? 'on' : ''} onClick={() => setVehicleType('Tricycle')}>Tricycle</button>
          </div>
        </div>
      </div>
      <PaymentMethodPicker value={acceptedPaymentMethods} onChange={setAcceptedPaymentMethods} />
      <AddressPicker
        value={address}
        onChange={setAddress}
        loadProvinces={() => api.operatorProvinces()}
        loadMunicipalities={(id) => api.operatorMunicipalities(id)}
        loadBarangays={(id) => api.operatorBarangays(id)}
      />
      <p className="muted" style={{ marginTop: 8 }}>Choose any province, city, and barangay in the Philippines.</p>
      <div className="form-grid">
        <label className="field">
          <span>Profile photo</span>
          <input type="file" accept="image/*" onChange={(e) => setProfilePhoto(e.target.files?.[0] ?? null)} />
          {existing?.profilePhotoUrl && !profilePhoto ? <small className="muted">Current photo kept unless you pick a new one.</small> : null}
        </label>
        <label className="field">
          <span>License photo</span>
          <input type="file" accept="image/*" onChange={(e) => setLicensePhoto(e.target.files?.[0] ?? null)} />
          {existing?.licensePhotoUrl && !licensePhoto ? <small className="muted">Current photo kept unless you pick a new one.</small> : null}
        </label>
      </div>
      <div style={{ display: 'flex', gap: 10, maxWidth: 280, marginTop: 14 }}>
        <button className="btn" type="submit" disabled={busy}>{busy ? 'Saving…' : riderId ? 'Save rider' : 'Create rider'}</button>
      </div>
    </form>
  )
}

function OperatorRiderDetail({ riderId, onBack, onEdit }: { riderId: string; onBack: () => void; onEdit: () => void }) {
  const [rider, setRider] = useState<RiderDetail | null>(null)
  const [rideId, setRideId] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [actionError, setActionError] = useState('')
  const [resetOpen, setResetOpen] = useState(false)
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [resetBusy, setResetBusy] = useState(false)

  useEffect(() => {
    api.opRider(riderId).then(setRider).catch((err: Error) => setError(err.message))
  }, [riderId])

  if (!rider) return error ? <p className="error">{error}</p> : <p>Loading rider…</p>
  if (rideId) {
    return (
      <BookingDetailPage
        load={() => api.opRiderRide(riderId, rideId)}
        loadKey={rideId}
        onBack={() => setRideId(null)}
        allowReassign
        commissionView="rider"
      />
    )
  }

  async function resetPassword() {
    if (newPassword.trim().length < 6) {
      setActionError('Password must be at least 6 characters.')
      return
    }
    if (newPassword !== confirmPassword) {
      setActionError('Passwords do not match.')
      return
    }
    setActionError('')
    setNotice('')
    setResetBusy(true)
    try {
      const result = await api.resetOperatorRiderPassword(riderId, newPassword.trim())
      setNotice(result.message)
      setResetOpen(false)
      setNewPassword('')
      setConfirmPassword('')
    } catch (err) {
      setActionError(err instanceof Error ? err.message : 'Could not reset password.')
    } finally {
      setResetBusy(false)
    }
  }

  async function toggle() {
    const next = await api.setOperatorRiderActive(riderId, !rider!.isActive)
    setRider(next)
  }

  return (
    <div className="card">
      <div className="panel-head">
        <div className="person-cell" style={{ alignItems: 'flex-start' }}>
          <ClickableAvatar name={rider.fullName} photoUrl={rider.profilePhotoUrl} size={72} />
          <div>
            <button className="btn tiny" type="button" onClick={onBack}>Back to riders</button>
            <h2 style={{ marginTop: 12 }}>{rider.fullName}</h2>
            <p>{rider.phoneNumber}</p>
            <p>{rider.fullAddress || 'No address yet'}</p>
            <p>{rider.vehicleType} · {rider.plateNumber}</p>
            <p>Franchise: {rider.vehicleFranchiseNumber || '—'}</p>
            <p>Credibility: {rider.credibilityScore ?? 100}{rider.riderCancelCount ? ` · ${rider.riderCancelCount} cancel${rider.riderCancelCount === 1 ? '' : 's'}` : ''}</p>
            <div className="tag-row" style={{ marginTop: 10 }}>
              <VehicleTag type={rider.vehicleType} />
              <StatusTag active={rider.isActive} />
              {rider.acceptedPaymentMethods.map((method) => {
                const normalized = normalizePaymentMethod(method)
                return normalized ? <PaymentMethodTag key={normalized} method={normalized} /> : null
              })}
            </div>
          </div>
        </div>
        <div style={{ display: 'grid', gap: 8, justifyItems: 'end' }}>
          <div className="rider-photos">
            {rider.profilePhotoUrl ? <PhotoThumb src={rider.profilePhotoUrl} alt="Profile" className="id-preview large" /> : null}
            {rider.licensePhotoUrl ? <PhotoThumb src={rider.licensePhotoUrl} alt="License" className="id-preview large" /> : null}
          </div>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
            <button className="btn tiny" type="button" onClick={onEdit}>Edit</button>
            <button className="btn tiny" type="button" onClick={() => { setResetOpen((open) => !open); setActionError(''); setNotice('') }}>
              {resetOpen ? 'Cancel reset' : 'Reset password'}
            </button>
            <button className={`btn tiny ${rider.isActive ? 'danger' : ''}`} type="button" onClick={() => void toggle()}>
              {rider.isActive ? 'Deactivate' : 'Activate'}
            </button>
          </div>
        </div>
      </div>
      {actionError ? <p className="error">{actionError}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      {resetOpen ? (
        <div className="form-grid" style={{ marginBottom: 16 }}>
          <label className="field">
            <span>New password</span>
            <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} autoComplete="new-password" />
          </label>
          <label className="field">
            <span>Confirm password</span>
            <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
          </label>
          <div style={{ display: 'flex', alignItems: 'end' }}>
            <button className="btn" type="button" disabled={resetBusy} onClick={() => void resetPassword()}>
              {resetBusy ? 'Saving…' : 'Save password'}
            </button>
          </div>
        </div>
      ) : null}
      <RiderWalletPanel riderId={riderId} acceptedMethods={rider.acceptedPaymentMethods} />
      <RidesReport
        sourceKey={riderId}
        fetchRides={(opts) => api.opRiderRides(riderId, opts)}
        onOpenRide={setRideId}
        commissionView="rider"
      />
    </div>
  )
}

function deriveRatesAsFare(rates: DeriveFareRates | null): FareRates | null {
  if (!rates) return null
  return {
    vehicleType: rates.vehicleType,
    municipalityId: '',
    municipalityName: '',
    baseFare: rates.baseFare,
    perKm: rates.perKm,
    minimumFare: rates.minimumFare,
    includedKm: rates.includedKm,
    operatorCommissionPercent: rates.operatorCommissionPercent,
    driverCommissionPercent: rates.driverCommissionPercent,
    isActive: rates.isActive,
    passengerTiers: rates.passengerTiers,
    surcharges: [],
    samples: rates.samples,
  }
}

function OperatorDeriveFaresPage() {
  const [items, setItems] = useState<DeriveFareZoneListItem[] | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)
  const [name, setName] = useState('')
  const [maxDropoffKm, setMaxDropoffKm] = useState('10')
  const [priority, setPriority] = useState('0')
  const [isActive, setIsActive] = useState(true)
  const [polygon, setPolygon] = useState<DeriveFareLatLng[]>([])
  const [motorcycle, setMotorcycle] = useState<FareDraft>(fareDraft(null, 10, true))
  const [tricycle, setTricycle] = useState<FareDraft>(fareDraft(null, 5, false))
  const [linked, setLinked] = useState(true)
  const [systemMc, setSystemMc] = useState(10)
  const [systemTrike, setSystemTrike] = useState(5)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  const matrixStub: OperatorFareMatrix = {
    operatorId: '',
    operatorName: '',
    operatorActive: true,
    motorcycleCommissionPercent: systemMc,
    tricycleCommissionPercent: systemTrike,
    municipalityId: null,
    municipalityName: null,
    municipalities: [],
    motorcycle: null,
    tricycle: null,
  }

  async function refreshList() {
    const res = await api.deriveFareZones()
    setItems(res.items)
  }

  useEffect(() => {
    refreshList().catch((err: Error) => setError(err.message))
  }, [])

  function resetEditor(detail?: DeriveFareZoneDetail | null) {
    if (!detail) {
      setName('')
      setMaxDropoffKm('10')
      setPriority('0')
      setIsActive(true)
      setPolygon([])
      setMotorcycle(fareDraft(null, systemMc, true))
      setTricycle(fareDraft(null, systemTrike, false))
      setLinked(true)
      return
    }
    setName(detail.name)
    setMaxDropoffKm(String(detail.maxDropoffKm))
    setPriority(String(detail.priority))
    setIsActive(detail.isActive)
    setPolygon(detail.polygon ?? [])
    setSystemMc(detail.motorcycleCommissionPercent)
    setSystemTrike(detail.tricycleCommissionPercent)
    const mc = fareDraft(deriveRatesAsFare(detail.motorcycle), detail.motorcycleCommissionPercent, true)
    const trike = fareDraft(deriveRatesAsFare(detail.tricycle), detail.tricycleCommissionPercent, false)
    setMotorcycle(mc)
    setTricycle(trike)
    setLinked(sameDraft(mc, trike) || !detail.tricycle)
  }

  async function openCreate() {
    setError('')
    setNotice('')
    setEditingId(null)
    setCreating(true)
    try {
      const fares = await api.opFares()
      setSystemMc(fares.motorcycleCommissionPercent)
      setSystemTrike(fares.tricycleCommissionPercent)
      resetEditor(null)
      setMotorcycle(fareDraft(null, fares.motorcycleCommissionPercent, true))
      setTricycle(fareDraft(null, fares.tricycleCommissionPercent, false))
    } catch {
      resetEditor(null)
    }
  }

  async function openEdit(id: string) {
    setError('')
    setNotice('')
    setBusy(true)
    try {
      const detail = await api.deriveFareZone(id)
      setEditingId(id)
      setCreating(false)
      resetEditor(detail)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load zone.')
    } finally {
      setBusy(false)
    }
  }

  function closeEditor() {
    setCreating(false)
    setEditingId(null)
    setError('')
  }

  function applyCommission(current: FareDraft, system: number, patch: Partial<FareDraft>): FareDraft {
    const next = { ...current, ...patch }
    if (patch.operatorCommissionPercent != null && patch.driverCommissionPercent == null) {
      next.driverCommissionPercent = remainderPercent(system, next.operatorCommissionPercent)
    }
    if (patch.driverCommissionPercent != null && patch.operatorCommissionPercent == null) {
      next.operatorCommissionPercent = remainderPercent(system, next.driverCommissionPercent)
    }
    return next
  }

  function changeRates(vehicle: VehicleType, patch: Partial<FareDraft>) {
    const commissionPatch = patch.operatorCommissionPercent != null || patch.driverCommissionPercent != null
    if (linked && !commissionPatch) {
      setMotorcycle((current) => ({ ...current, ...patch }))
      setTricycle((current) => ({ ...current, ...patch }))
      return
    }
    if (vehicle === 'Motorcycle') {
      setMotorcycle((current) => applyCommission(current, systemMc, patch))
    } else {
      setTricycle((current) => applyCommission(current, systemTrike, patch))
    }
  }

  async function save() {
    const maxKm = Number(maxDropoffKm)
    if (!name.trim()) {
      setError('Name is required.')
      return
    }
    if (!Number.isFinite(maxKm) || maxKm <= 0) {
      setError('Max drop-off km must be greater than zero.')
      return
    }
    if (polygon.length < 3) {
      setError('Draw a polygon with at least 3 points.')
      return
    }
    const trikeDraft = linked
      ? { ...motorcycle, operatorCommissionPercent: tricycle.operatorCommissionPercent, driverCommissionPercent: tricycle.driverCommissionPercent }
      : tricycle
    if (commissionSum(systemMc, motorcycle) !== 100 || commissionSum(systemTrike, trikeDraft) !== 100) {
      setError('System, operator, and driver commission must add up to 100% for each vehicle.')
      return
    }
    setBusy(true)
    setError('')
    try {
      const body = {
        name: name.trim(),
        maxDropoffKm: maxKm,
        isActive,
        priority: Math.floor(Number(priority) || 0),
        polygon,
        motorcycle: parseDraft(motorcycle, true),
        tricycle: parseDraft(trikeDraft, false),
      }
      const saved = editingId
        ? await api.updateDeriveFareZone(editingId, body)
        : await api.createDeriveFareZone(body)
      await refreshList()
      setNotice(`${saved.name} saved.`)
      closeEditor()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save derive fare zone.')
    } finally {
      setBusy(false)
    }
  }

  async function toggle(item: DeriveFareZoneListItem) {
    try {
      await api.toggleDeriveFareZone(item.id)
      await refreshList()
      setNotice(`${item.name} is now ${item.isActive ? 'inactive' : 'active'}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not toggle zone.')
    }
  }

  async function remove(item: DeriveFareZoneListItem) {
    if (!window.confirm(`Delete ${item.name}?`)) return
    try {
      await api.deleteDeriveFareZone(item.id)
      await refreshList()
      setNotice(`${item.name} deleted.`)
      if (editingId === item.id) closeEditor()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete zone.')
    }
  }

  if (creating || editingId) {
    return (
      <div className="card">
        <div className="toolbar">
          <div>
            <button className="btn tiny" type="button" onClick={closeEditor}>Back to list</button>
            <h2 style={{ marginTop: 12 }}>{editingId ? 'Edit derive fare' : 'Add derive fare'}</h2>
            <p className="muted" style={{ margin: '6px 0 0', maxWidth: 560 }}>
              Name the zone, draw its polygon, set max drop-off km, then set rates (same layout as Fare matrix).
              Surcharges come from Rates → Surcharges for the pickup municipality.
            </p>
          </div>
        </div>
        {error ? <p className="error">{error}</p> : null}
        <div className="form-grid" style={{ marginBottom: 16 }}>
          <label className="field wide">
            <span>Name</span>
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Port Of Calapan City Mindoro" />
          </label>
          <label className="field">
            <span>Max drop-off km</span>
            <input type="number" min={0.1} step={0.1} value={maxDropoffKm} onChange={(e) => setMaxDropoffKm(e.target.value)} />
          </label>
          <label className="field">
            <span>Priority</span>
            <input type="number" value={priority} onChange={(e) => setPriority(e.target.value)} />
          </label>
          <label className="field">
            <span>Status</span>
            <select value={isActive ? 'active' : 'inactive'} onChange={(e) => setIsActive(e.target.value === 'active')}>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </label>
        </div>
        <h3 style={{ marginTop: 0 }}>Zone polygon</h3>
        <DeriveZoneMap points={polygon} onChange={setPolygon} />
        <h3>Fare rates</h3>
        <RelatedFareRatesTable
          data={{ ...matrixStub, motorcycleCommissionPercent: systemMc, tricycleCommissionPercent: systemTrike }}
          motorcycle={motorcycle}
          tricycle={tricycle}
          linked={linked}
          onLinked={setLinked}
          onChange={changeRates}
        />
        <div className="modal-actions">
          <button className="btn" type="button" disabled={busy} onClick={() => void save()}>
            {busy ? 'Saving…' : editingId ? 'Save changes' : 'Create zone'}
          </button>
        </div>
      </div>
    )
  }

  if (!items) return error ? <p className="error">{error}</p> : <p>Loading derive fares…</p>

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Derive fare</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 560 }}>
            Special fare zones by map polygon. Pickup inside a zone uses these rates (plus shared surcharges). Outside zones use the municipality Fare matrix.
          </p>
        </div>
        <button className="btn" type="button" style={{ width: 'auto', whiteSpace: 'nowrap' }} onClick={() => void openCreate()}>
          Add zone
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Max km</th>
              <th>Points</th>
              <th>Priority</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr><td colSpan={6}>No derive fare zones yet.</td></tr>
            ) : items.map((item) => (
              <tr key={item.id}>
                <td><strong>{item.name}</strong></td>
                <td>{item.maxDropoffKm}</td>
                <td>{item.pointCount}</td>
                <td>{item.priority}</td>
                <td><span className={`tag ${item.isActive ? 'active' : 'rejected'}`}>{item.isActive ? 'Active' : 'Inactive'}</span></td>
                <td style={{ whiteSpace: 'nowrap', textAlign: 'right' }}>
                  <button className="btn tiny" type="button" onClick={() => void openEdit(item.id)}>Edit</button>
                  {' '}
                  <button className={`btn tiny${item.isActive ? ' danger' : ''}`} type="button" onClick={() => void toggle(item)}>
                    {item.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                  {' '}
                  <button className="btn tiny danger" type="button" onClick={() => void remove(item)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function OperatorFaresPage() {
  const [data, setData] = useState<OperatorFareMatrix | null>(null)
  const [motorcycle, setMotorcycle] = useState<FareDraft>(fareDraft(null))
  const [tricycle, setTricycle] = useState<FareDraft>(fareDraft(null))
  const [linked, setLinked] = useState(true)
  const [municipalityId, setMunicipalityId] = useState<string>('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  function loadMatrix(next: OperatorFareMatrix) {
    const mc = fareDraft(next.motorcycle, next.motorcycleCommissionPercent, true)
    const trike = fareDraft(next.tricycle, next.tricycleCommissionPercent, false)
    setData(next)
    setMunicipalityId(next.municipalityId ?? next.municipalities[0]?.id ?? '')
    setMotorcycle(mc)
    setTricycle(trike)
    setLinked(sameDraft(mc, trike) || !next.tricycle)
  }

  useEffect(() => {
    api.opFares().then(loadMatrix).catch((err: Error) => setError(err.message))
  }, [])

  async function changeMunicipality(nextId: string) {
    setMunicipalityId(nextId)
    setError('')
    setNotice('')
    try {
      loadMatrix(await api.opFares(nextId))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load municipality fares.')
    }
  }

  function applyCommission(current: FareDraft, system: number, patch: Partial<FareDraft>): FareDraft {
    const next = { ...current, ...patch }
    if (patch.operatorCommissionPercent != null && patch.driverCommissionPercent == null) {
      next.driverCommissionPercent = remainderPercent(system, next.operatorCommissionPercent)
    }
    if (patch.driverCommissionPercent != null && patch.operatorCommissionPercent == null) {
      next.operatorCommissionPercent = remainderPercent(system, next.driverCommissionPercent)
    }
    return next
  }

  function changeRates(vehicle: VehicleType, patch: Partial<FareDraft>) {
    const commissionPatch = patch.operatorCommissionPercent != null || patch.driverCommissionPercent != null
    if (linked && !commissionPatch) {
      setMotorcycle((current) => ({ ...current, ...patch }))
      setTricycle((current) => ({ ...current, ...patch }))
      return
    }
    if (vehicle === 'Motorcycle') {
      setMotorcycle((current) => applyCommission(current, data?.motorcycleCommissionPercent ?? 0, patch))
    } else {
      setTricycle((current) => applyCommission(current, data?.tricycleCommissionPercent ?? 0, patch))
    }
  }

  async function saveRates() {
    if (!municipalityId) {
      setError('Choose a municipality in your service area first.')
      setNotice('')
      return
    }
    const mcTotal = commissionSum(data?.motorcycleCommissionPercent ?? 0, motorcycle)
    const trikeDraft = linked ? { ...motorcycle, operatorCommissionPercent: tricycle.operatorCommissionPercent, driverCommissionPercent: tricycle.driverCommissionPercent } : tricycle
    const trikeTotal = commissionSum(data?.tricycleCommissionPercent ?? 0, trikeDraft)
    if (mcTotal !== 100 || trikeTotal !== 100) {
      setError('System, operator, and driver commission must add up to 100% for each vehicle.')
      setNotice('')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      loadMatrix(await api.saveOperatorFareMatrix({
        municipalityId,
        motorcycle: parseDraft(motorcycle, true),
        tricycle: parseDraft(trikeDraft, false),
      }))
      setNotice(linked ? 'Motorcycle and tricycle rates saved together.' : 'Fare matrix saved.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save fares.')
    } finally {
      setBusy(false)
    }
  }

  if (!data) return error ? <p className="error">{error}</p> : <p>Loading fare matrix…</p>

  return (
    <div className="form-sections">
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <div className="card">
        <div className="toolbar" style={{ alignItems: 'flex-start' }}>
          <div>
            <h2 style={{ margin: 0 }}>{data.operatorName}</h2>
            <p className="muted" style={{ margin: '6px 0 0', maxWidth: 560 }}>
              Set motorcycle and tricycle rates for each municipality you serve. Quotes use the pickup city.
            </p>
          </div>
        </div>
        {data.municipalities.length === 0 ? (
          <p className="error">Add service-area barangays first, then create a fare matrix for each municipality.</p>
        ) : (
          <label className="field" style={{ maxWidth: 360, marginTop: 16 }}>
            <span>Municipality</span>
            <select value={municipalityId} onChange={(e) => void changeMunicipality(e.target.value)}>
              {data.municipalities.map((item) => (
                <option key={item.id} value={item.id}>{item.name}</option>
              ))}
            </select>
          </label>
        )}
        {municipalityId && data.municipalityName ? (
          <p className="muted" style={{ margin: '0 0 12px' }}>
            Editing rates for <strong style={{ color: 'var(--text)' }}>{data.municipalityName}</strong>
          </p>
        ) : null}
        <RelatedFareRatesTable
          data={data}
          motorcycle={motorcycle}
          tricycle={linked ? { ...motorcycle, operatorCommissionPercent: tricycle.operatorCommissionPercent, driverCommissionPercent: tricycle.driverCommissionPercent } : tricycle}
          linked={linked}
          onLinked={(value) => {
            setLinked(value)
            if (value) {
              setTricycle((current) => ({
                ...motorcycle,
                operatorCommissionPercent: current.operatorCommissionPercent,
                driverCommissionPercent: current.driverCommissionPercent,
              }))
            }
          }}
          onChange={changeRates}
        />
        <button className="btn" type="button" disabled={busy || !municipalityId} style={{ maxWidth: 240, marginTop: 14 }} onClick={() => void saveRates()}>
          Save fare matrix
        </button>
      </div>
      <div className="card">
        <h2 style={{ marginTop: 0 }}>Distance samples</h2>
        <RelatedFareSamples data={data} />
      </div>
    </div>
  )
}

function OperatorSurchargesPage() {
  const [data, setData] = useState<OperatorFareMatrix | null>(null)
  const [municipalityId, setMunicipalityId] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<RelatedSurcharge | null>(null)
  const [applyTo, setApplyTo] = useState<'Both' | VehicleType>('Both')
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

  function loadMatrix(next: OperatorFareMatrix) {
    setData(next)
    setMunicipalityId(next.municipalityId ?? next.municipalities[0]?.id ?? '')
  }

  useEffect(() => {
    api.opFares().then(loadMatrix).catch((err: Error) => setError(err.message))
  }, [])

  async function changeMunicipality(nextId: string) {
    setMunicipalityId(nextId)
    setError('')
    setNotice('')
    try {
      loadMatrix(await api.opFares(nextId))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load municipality fares.')
    }
  }

  function openCreate() {
    setEditing(null)
    setApplyTo('Both')
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

  function openEdit(item: RelatedSurcharge) {
    setEditing(item)
    setApplyTo(item.vehicleType)
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

  function surchargeBody() {
    return {
      kind,
      name,
      amount: Number(amount),
      windowStart: kind === 'TimeWindow' ? windowStart : null,
      windowEnd: kind === 'TimeWindow' ? windowEnd : null,
      rangeStartUtc: kind === 'DateRange' ? fromPhInput(rangeStart) : null,
      rangeEndUtc: kind === 'DateRange' ? fromPhInput(rangeEnd) : null,
      isActive: surchargeActive,
    }
  }

  async function saveSurcharge() {
    if (!municipalityId) {
      setFormError('Choose a municipality first.')
      return
    }
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
    if (kind === 'DateRange') {
      if (!fromPhInput(rangeStart) || !fromPhInput(rangeEnd)) {
        setFormError('Choose a from and until date/time (Philippine time).')
        return
      }
    }
    setBusy(true)
    setFormError('')
    try {
      const body = {
        ...surchargeBody(),
        name: trimmedName,
        amount: pesos,
      }
      if (editing) {
        loadMatrix(await api.updateOperatorSurcharge(editing.id, body))
        setNotice(`${trimmedName} updated.`)
      } else {
        const vehicleTypes: VehicleType[] = applyTo === 'Both' ? ['Motorcycle', 'Tricycle'] : [applyTo]
        loadMatrix(await api.addOperatorSurcharges({ municipalityId, vehicleTypes, ...body }))
        setNotice(`${trimmedName} added.`)
      }
      closeModal()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Could not save surcharge.')
    } finally {
      setBusy(false)
    }
  }

  if (!data) return error ? <p className="error">{error}</p> : <p>Loading surcharges…</p>

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Surcharges</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 480 }}>
            Time windows and date ranges for {data.operatorName}
            {data.municipalityName ? ` · ${data.municipalityName}` : ''}.
          </p>
          {data.municipalities.length > 0 ? (
            <label className="field" style={{ maxWidth: 320, marginTop: 14, marginBottom: 0 }}>
              <span>Municipality</span>
              <select value={municipalityId} onChange={(e) => void changeMunicipality(e.target.value)}>
                {data.municipalities.map((item) => (
                  <option key={item.id} value={item.id}>{item.name}</option>
                ))}
              </select>
            </label>
          ) : null}
        </div>
        <button className="btn" type="button" style={{ width: 'auto', whiteSpace: 'nowrap' }} disabled={!municipalityId} onClick={openCreate}>
          Add surcharge
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <RelatedSurchargeTable
        items={relatedSurcharges(data)}
        onEdit={openEdit}
        onToggle={(item, isActive) => {
          void api.updateOperatorSurcharge(item.id, {
            kind: item.kind,
            name: item.name,
            amount: item.amount,
            windowStart: item.windowStart,
            windowEnd: item.windowEnd,
            rangeStartUtc: item.rangeStartUtc,
            rangeEndUtc: item.rangeEndUtc,
            isActive,
          })
            .then((next) => {
              loadMatrix(next)
              setNotice(`${item.name} is now ${isActive ? 'active' : 'off'}.`)
            })
            .catch((err: Error) => setError(err.message))
        }}
        onDelete={(item) => {
          if (!window.confirm(`Remove ${item.name} from ${item.vehicleType.toLowerCase()}?`)) {
            return
          }
          void api.deleteOperatorSurcharge(item.id)
            .then((next) => {
              loadMatrix(next)
              setNotice(`${item.name} removed.`)
              if (editing?.id === item.id) {
                closeModal()
              }
            })
            .catch((err: Error) => setError(err.message))
        }}
      />
      {open ? (
        <div className="modal-backdrop" role="presentation" onClick={closeModal}>
          <div
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby="surcharge-modal-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <div>
                <h2 id="surcharge-modal-title">{editing ? 'Edit surcharge' : 'Add surcharge'}</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  {editing
                    ? `Editing ${editing.name} for ${editing.vehicleType.toLowerCase()}. Times use Philippine time.`
                    : 'Choose a time window or date range. Apply to one vehicle or both.'}
                </p>
              </div>
              <button className="btn tiny" type="button" onClick={closeModal}>Close</button>
            </div>
            <div className="form-grid">
              {!editing ? (
                <div className="field wide">
                  <span>Applies to</span>
                  <div className="chips" style={{ marginTop: 8 }}>
                    <button type="button" className={applyTo === 'Both' ? 'on' : ''} onClick={() => setApplyTo('Both')}>Both vehicles</button>
                    <button type="button" className={applyTo === 'Motorcycle' ? 'on' : ''} onClick={() => setApplyTo('Motorcycle')}>Motorcycle</button>
                    <button type="button" className={applyTo === 'Tricycle' ? 'on' : ''} onClick={() => setApplyTo('Tricycle')}>Tricycle</button>
                  </div>
                </div>
              ) : null}
              <label className="field"><span>Name</span><input value={name} onChange={(e) => setName(e.target.value)} placeholder="Night" /></label>
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
                  <button type="button" className={kind === 'TimeWindow' ? 'on' : ''} onClick={() => setKind('TimeWindow')}>Time window</button>
                  <button type="button" className={kind === 'DateRange' ? 'on' : ''} onClick={() => setKind('DateRange')}>Date range</button>
                </div>
              </div>
              {kind === 'TimeWindow' ? (
                <>
                  <label className="field"><span>Start</span><input type="time" value={windowStart} onChange={(e) => setWindowStart(e.target.value)} /></label>
                  <label className="field"><span>End</span><input type="time" value={windowEnd} onChange={(e) => setWindowEnd(e.target.value)} /></label>
                </>
              ) : (
                <>
                  <label className="field"><span>From</span><input type="datetime-local" value={rangeStart} onChange={(e) => setRangeStart(e.target.value)} /></label>
                  <label className="field"><span>Until</span><input type="datetime-local" value={rangeEnd} onChange={(e) => setRangeEnd(e.target.value)} /></label>
                </>
              )}
              <div className="field">
                <span>Status</span>
                <div className="chips" style={{ marginTop: 8 }}>
                  <button type="button" className={surchargeActive ? 'on' : ''} onClick={() => setSurchargeActive(true)}>Active</button>
                  <button type="button" className={!surchargeActive ? 'on' : ''} onClick={() => setSurchargeActive(false)}>Off</button>
                </div>
              </div>
            </div>
            {formError ? <p className="error">{formError}</p> : null}
            <div className="modal-actions">
              <button className="btn" type="button" disabled={busy} onClick={() => void saveSurcharge()}>
                {busy ? 'Saving…' : editing ? 'Save surcharge' : applyTo === 'Both' ? 'Add to both vehicles' : 'Add surcharge'}
              </button>
              <button className="btn ghost" type="button" disabled={busy} onClick={closeModal}>Cancel</button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}

function OperatorSupportPage() {
  const [ticketId, setTicketId] = useState<string | null>(null)
  if (ticketId) {
    return <OperatorSupportDetail ticketId={ticketId} onBack={() => setTicketId(null)} />
  }
  return <OperatorSupportList onOpen={setTicketId} />
}

function OperatorSupportList({ onOpen }: { onOpen: (id: string) => void }) {
  const [q, setQ] = useState('')
  const [kind, setKind] = useState<SupportKind | ''>('')
  const [status, setStatus] = useState<SupportStatus | ''>('')
  const [items, setItems] = useState<SupportTicket[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [openSos, setOpenSos] = useState(0)
  const [openTickets, setOpenTickets] = useState(0)
  const [closedTickets, setClosedTickets] = useState(0)
  const [error, setError] = useState('')
  const pageSize = 10

  useEffect(() => {
    function reload() {
      api.operatorSupport(q, kind, status, page, pageSize)
        .then((data) => {
          setItems(data.items)
          setTotal(data.total)
          setOpenSos(data.openSos)
          setOpenTickets(data.openTickets)
          setClosedTickets(data.closedTickets)
          setError('')
        })
        .catch((err: Error) => setError(err.message))
    }
    const handle = window.setTimeout(reload, 200)
    window.addEventListener(SOS_ALERT_EVENT, reload)
    return () => {
      window.clearTimeout(handle)
      window.removeEventListener(SOS_ALERT_EVENT, reload)
    }
  }, [q, kind, status, page])

  return (
    <div className="form-sections">
      <div className="card">
        <div className="toolbar">
          <div>
            <h2 style={{ margin: 0 }}>Support</h2>
            <p className="muted" style={{ margin: '4px 0 0' }}>Handle tickets and SOS in your area.</p>
          </div>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}>
            <div className="chips">
              <button type="button" className={kind === '' && status === '' ? 'on' : ''} onClick={() => { setKind(''); setStatus(''); setPage(1) }}>All</button>
              <button type="button" className={kind === 'Sos' ? 'on' : ''} onClick={() => { setKind('Sos'); setStatus(''); setPage(1) }}>SOS</button>
              <button type="button" className={status === 'Open' && kind === '' ? 'on' : ''} onClick={() => { setKind(''); setStatus('Open'); setPage(1) }}>Open</button>
              <button type="button" className={status === 'Closed' ? 'on' : ''} onClick={() => { setKind(''); setStatus('Closed'); setPage(1) }}>Closed</button>
            </div>
            <div className="ac">
              <input value={q} onChange={(e) => { setQ(e.target.value); setPage(1) }} placeholder="Search booking or person" />
            </div>
          </div>
        </div>
        <div className="fare-cards three" style={{ marginBottom: 16 }}>
          <div className="detail-card"><span>Open SOS</span><p className="detail-name" style={{ marginTop: 10 }}>{openSos}</p></div>
          <div className="detail-card"><span>Open tickets</span><p className="detail-name" style={{ marginTop: 10 }}>{openTickets}</p></div>
          <div className="detail-card"><span>Closed</span><p className="detail-name" style={{ marginTop: 10 }}>{closedTickets}</p></div>
        </div>
        {error ? <p className="error">{error}</p> : null}
        <div className="table-wrap">
          <table>
            <thead><tr><th>Ticket</th><th>From</th><th>Status</th></tr></thead>
            <tbody>
              {items.length === 0 ? (
                <tr><td colSpan={3}>No tickets yet.</td></tr>
              ) : items.map((item) => (
                <tr key={item.id} className="clickable" onClick={() => onOpen(item.id)}>
                  <td>
                    <div className="tag-row" style={{ marginTop: 0, marginBottom: 6 }}>
                      {item.kind === 'Sos' ? <span className="tag sos">SOS</span> : <span className="tag kind">Support</span>}
                    </div>
                    <strong>{item.subject}</strong>
                    <div className="muted">{item.body}</div>
                    <small className="muted">{phDateTime(item.createdAtUtc)}{item.bookingNumber ? ` · ${item.bookingNumber}` : ''}</small>
                  </td>
                  <td><strong>{item.openedByName}</strong><div className="muted">{item.openedBy} · {item.openedByPhone || '—'}</div></td>
                  <td><SupportStatusTag status={item.status} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Pager page={page} pageSize={pageSize} total={total} onPage={setPage} />
      </div>
    </div>
  )
}

function OperatorSupportDetail({ ticketId, onBack }: { ticketId: string; onBack: () => void }) {
  const [detail, setDetail] = useState<SupportTicketDetail | null>(null)
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [theme] = useState<Theme>(readTheme)

  function loadDetail() {
    return api.operatorSupportTicket(ticketId).then(setDetail)
  }

  useEffect(() => {
    loadDetail().catch((err: Error) => setError(err.message))
  }, [ticketId])

  useEffect(() => {
    function reload(event: Event) {
      const detail = (event as CustomEvent<OpsAlert>).detail
      if (detail?.ticketId && detail.ticketId !== ticketId) {
        return
      }
      loadDetail().catch(() => undefined)
    }
    window.addEventListener(SOS_ALERT_EVENT, reload)
    return () => window.removeEventListener(SOS_ALERT_EVENT, reload)
  }, [ticketId])

  const live = detail?.booking?.status === 'Ongoing' || detail?.booking?.status === 'Waiting'

  useEffect(() => {
    if (!live) {
      return
    }
    const handle = window.setInterval(() => {
      loadDetail().catch(() => undefined)
    }, 8000)
    return () => window.clearInterval(handle)
  }, [ticketId, live])

  const item = detail?.ticket

  if (!item) return error ? <p className="error">{error}</p> : <p>Loading ticket…</p>

  async function addNote() {
    setBusy(true)
    setError('')
    try {
      const next = await api.addOperatorSupportNote(ticketId, note)
      setDetail((current) => current ? { ...current, ticket: next } : current)
      setNote('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save note.')
    } finally {
      setBusy(false)
    }
  }

  async function toggleClose() {
    if (!item) {
      return
    }
    setBusy(true)
    setError('')
    try {
      const next = await api.closeOperatorSupport(ticketId, item.status !== 'Closed')
      setDetail((current) => current ? { ...current, ticket: next } : current)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not update ticket.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="card">
      <div className="panel-head">
        <div>
          <button className="btn tiny" type="button" onClick={onBack}>Back to support</button>
          <h2 style={{ marginTop: 12 }}>{item.subject}</h2>
          <p className="muted">Opened {phDateTime(item.createdAtUtc)} · {item.openedBy} {item.openedByName}</p>
        </div>
        <div className="tag-row" style={{ marginTop: 0 }}>
          {item.kind === 'Sos' ? <span className="tag sos">SOS</span> : <span className="tag kind">Support</span>}
          <SupportStatusTag status={item.status} />
        </div>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {item.kind === 'Sos' && detail ? (
        <>
          <SosTicketMapSection detail={detail} theme={theme} />
          <div className="detail-item wide" style={{ marginTop: 16 }}>
            <span>SOS message</span>
            <p>{item.body}</p>
          </div>
          {detail.booking ? (
            <>
              <h3 style={{ marginTop: 24 }}>Booking details</h3>
              <BookingDetailsBody ride={detail.booking} />
            </>
          ) : null}
        </>
      ) : (
        <div className="detail-grid">
          <DetailItem label="Opened by" value={`${item.openedByName} · ${item.openedByPhone || '—'}`} />
          <DetailItem label="Municipality" value={item.municipality || '—'} />
          <DetailItem label="Booking" value={item.bookingNumber || 'Not tied to a booking'} />
          <div className="detail-item wide"><span>Message</span><p>{item.body}</p></div>
          <div className="detail-item wide"><span>Operator notes</span><p style={{ whiteSpace: 'pre-wrap' }}>{item.operatorNotes || 'No notes yet.'}</p></div>
        </div>
      )}
      <label className="field" style={{ marginTop: 16 }}>
        <span>Add handling note</span>
        <textarea rows={3} value={note} onChange={(e) => setNote(e.target.value)} />
      </label>
      <div style={{ display: 'flex', gap: 8, marginTop: 10, flexWrap: 'wrap' }}>
        <button className="btn" type="button" disabled={busy || !note.trim()} onClick={() => void addNote()}>Save note</button>
        <button className={`btn ${item.status === 'Open' ? 'danger' : ''}`} type="button" disabled={busy} onClick={() => void toggleClose()}>
          {item.status === 'Open' ? 'Close ticket' : 'Reopen ticket'}
        </button>
      </div>
    </div>
  )
}

function OperatorInboxPage({
  onOpenBilling,
  onOpenCustomers,
}: {
  onOpenBilling: () => void
  onOpenCustomers: () => void
}) {
  const [items, setItems] = useState<OperatorInboxItem[]>([])
  const [error, setError] = useState('')

  useEffect(() => {
    api.operatorInbox().then(setItems).catch((err: Error) => setError(err.message))
  }, [])

  async function markRead(id: string) {
    const next = await api.readOperatorInbox(id)
    setItems((rows) => rows.map((row) => row.id === id ? next : row))
  }

  return (
    <div className="card">
      <h2 style={{ marginTop: 0 }}>Inbox</h2>
      <p className="muted">Billing records, account deletion requests, and platform announcements for your company.</p>
      {error ? <p className="error">{error}</p> : null}
      {items.length === 0 ? <p>No notifications yet.</p> : (
        <div className="list">
          {items.map((item) => (
            <div className="row" key={item.id}>
              <div>
                <strong>{item.title}</strong>
                <div className="muted">{item.body}</div>
                <small className="muted">{phDateTime(item.createdAtUtc)} · {item.kind === 'AccountDelete' ? 'Delete request' : item.kind}{item.readAtUtc ? '' : ' · unread'}</small>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                {!item.readAtUtc ? <button className="btn tiny" type="button" onClick={() => void markRead(item.id)}>Mark read</button> : null}
                {item.billId ? <button className="btn tiny" type="button" onClick={onOpenBilling}>Open billing</button> : null}
                {item.kind === 'AccountDelete' ? <button className="btn tiny" type="button" onClick={onOpenCustomers}>Open customers</button> : null}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function OperatorBillingPage() {
  const [data, setData] = useState<BillingOperatorDetail | null>(null)
  const [billId, setBillId] = useState<string | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    api.operatorBilling().then(setData).catch((err: Error) => setError(err.message))
  }, [])

  if (!data) return error ? <p className="error">{error}</p> : <p>Loading billing…</p>

  const selectedBill = billId ? data.bills.find((bill) => bill.id === billId) ?? null : null
  if (selectedBill) {
    return <BillRecordDetail bill={selectedBill} onBack={() => setBillId(null)} />
  }

  return (
    <div className="card">
      <h2 style={{ marginTop: 0 }}>Billing</h2>
      <p className="muted">Super Admin issues bills. Tap a billing record to view trip details.</p>
      <div className="fare-cards">
        <div className="detail-card">
          <span>Pending commission</span>
          <p className="detail-name" style={{ marginTop: 10 }}>{peso(data.pendingCommission)}</p>
          <p className="muted">{data.pendingTripCount} unbilled completed trip{data.pendingTripCount === 1 ? '' : 's'}</p>
        </div>
        <div className="detail-card">
          <span>By vehicle</span>
          <p className="muted" style={{ marginTop: 10 }}>Motorcycle {percent(data.motorcycleCommissionPercent)} · {peso(data.pendingMotorcycle)}</p>
          <p className="muted">Tricycle {percent(data.tricycleCommissionPercent)} · {peso(data.pendingTricycle)}</p>
        </div>
      </div>
      <div className="toolbar" style={{ marginTop: 18 }}>
        <h3 style={{ margin: 0 }}>Billing records</h3>
      </div>
      <BillRecordsList bills={data.bills} onOpen={setBillId} />
    </div>
  )
}

function OperatorPromosPage() {
  const [items, setItems] = useState<OperatorPromoItem[] | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<OperatorPromoItem | null>(null)
  const [discountPercent, setDiscountPercent] = useState('50')
  const [isActive, setIsActive] = useState(true)
  const [startsAt, setStartsAt] = useState('')
  const [endsAt, setEndsAt] = useState('')
  const [maxRedemptions, setMaxRedemptions] = useState('')
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)

  const previewPercent = (() => {
    const n = Math.floor(Number(discountPercent))
    return Number.isFinite(n) && n >= 1 && n <= 100 ? n : null
  })()

  function load(rows: OperatorPromoItem[]) {
    setItems(rows)
  }

  useEffect(() => {
    api.operatorPromos()
      .then((res) => load(res.items))
      .catch((err: Error) => setError(err.message))
  }, [])

  function openCreate() {
    setEditing(null)
    setDiscountPercent('50')
    setIsActive(true)
    setStartsAt('')
    setEndsAt('')
    setMaxRedemptions('')
    setFormError('')
    setOpen(true)
  }

  function openEdit(item: OperatorPromoItem) {
    setEditing(item)
    setDiscountPercent(String(item.discountPercent))
    setIsActive(item.isActive)
    setStartsAt(toPhInput(item.startsAtUtc))
    setEndsAt(toPhInput(item.endsAtUtc))
    setMaxRedemptions(item.maxRedemptions != null ? String(item.maxRedemptions) : '')
    setFormError('')
    setOpen(true)
  }

  function closeModal() {
    setOpen(false)
    setEditing(null)
    setFormError('')
  }

  function body() {
    const percent = Math.floor(Number(discountPercent))
    const maxRaw = maxRedemptions.trim()
    const max = maxRaw ? Math.floor(Number(maxRaw)) : null
    return {
      discountPercent: percent,
      isActive,
      startsAtUtc: fromPhInput(startsAt),
      endsAtUtc: fromPhInput(endsAt),
      maxRedemptions: max != null && Number.isFinite(max) && max > 0 ? max : null,
    }
  }

  async function save() {
    const percent = Math.floor(Number(discountPercent))
    if (!Number.isFinite(percent) || percent < 1 || percent > 100) {
      setFormError('Discount must be between 1 and 100.')
      return
    }
    setBusy(true)
    setFormError('')
    try {
      const payload = body()
      if (editing) {
        const row = await api.updateOperatorPromo(editing.id, payload)
        setItems((prev) => (prev ?? []).map((x) => (x.id === row.id ? row : x)).sort((a, b) => b.discountPercent - a.discountPercent))
        setNotice(`${row.displayCode} updated.`)
      } else {
        const row = await api.createOperatorPromo(payload)
        setItems((prev) => [row, ...(prev ?? [])].sort((a, b) => b.discountPercent - a.discountPercent))
        setNotice(`${row.displayCode} created.`)
      }
      closeModal()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : 'Could not save promo.')
    } finally {
      setBusy(false)
    }
  }

  async function toggle(item: OperatorPromoItem) {
    setError('')
    try {
      const row = await api.toggleOperatorPromo(item.id)
      setItems((prev) => (prev ?? []).map((x) => (x.id === row.id ? row : x)))
      setNotice(`${row.displayCode} is now ${row.isActive ? 'active' : 'inactive'}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not toggle promo.')
    }
  }

  if (!items) return error ? <p className="error">{error}</p> : <p>Loading promos…</p>

  return (
    <div className="card">
      <div className="toolbar">
        <div>
          <h2 style={{ margin: 0 }}>Promos</h2>
          <p className="muted" style={{ margin: '6px 0 0', maxWidth: 520 }}>
            Set any discount from 1–100%. The code is always <strong>Save</strong> plus the percent (Save10, Save50, Save100).
            Customer pays the rest; you settle the discount with the rider offline. Commission stays on the full fare.
          </p>
        </div>
        <button className="btn" type="button" style={{ width: 'auto', whiteSpace: 'nowrap' }} onClick={openCreate}>
          Add promo
        </button>
      </div>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Discount</th>
              <th>Status</th>
              <th>Dates</th>
              <th>Redemptions</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={6}>No promos yet. Add one like Save50 for 50% off.</td>
              </tr>
            ) : items.map((item) => (
              <tr key={item.id}>
                <td><strong>{item.displayCode}</strong></td>
                <td>{item.discountPercent}%</td>
                <td>
                  <span className={`tag ${item.isActive ? 'active' : 'rejected'}`}>
                    {item.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td>
                  {item.startsAtUtc || item.endsAtUtc
                    ? `${item.startsAtUtc ? phDateTime(item.startsAtUtc) : '—'} → ${item.endsAtUtc ? phDateTime(item.endsAtUtc) : '—'}`
                    : 'Always'}
                </td>
                <td>
                  {item.redemptionCount}
                  {item.maxRedemptions != null ? ` / ${item.maxRedemptions}` : ''}
                </td>
                <td style={{ whiteSpace: 'nowrap', textAlign: 'right' }}>
                  <button className="btn tiny" type="button" onClick={() => openEdit(item)}>Edit</button>
                  {' '}
                  <button className={`btn tiny${item.isActive ? ' danger' : ''}`} type="button" onClick={() => void toggle(item)}>
                    {item.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {open ? (
        <div className="modal-backdrop" role="presentation" onClick={closeModal}>
          <div
            className="modal-panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby="promo-modal-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-head">
              <div>
                <h2 id="promo-modal-title">{editing ? 'Edit promo' : 'Add promo'}</h2>
                <p className="muted" style={{ margin: '6px 0 0' }}>
                  Code is generated from the percent. Optional dates use Philippine time.
                </p>
              </div>
              <button className="btn tiny" type="button" onClick={closeModal}>Close</button>
            </div>
            <div className="form-grid">
              <label className="field">
                <span>Discount %</span>
                <input
                  type="number"
                  min={1}
                  max={100}
                  inputMode="numeric"
                  value={discountPercent}
                  onChange={(e) => setDiscountPercent(e.target.value)}
                  placeholder="50"
                />
              </label>
              <div className="field">
                <span>Code</span>
                <input readOnly value={previewPercent != null ? `Save${previewPercent}` : 'Save…'} />
              </div>
              <label className="field">
                <span>Status</span>
                <select value={isActive ? 'active' : 'inactive'} onChange={(e) => setIsActive(e.target.value === 'active')}>
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                </select>
              </label>
              <label className="field">
                <span>Max redemptions (optional)</span>
                <input
                  type="number"
                  min={1}
                  inputMode="numeric"
                  value={maxRedemptions}
                  onChange={(e) => setMaxRedemptions(e.target.value)}
                  placeholder="Unlimited"
                />
              </label>
              <label className="field">
                <span>Starts (optional)</span>
                <input type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} />
              </label>
              <label className="field">
                <span>Ends (optional)</span>
                <input type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)} />
              </label>
            </div>
            {formError ? <p className="error">{formError}</p> : null}
            <div className="modal-actions">
              <button className="btn" type="button" disabled={busy} onClick={() => void save()}>
                {busy ? 'Saving…' : editing ? 'Save changes' : 'Create promo'}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}

function OperatorCompanyPage() {
  const [data, setData] = useState<OperatorDetail | null>(null)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [broadcastRadiusKm, setBroadcastRadiusKm] = useState('5')
  const [liveBookingExpiryMinutes, setLiveBookingExpiryMinutes] = useState('15')
  const [scheduledBookingGraceMinutes, setScheduledBookingGraceMinutes] = useState('20')
  const [busy, setBusy] = useState(false)

  function applyCompany(next: OperatorDetail) {
    setData(next)
    setBroadcastRadiusKm(String(next.broadcastRadiusKm ?? 5))
    setLiveBookingExpiryMinutes(String(next.liveBookingExpiryMinutes ?? 15))
    setScheduledBookingGraceMinutes(String(next.scheduledBookingGraceMinutes ?? 20))
  }

  useEffect(() => {
    api.operatorCompany()
      .then(applyCompany)
      .catch((err: Error) => setError(err.message))
  }, [])

  async function changePassword(e: FormEvent) {
    e.preventDefault()
    if (newPassword.trim().length < 6) {
      setError('New password must be at least 6 characters.')
      return
    }
    if (newPassword !== confirmPassword) {
      setError('New passwords do not match.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const result = await api.changeOperatorPassword(currentPassword, newPassword.trim())
      setNotice(result.message)
      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not change password.')
    } finally {
      setBusy(false)
    }
  }

  async function saveDispatch(mode: 'Broadcast' | 'Selection' | 'Both') {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const radius = Number(broadcastRadiusKm)
      const next = await api.saveOperatorDispatchMode(mode, {
        broadcastRadiusKm: Number.isFinite(radius) ? radius : undefined,
        liveBookingExpiryMinutes: Number(liveBookingExpiryMinutes) || undefined,
        scheduledBookingGraceMinutes: Number(scheduledBookingGraceMinutes) || undefined,
      })
      applyCompany(next)
      setNotice(`Booking dispatch set to ${mode}.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save dispatch mode.')
    } finally {
      setBusy(false)
    }
  }

  async function saveBroadcastRadius(e: FormEvent) {
    e.preventDefault()
    if (!data) return
    const radius = Number(broadcastRadiusKm)
    if (!Number.isFinite(radius) || radius < 1 || radius > 50) {
      setError('Broadcast radius must be between 1 and 50 km.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const mode = data.bookingDispatchMode ?? 'Broadcast'
      const next = await api.saveOperatorDispatchMode(mode, { broadcastRadiusKm: radius })
      applyCompany(next)
      setNotice(`Broadcast radius set to ${next.broadcastRadiusKm ?? radius} km.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save broadcast radius.')
    } finally {
      setBusy(false)
    }
  }

  async function saveBookingExpiry(e: FormEvent) {
    e.preventDefault()
    if (!data) return
    const live = Number(liveBookingExpiryMinutes)
    const grace = Number(scheduledBookingGraceMinutes)
    if (!Number.isFinite(live) || live < 1 || live > 180) {
      setError('Live booking expiry must be between 1 and 180 minutes.')
      return
    }
    if (!Number.isFinite(grace) || grace < 1 || grace > 180) {
      setError('Scheduled booking grace must be between 1 and 180 minutes.')
      return
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const mode = data.bookingDispatchMode ?? 'Broadcast'
      const next = await api.saveOperatorDispatchMode(mode, {
        liveBookingExpiryMinutes: live,
        scheduledBookingGraceMinutes: grace,
      })
      applyCompany(next)
      setNotice(`Booking expiry set to ${next.liveBookingExpiryMinutes ?? live} min live / ${next.scheduledBookingGraceMinutes ?? grace} min scheduled grace.`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save booking expiry.')
    } finally {
      setBusy(false)
    }
  }

  if (!data) return error ? <p className="error">{error}</p> : <p>Loading company…</p>

  return (
    <div className="card">
      <div className="panel-head">
        <div className="person-cell" style={{ alignItems: 'flex-start' }}>
          <Avatar name={data.companyName} photoUrl={data.profilePhotoUrl} size={72} />
          <div>
            <h2 style={{ margin: 0 }}>{data.companyName}</h2>
            <p>{data.contactName} · {data.contactPhone}</p>
            <p>{data.fullAddress}</p>
            <div className="tag-row" style={{ marginTop: 10 }}>
              <StatusTag active={data.isActive} />
              <VehicleTag type="Motorcycle" count={data.ridersMotorcycle} />
              <VehicleTag type="Tricycle" count={data.ridersTricycle} />
            </div>
          </div>
        </div>
        {data.governmentIdPhotoUrl ? <img className="id-preview" src={data.governmentIdPhotoUrl} alt="Government ID" /> : null}
      </div>
      <p className="muted">Company profile, areas, and commission are set by Super Admin. You can choose how customer bookings find a rider.</p>
      {error ? <p className="error">{error}</p> : null}
      {notice ? <p className="ok">{notice}</p> : null}
      <div className="detail-grid">
        <DetailItem label="Government ID" value={`${data.governmentIdType} · ${data.governmentId}`} />
        <DetailItem label="Area of operation" value={data.areaOfOperation} />
        <DetailItem label="System Comm moto" value={percent(data.motorcycleCommissionPercent)} />
        <DetailItem label="System Comm tri" value={percent(data.tricycleCommissionPercent)} />
      </div>
      <h3>Booking dispatch</h3>
      <p className="muted" style={{ marginTop: 0 }}>
        Broadcast: nearby riders race to accept. Selection: customer picks a rider. Both: customer chooses at booking time.
      </p>
      <div className="chips" style={{ marginBottom: 8 }}>
        {([
          ['Broadcast', 'Broadcast'],
          ['Selection', 'Selection'],
          ['Both', 'Both'],
        ] as const).map(([value, label]) => (
          <button
            key={value}
            type="button"
            className={(data.bookingDispatchMode ?? 'Broadcast') === value ? 'on' : ''}
            disabled={busy}
            onClick={() => void saveDispatch(value)}
          >
            {label}
          </button>
        ))}
      </div>
      <form onSubmit={saveBroadcastRadius} style={{ marginBottom: 20, maxWidth: 320 }}>
        <label className="field">
          <span>Broadcast radius (km)</span>
          <input
            type="number"
            min={1}
            max={50}
            step={0.5}
            value={broadcastRadiusKm}
            onChange={(e) => setBroadcastRadiusKm(e.target.value)}
            disabled={busy}
          />
        </label>
        <p className="muted" style={{ margin: '6px 0 10px' }}>
          Only online riders within this distance of pickup get broadcast offers (1–50 km). Current: {data.broadcastRadiusKm ?? 5} km.
        </p>
        <button className="btn tiny" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save radius'}</button>
      </form>
      <h3>Booking expiry</h3>
      <p className="muted" style={{ marginTop: 0 }}>
        Auto-cancel unanswered Pending bookings. Waiting / Ongoing trips (rider already accepted) are not expired.
      </p>
      <form onSubmit={saveBookingExpiry} style={{ marginBottom: 20, maxWidth: 420 }}>
        <div className="form-grid">
          <label className="field">
            <span>Live expiry (minutes)</span>
            <input
              type="number"
              min={1}
              max={180}
              step={1}
              value={liveBookingExpiryMinutes}
              onChange={(e) => setLiveBookingExpiryMinutes(e.target.value)}
              disabled={busy}
            />
          </label>
          <label className="field">
            <span>Scheduled grace (minutes)</span>
            <input
              type="number"
              min={1}
              max={180}
              step={1}
              value={scheduledBookingGraceMinutes}
              onChange={(e) => setScheduledBookingGraceMinutes(e.target.value)}
              disabled={busy}
            />
          </label>
        </div>
        <p className="muted" style={{ margin: '6px 0 10px' }}>
          Live: cancel if still Pending after this many minutes from request (1–180). Scheduled: cancel if still Pending this many minutes after pickup time. Current: {data.liveBookingExpiryMinutes ?? 15} / {data.scheduledBookingGraceMinutes ?? 20}.
        </p>
        <button className="btn tiny" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save expiry'}</button>
      </form>
      <h3>Service areas</h3>
      {data.areas.length === 0 ? <p>No barangays assigned.</p> : (
        <AreaGroups areas={data.areas} />
      )}
      <form onSubmit={changePassword} style={{ marginTop: 24 }}>
        <h3>Change password</h3>
        <p className="muted">Use this to change the operator login password. Super Admin can also reset it.</p>
        {error ? <p className="error">{error}</p> : null}
        {notice ? <p className="ok">{notice}</p> : null}
        <div className="form-grid">
          <label className="field">
            <span>Current password</span>
            <input type="password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} autoComplete="current-password" />
          </label>
          <label className="field">
            <span>New password</span>
            <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} autoComplete="new-password" />
          </label>
          <label className="field">
            <span>Confirm new password</span>
            <input type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
          </label>
        </div>
        <div style={{ marginTop: 14, maxWidth: 220 }}>
          <button className="btn" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save password'}</button>
        </div>
      </form>
    </div>
  )
}
