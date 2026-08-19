const TOKEN_KEY = 'yapasakay-access'
const REFRESH_KEY = 'yapasakay-refresh'

export type UserRole = 'Admin' | 'Operator' | 'Rider' | 'Customer'
export type VehicleType = 'Motorcycle' | 'Tricycle'
export type PageId =
  | 'overview'
  | 'operators'
  | 'customers'
  | 'territories'
  | 'fares'
  | 'surcharges'
  | 'billing'
  | 'announcements'
  | 'support'
  | 'audit'
  | 'settings'
  | 'roles'
  | 'admins'
  | 'profile'
  | 'riders'
  | 'fleet'
  | 'dashboard'
  | 'bookings'
  | 'schedule'
  | 'inbox'
  | 'company'
  | 'wallet'

export type Me = {
  id: string
  phoneNumber: string
  fullName: string
  role: UserRole
  operatorId: string | null
  isActive: boolean
  isMainAdmin: boolean
  accessGroupName: string | null
  companyName: string | null
  accessPages: PageId[]
}

export type AuthResponse = {
  accessToken: string
  refreshToken: string
  expiresAtUtc: string
  user: Me
}

export type OverviewSeriesPoint = {
  date: string
  operatorsCreated: number
  customersRegistered: number
  tripsCompleted: number
}

export type OperatorListItem = {
  id: string
  companyName: string
  contactName: string
  contactPhone: string
  fullAddress: string
  areaOfOperation: string
  governmentIdType: string
  governmentId: string
  profilePhotoUrl: string | null
  governmentIdPhotoUrl: string | null
  isActive: boolean
  motorcycleCommissionPercent: number
  tricycleCommissionPercent: number
  riderCount: number
  ridersMotorcycle: number
  ridersTricycle: number
  createdAtUtc: string
}

export type Overview = {
  operators: number
  riders: number
  ridersMotorcycle: number
  ridersTricycle: number
  customers: number
  tripsToday: number
  adminCutToday: number
  openSos: number
  unreadSosAlerts: number
  pendingAccountDeletes: number
  series: OverviewSeriesPoint[]
  recentOperators: OperatorListItem[]
}

export type RiderListItem = {
  id: string
  fullName: string
  phoneNumber: string
  vehicleType: VehicleType
  plateNumber: string
  vehicleModel: string | null
  isActive: boolean
  licenseType: string
  licenseNumber: string
  profilePhotoUrl: string | null
  licensePhotoUrl: string | null
  acceptedPaymentMethods: PaymentMethod[]
}

export type RiderDetail = RiderListItem & {
  fullAddress: string
  address: OperatorAddress
}

export type TripStatus = 'Completed' | 'Cancelled' | 'Ongoing' | 'Pending' | 'Waiting'

export type PaymentMethod = 'Cash' | 'GCash' | 'Maya' | 'Other'

export const PAYMENT_METHODS: PaymentMethod[] = ['Cash', 'GCash', 'Maya', 'Other']

export const WALLET_PAYMENT_METHODS: PaymentMethod[] = ['Cash', 'GCash', 'Maya', 'Other']

const PAYMENT_METHOD_BY_VALUE: Record<number, PaymentMethod> = {
  1: 'Cash',
  2: 'GCash',
  3: 'Maya',
  4: 'Other',
}

export function normalizePaymentMethod(value: unknown): PaymentMethod | null {
  if (value == null || value === '') return null
  if (typeof value === 'number') return PAYMENT_METHOD_BY_VALUE[value] ?? null
  const text = String(value)
  return PAYMENT_METHODS.includes(text as PaymentMethod) ? text as PaymentMethod : null
}

export function paymentMethodCssKey(value: unknown): string {
  const method = normalizePaymentMethod(value)
  return method ? method.toLowerCase() : 'other'
}

export function paymentMethodLabelUpper(method: PaymentMethod): string {
  if (method === 'GCash') return 'GCASH'
  if (method === 'Other') return 'OTHERS'
  return method.toUpperCase()
}

export function parsePaymentMethodInput(value: string): PaymentMethod | null {
  const compact = value.trim().toLowerCase().replace(/[\s_-]+/g, '')
  if (!compact) return null
  if (compact === 'cash') return 'Cash'
  if (compact === 'gcash') return 'GCash'
  if (compact === 'maya') return 'Maya'
  if (compact === 'other' || compact === 'others') return 'Other'
  return normalizePaymentMethod(value)
}

export type WalletTransactionKind = 'CashIn' | 'CashOut' | 'Commission'

export type WalletTransactionStatus = 'Pending' | 'Approved' | 'Rejected'

export type WalletTransaction = {
  id: string
  kind: WalletTransactionKind
  status: WalletTransactionStatus
  paymentMethod: PaymentMethod | null
  amount: number
  balanceAfter: number | null
  tripId: string | null
  tripReference: string | null
  note: string | null
  rejectionReason: string | null
  createdAtUtc: string
  resolvedAtUtc: string | null
}

export type WalletHistoryItem = WalletTransaction & {
  riderId: string
  riderName: string
  riderPhone: string
  plateNumber: string
}

const WALLET_KIND_BY_VALUE: Record<number, WalletTransactionKind> = {
  1: 'CashIn',
  2: 'CashOut',
  3: 'Commission',
}

const WALLET_STATUS_BY_VALUE: Record<number, WalletTransactionStatus> = {
  1: 'Pending',
  2: 'Approved',
  3: 'Rejected',
}

export function normalizeWalletKind(value: unknown): WalletTransactionKind | null {
  if (value == null || value === '') return null
  if (typeof value === 'number') return WALLET_KIND_BY_VALUE[value] ?? null
  const text = String(value)
  return (['CashIn', 'CashOut', 'Commission'] as const).includes(text as WalletTransactionKind)
    ? text as WalletTransactionKind
    : null
}

export function normalizeWalletStatus(value: unknown): WalletTransactionStatus | null {
  if (value == null || value === '') return null
  if (typeof value === 'number') return WALLET_STATUS_BY_VALUE[value] ?? null
  const text = String(value)
  return (['Pending', 'Approved', 'Rejected'] as const).includes(text as WalletTransactionStatus)
    ? text as WalletTransactionStatus
    : null
}

export type RiderWalletDetail = {
  riderId: string
  riderName: string
  riderPhone: string
  balance: number
  pendingCount: number
  transactions: WalletTransaction[]
}

export type WalletRequest = {
  id: string
  riderId: string
  riderName: string
  riderPhone: string
  plateNumber: string
  kind: WalletTransactionKind
  paymentMethod: PaymentMethod
  amount: number
  note: string | null
  createdAtUtc: string
}

export type RiderWalletBalance = {
  riderId: string
  riderName: string
  riderPhone: string
  plateNumber: string
  vehicleType: VehicleType
  isActive: boolean
  balance: number
  pendingCount: number
}

export type OperatorWalletOverview = {
  totalBalance: number
  pendingRequests: number
  riders: RiderWalletBalance[]
}

export type RideStop = {
  details: string
  barangay: string
  municipality: string
  province: string
  fullAddress: string
}

export type RideListItem = {
  id: string
  reference: string
  requestedAtUtc: string
  pickup: string
  dropoff: string
  customerName: string
  vehicleType: VehicleType
  status: TripStatus
  fare: number
  distanceKm: number
  paymentMethod: PaymentMethod
  paymentMethodOther: string | null
}

export type RideDetail = {
  id: string
  reference: string
  status: TripStatus
  customerName: string
  customerPhone: string
  pickupStop: RideStop
  dropoffStop: RideStop
  pickup: string
  dropoff: string
  notes: string | null
  fare: number
  distanceKm: number
  durationMinutes: number | null
  vehicleType: VehicleType
  requestedAtUtc: string
  scheduledAtUtc: string | null
  completedAtUtc: string | null
  cancelledAtUtc: string | null
  cancelReason: string | null
  rating: number | null
  ratingComment: string | null
  ratedAtUtc: string | null
  paymentMethod: PaymentMethod
  paymentMethodOther: string | null
  operatorId: string
  operatorName: string
  operatorPhone: string
  riderId: string
  riderName: string
  riderPhone: string
  plateNumber: string
  vehicleModel: string | null
  riderPhotoUrl: string | null
  chat: RideChatMessage[]
}

export type ChatSender = 'Customer' | 'Rider'

export type RideChatMessage = {
  id: string
  sender: ChatSender
  body: string
  sentAtUtc: string
  photoUrl?: string | null
}

export type RideSeriesPoint = {
  date: string
  completed: number
}

export type RiderRideSummary = {
  total: number
  completed: number
  cancelled: number
  ongoing: number
  grossFare: number
}

export type RiderRides = {
  summary: RiderRideSummary
  series: RideSeriesPoint[]
  rides: Paged<RideListItem>
}

export type IdName = {
  id: string
  name: string
}

export type BarangayOption = {
  id: string
  name: string
  municipalityId: string
  municipality: string
  provinceId: string
  province: string
}

export type OperatorArea = {
  barangayId: string
  barangay: string
  municipality: string
  province: string
}

export type OperatorAddress = {
  barangayId: string | null
  municipalityId: string | null
  provinceId: string | null
  barangay: string
  municipality: string
  province: string
  details: string
  fullAddress: string
}

export type OperatorDetail = OperatorListItem & {
  riders: RiderListItem[]
  areas: OperatorArea[]
  address: OperatorAddress
}

export type CustomerListItem = {
  id: string
  firstName: string
  lastName: string
  fullName: string
  phoneNumber: string
  registeredAtUtc: string
  isActive: boolean
  photoUrl: string | null
  deleteStatus: DeleteAccountStatus
}

export type DeleteAccountStatus = 'None' | 'Pending' | 'Approved' | 'Rejected'

export type CustomerDeleteRequest = {
  status: DeleteAccountStatus
  requestedAtUtc: string | null
  reason: string | null
  resolvedAtUtc: string | null
  resolutionNote: string | null
}

export type CustomerDetail = {
  id: string
  firstName: string
  lastName: string
  fullName: string
  phoneNumber: string
  registeredAtUtc: string
  isActive: boolean
  photoUrl: string | null
  deleteRequest: CustomerDeleteRequest
}

export type RideQuery = {
  range?: 'weekly' | 'monthly' | 'yearly'
  from?: string
  to?: string
  q?: string
  status?: TripStatus | ''
  page?: number
  pageSize?: number
}

export type TerritoryListItem = {
  id: string
  provinceId: string
  province: string
  municipality: string
  barangays: string[]
  barangayCount: number
  operatorCount: number
}

export type FareSample = {
  distanceKm: number
  fare: number
}

export type SurchargeKind = 'TimeWindow' | 'DateRange'

export type FareSurcharge = {
  id: string
  kind: SurchargeKind
  name: string
  amount: number
  windowStart: string | null
  windowEnd: string | null
  rangeStartUtc: string | null
  rangeEndUtc: string | null
  isActive: boolean
}

export type FareRates = {
  vehicleType: VehicleType
  baseFare: number
  perKm: number
  minimumFare: number
  includedKm: number
  operatorCommissionPercent: number
  driverCommissionPercent: number
  isActive: boolean
  surcharges: FareSurcharge[]
  samples: FareSample[]
}

export type FleetDuty = 'available' | 'pending' | 'waiting' | 'ongoing' | 'offline'

const ONLINE_TTL_MS = 5 * 60 * 1000

export function fleetDuty(
  status: TripStatus | null | undefined,
  isOnline?: boolean,
  lastLocationAtUtc?: string,
): FleetDuty {
  if (status === 'Ongoing') return 'ongoing'
  if (status === 'Waiting') return 'waiting'
  if (status === 'Pending') return 'pending'
  if (!isOnline) return 'offline'
  if (lastLocationAtUtc) {
    const age = Date.now() - new Date(lastLocationAtUtc).getTime()
    if (Number.isFinite(age) && age > ONLINE_TTL_MS) return 'offline'
  }
  return 'available'
}

export function fleetDutyLabel(duty: FleetDuty) {
  if (duty === 'available') return 'Available'
  return duty.slice(0, 1).toUpperCase() + duty.slice(1)
}

export type FleetRider = {
  id: string
  fullName: string
  phoneNumber: string
  vehicleType: VehicleType
  plateNumber: string
  profilePhotoUrl: string | null
  lat: number
  lng: number
  lastLocationAtUtc: string
  isOnline: boolean
  status: TripStatus | null
  bookingReference: string | null
}

export type OperatorFleet = {
  active: number
  onMap: number
  motorcycle: number
  tricycle: number
  riders: FleetRider[]
}

export type OperatorFareMatrix = {
  operatorId: string
  operatorName: string
  operatorActive: boolean
  motorcycleCommissionPercent: number
  tricycleCommissionPercent: number
  motorcycle: FareRates | null
  tricycle: FareRates | null
}

export type BillingOperator = {
  operatorId: string
  companyName: string
  contactName: string
  contactPhone: string
  profilePhotoUrl: string | null
  isActive: boolean
  motorcycleCommissionPercent: number
  tricycleCommissionPercent: number
  pendingCommission: number
  pendingMotorcycle: number
  pendingTricycle: number
  pendingTripCount: number
  oldestUnbilledUtc: string | null
  newestUnbilledUtc: string | null
}

export type BillStatus = 'Issued'

export type BillTrip = {
  atUtc: string
  riderName: string
  bookingNumber: string
  fare: number
  amount: number
}

export type OperatorBill = {
  id: string
  number: string
  status: BillStatus
  amount: number
  motorcycleAmount: number
  tricycleAmount: number
  tripCount: number
  periodFromUtc: string
  periodToUtc: string
  disabledOperator: boolean
  notifiedAtUtc: string
  createdAtUtc: string
  note: string | null
  trips: BillTrip[]
}

export type BillingOperatorDetail = BillingOperator & {
  riderCount: number
  bills: OperatorBill[]
}

export type SearchHit = {
  kind: 'operator' | 'customer'
  id: string
  name: string
  phone: string
  photoUrl: string | null
}

export type Paged<T> = {
  items: T[]
  page: number
  pageSize: number
  total: number
}

export type Announcement = {
  id: string
  title: string
  body: string
  forOperators: boolean
  forRiders: boolean
  forCustomers: boolean
  startsAtUtc: string | null
  endsAtUtc: string | null
  isActive: boolean
  createdAtUtc: string
}

export type SupportKind = 'Support' | 'Sos'
export type SupportStatus = 'Open' | 'Closed'
export type SupportOpenedBy = 'Customer' | 'Rider'

export type SupportTicket = {
  id: string
  kind: SupportKind
  status: SupportStatus
  openedBy: SupportOpenedBy
  openedByName: string
  openedByPhone: string
  subject: string
  body: string
  operatorNotes: string | null
  operatorId: string
  operatorName: string
  operatorPhone: string
  municipality: string
  tripId: string | null
  bookingNumber: string | null
  createdAtUtc: string
  closedAtUtc: string | null
}

export type MapPoint = {
  lat: number
  lng: number
  label: string
  atUtc: string | null
}

export type SupportTicketDetail = {
  ticket: SupportTicket
  booking: RideDetail | null
  sosLocation: MapPoint | null
  riderLocation: MapPoint | null
  pickupLocation: MapPoint | null
  dropoffLocation: MapPoint | null
}

export type SupportInbox = Paged<SupportTicket> & {
  openSos: number
  openTickets: number
  closedTickets: number
  unreadSosAlerts: number
}

export type AdminAlertsSummary = {
  openSos: number
  unreadSosAlerts: number
  pendingBilling: number
  pendingAccountDeletes: number
}

export type OperatorNavAlerts = {
  pendingWalletRequests: number
  openSos: number
  unreadBilling: number
  pendingAccountDeletes: number
}

export type AdminAlertItem = {
  id: string
  kind: 'Billing' | 'Announcement' | 'Sos' | 'AccountDelete'
  title: string
  body: string
  supportTicketId: string | null
  createdAtUtc: string
  readAtUtc: string | null
}

export type OperatorOverviewPoint = {
  date: string
  sales: number
  pending: number
  ongoing: number
  complete: number
}

export type OperatorOverview = {
  companyName: string
  isActive: boolean
  riders: number
  ridersMotorcycle: number
  ridersTricycle: number
  tripsToday: number
  openSos: number
  openTickets: number
  pendingCommission: number
  unreadInbox: number
  salesToday: number
  pendingNow: number
  ongoingNow: number
  completeToday: number
  series: OperatorOverviewPoint[]
}

export type OperatorBookingColumn = {
  total: number
  items: RideListItem[]
}

export type OperatorBookingBoard = {
  pending: OperatorBookingColumn
  waiting: OperatorBookingColumn
  ongoing: OperatorBookingColumn
  completed: OperatorBookingColumn
}

export type ScheduledBooking = {
  id: string
  reference: string
  scheduledAtUtc: string
  customerName: string
  customerPhone: string
  riderId: string
  riderName: string
  plateNumber: string
  vehicleType: VehicleType
  pickup: string
  dropoff: string
  status: TripStatus
  fare: number
  paymentMethod: PaymentMethod
  paymentMethodOther: string | null
}

export type OperatorBookingListItem = {
  id: string
  reference: string
  requestedAtUtc: string
  scheduledAtUtc: string | null
  customerName: string
  customerPhone: string
  riderName: string
  plateNumber: string
  vehicleType: VehicleType
  pickup: string
  dropoff: string
  status: TripStatus
  fare: number
  paymentMethod: PaymentMethod
  paymentMethodOther: string | null
}

export type OperatorInboxItem = {
  id: string
  kind: 'Billing' | 'Announcement' | 'Sos' | 'AccountDelete'
  title: string
  body: string
  billId: string | null
  createdAtUtc: string
  readAtUtc: string | null
}

export type AuditAction =
  | 'OperatorCreated'
  | 'OperatorUpdated'
  | 'OperatorActivated'
  | 'OperatorDeactivated'
  | 'BillIssued'

export type AuditLog = {
  id: string
  action: AuditAction
  actionLabel: string
  summary: string
  operatorId: string
  operatorName: string
  actorUserId: string | null
  actorName: string
  createdAtUtc: string
}

export type AccessPage = {
  id: PageId
  label: string
}

export type AccessGroup = {
  id: string
  name: string
  description: string
  userCount: number
  pages: PageId[]
}

export type AccessStaff = {
  id: string
  fullName: string
  phoneNumber: string
  accessGroupId: string
  accessGroupName: string
  isActive: boolean
  isMainAdmin: boolean
  createdAtUtc: string
}

export type ResetPasswordResult = {
  phoneNumber: string
  otp: string
  message: string
}

export type SuggestItem = {
  id: string
  name: string
  phone: string
  photoUrl?: string | null
  vehicleType?: string | null
  extra?: string
}

export function getToken() {
  return localStorage.getItem(TOKEN_KEY)
}

export function saveAuth(auth: AuthResponse) {
  localStorage.setItem(TOKEN_KEY, auth.accessToken)
  localStorage.setItem(REFRESH_KEY, auth.refreshToken)
}

export function clearAuth() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(REFRESH_KEY)
}

function rideQuery(opts: RideQuery) {
  const page = opts.page ?? 1
  const pageSize = opts.pageSize ?? 10
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (opts.status) {
    params.set('status', opts.status)
  }
  if (opts.q?.trim()) {
    params.set('q', opts.q.trim())
  } else if (opts.from && opts.to) {
    params.set('from', opts.from)
    params.set('to', opts.to)
  } else {
    params.set('range', opts.range ?? 'weekly')
  }
  return params.toString()
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  const isForm = typeof FormData !== 'undefined' && init?.body instanceof FormData
  if (!isForm && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  const token = getToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const res = await fetch(path, { ...init, headers })
  if (res.status === 401) {
    clearAuth()
    let message = 'Session expired. Sign in again.'
    try {
      const body = (await res.json()) as { message?: string }
      if (body.message) {
        message = body.message
      }
    } catch {
      /* ignore */
    }
    throw new Error(message)
  }
  if (!res.ok) {
    let message = 'Request failed.'
    try {
      const body = (await res.json()) as { message?: string }
      if (body.message) {
        message = body.message
      }
    } catch {
      /* ignore */
    }
    throw new Error(message)
  }
  if (res.status === 204) {
    return undefined as T
  }
  return (await res.json()) as T
}

export const api = {
  login: (phone: string, password: string) =>
    request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ phone, password }),
    }),
  requestOtp: (phone: string) =>
    request<{ message: string }>('/api/auth/request-otp', {
      method: 'POST',
      body: JSON.stringify({ phone }),
    }),
  verifyOtp: (phone: string, code: string) =>
    request<AuthResponse>('/api/auth/verify-otp', {
      method: 'POST',
      body: JSON.stringify({ phone, code }),
    }),
  me: () => request<Me>('/api/auth/me'),
  overview: (range: 'weekly' | 'monthly' | 'yearly') =>
    request<Overview>(`/api/admin/overview?range=${range}`),
  search: (q = '') => request<SearchHit[]>(`/api/admin/search?q=${encodeURIComponent(q)}`),
  operators: (q = '', page = 1, pageSize = 10) =>
    request<Paged<OperatorListItem>>(`/api/admin/operators?q=${encodeURIComponent(q)}&page=${page}&pageSize=${pageSize}`),
  operatorRiders: (operatorId: string, q = '', page = 1, pageSize = 10) =>
    request<Paged<RiderListItem>>(`/api/admin/operators/${operatorId}/riders?q=${encodeURIComponent(q)}&page=${page}&pageSize=${pageSize}`),
  operatorRider: (operatorId: string, riderId: string) =>
    request<RiderDetail>(`/api/admin/operators/${operatorId}/riders/${riderId}`),
  riderRides: (
    operatorId: string,
    riderId: string,
    opts: RideQuery = {},
  ) => request<RiderRides>(`/api/admin/operators/${operatorId}/riders/${riderId}/rides?${rideQuery(opts)}`),
  riderRide: (operatorId: string, riderId: string, rideId: string) =>
    request<RideDetail>(`/api/admin/operators/${operatorId}/riders/${riderId}/rides/${rideId}`),
  createOperator: (body: FormData) =>
    request<OperatorListItem>('/api/admin/operators', { method: 'POST', body }),
  updateOperator: (id: string, body: FormData) =>
    request<OperatorListItem>(`/api/admin/operators/${id}`, { method: 'PUT', body }),
  operator: (id: string) => request<OperatorDetail>(`/api/admin/operators/${id}`),
  adminOperatorBookings: (operatorId: string, q = '', page = 1, pageSize = 10, status?: TripStatus | '', from?: string, to?: string) => {
    const params = new URLSearchParams({
      q,
      page: String(page),
      pageSize: String(pageSize),
    })
    if (status) {
      params.set('status', status)
    }
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    return request<Paged<OperatorBookingListItem>>(`/api/admin/operators/${operatorId}/bookings?${params}`)
  },
  adminOperatorBooking: (operatorId: string, bookingId: string) =>
    request<RideDetail>(`/api/admin/operators/${operatorId}/bookings/${bookingId}`),
  setOperatorActive: (id: string, isActive: boolean) =>
    request(`/api/admin/operators/${id}/active`, { method: 'POST', body: JSON.stringify({ isActive }) }),
  resetOperatorPassword: (id: string, password: string) =>
    request<ResetPasswordResult>(`/api/admin/operators/${id}/reset-password`, {
      method: 'POST',
      body: JSON.stringify({ password }),
    }),
  resetRiderPassword: (operatorId: string, riderId: string, password: string) =>
    request<ResetPasswordResult>(`/api/admin/operators/${operatorId}/riders/${riderId}/reset-password`, {
      method: 'POST',
      body: JSON.stringify({ password }),
    }),
  customers: (q = '') => request<CustomerListItem[]>(`/api/admin/customers?q=${encodeURIComponent(q)}`),
  customer: (id: string) => request<CustomerDetail>(`/api/admin/customers/${id}`),
  customerRides: (id: string, opts: RideQuery = {}) =>
    request<RiderRides>(`/api/admin/customers/${id}/rides?${rideQuery(opts)}`),
  customerRide: (id: string, rideId: string) =>
    request<RideDetail>(`/api/admin/customers/${id}/rides/${rideId}`),
  resetCustomerPassword: (id: string) =>
    request<ResetPasswordResult>(`/api/admin/customers/${id}/reset-password`, { method: 'POST' }),
  recordCustomerDelete: (id: string, reason?: string) =>
    request<CustomerDetail>(`/api/admin/customers/${id}/delete-request`, {
      method: 'POST',
      body: JSON.stringify({ reason: reason || null }),
    }),
  resolveCustomerDelete: (id: string, approve: boolean, note?: string) =>
    request<CustomerDetail>(`/api/admin/customers/${id}/delete-request/resolve`, {
      method: 'POST',
      body: JSON.stringify({ approve, note: note || null }),
    }),
  territories: (q = '', page = 1, pageSize = 10) =>
    request<Paged<TerritoryListItem>>(`/api/admin/territories?q=${encodeURIComponent(q)}&page=${page}&pageSize=${pageSize}`),
  fares: (q = '', vehicleType?: VehicleType, page = 1, pageSize = 10) => {
    const params = new URLSearchParams({ q, page: String(page), pageSize: String(pageSize) })
    if (vehicleType) {
      params.set('vehicleType', vehicleType)
    }
    return request<Paged<OperatorFareMatrix>>(`/api/admin/fares?${params}`)
  },
  operatorFares: (operatorId: string) =>
    request<OperatorFareMatrix>(`/api/admin/operators/${operatorId}/fares`),
  billingOperators: (q = '', page = 1, pageSize = 10) =>
    request<Paged<BillingOperator>>(`/api/admin/billing?q=${encodeURIComponent(q)}&page=${page}&pageSize=${pageSize}`),
  billingOperator: (operatorId: string) =>
    request<BillingOperatorDetail>(`/api/admin/billing/${operatorId}`),
  createBill: (operatorId: string, disableOperator: boolean, note?: string) =>
    request<BillingOperatorDetail>(`/api/admin/billing/${operatorId}`, {
      method: 'POST',
      body: JSON.stringify({ disableOperator, note: note || null }),
    }),
  announcements: (q = '', page = 1, pageSize = 10) =>
    request<Paged<Announcement>>(`/api/admin/announcements?q=${encodeURIComponent(q)}&page=${page}&pageSize=${pageSize}`),
  createAnnouncement: (body: {
    title: string
    body: string
    forOperators: boolean
    forRiders: boolean
    forCustomers: boolean
    startsAtUtc: string | null
    endsAtUtc: string | null
  }) => request<Announcement>('/api/admin/announcements', { method: 'POST', body: JSON.stringify(body) }),
  setAnnouncementActive: (id: string, isActive: boolean) =>
    request<Announcement>(`/api/admin/announcements/${id}/active`, {
      method: 'POST',
      body: JSON.stringify({ isActive }),
    }),
  supportTickets: (q = '', kind?: SupportKind | '', status?: SupportStatus | '', page = 1, pageSize = 10) => {
    const params = new URLSearchParams({ q, page: String(page), pageSize: String(pageSize) })
    if (kind) {
      params.set('kind', kind)
    }
    if (status) {
      params.set('status', status)
    }
    return request<SupportInbox>(`/api/admin/support?${params}`)
  },
  supportTicket: (id: string) => request<SupportTicketDetail>(`/api/admin/support/${id}`),
  adminAlerts: () => request<AdminAlertsSummary>('/api/admin/alerts'),
  adminAlertInbox: () => request<AdminAlertItem[]>('/api/admin/alerts/inbox'),
  readAdminAlert: (id: string) =>
    request<AdminAlertItem>(`/api/admin/alerts/${id}/read`, { method: 'POST' }),
  readAllAdminAlerts: () =>
    request<{ message: string }>('/api/admin/alerts/read-all', { method: 'POST' }),
  auditLogs: (q = '', action?: AuditAction | '', page = 1, pageSize = 10) => {
    const params = new URLSearchParams({ q, page: String(page), pageSize: String(pageSize) })
    if (action) {
      params.set('action', action)
    }
    return request<Paged<AuditLog>>(`/api/admin/audit?${params}`)
  },
  accessPages: () => request<AccessPage[]>('/api/admin/access/pages'),
  accessGroups: () => request<AccessGroup[]>('/api/admin/access/groups'),
  createAccessGroup: (body: { name: string; description: string; pages: PageId[] }) =>
    request<AccessGroup>('/api/admin/access/groups', { method: 'POST', body: JSON.stringify(body) }),
  updateAccessGroup: (id: string, body: { name: string; description: string; pages: PageId[] }) =>
    request<AccessGroup>(`/api/admin/access/groups/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteAccessGroup: (id: string) =>
    request<{ ok: boolean }>(`/api/admin/access/groups/${id}/delete`, { method: 'POST' }),
  accessUsers: () => request<AccessStaff[]>('/api/admin/access/users'),
  createAccessUser: (body: { fullName: string; phone: string; accessGroupId: string; password: string }) =>
    request<AccessStaff>('/api/admin/access/users', { method: 'POST', body: JSON.stringify(body) }),
  updateAccessUser: (id: string, body: { fullName: string; phone: string; accessGroupId: string; password?: string }) =>
    request<AccessStaff>(`/api/admin/access/users/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  setAccessUserActive: (id: string, isActive: boolean) =>
    request<AccessStaff>(`/api/admin/access/users/${id}/active`, {
      method: 'POST',
      body: JSON.stringify({ isActive }),
    }),
  resetAccessUserPassword: (id: string, password: string) =>
    request<ResetPasswordResult>(`/api/admin/access/users/${id}/reset-password`, {
      method: 'POST',
      body: JSON.stringify({ password }),
    }),
  changeAdminPassword: (currentPassword: string, newPassword: string) =>
    request<{ message: string }>('/api/admin/profile/password', {
      method: 'POST',
      body: JSON.stringify({ currentPassword, newPassword }),
    }),
  provinces: () => request<IdName[]>('/api/admin/territories/provinces'),
  municipalities: (provinceId: string) =>
    request<IdName[]>(`/api/admin/territories/municipalities?provinceId=${encodeURIComponent(provinceId)}`),
  barangays: (municipalityId: string) =>
    request<BarangayOption[]>(`/api/admin/territories/barangays?municipalityId=${encodeURIComponent(municipalityId)}`),
  governmentIdTypes: () => request<string[]>('/api/admin/government-id-types'),
  operatorOverview: () => request<OperatorOverview>('/api/operator/overview'),
  operatorBookings: (from?: string, to?: string) => {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    const query = params.toString()
    return request<OperatorBookingBoard>(`/api/operator/bookings${query ? `?${query}` : ''}`)
  },
  operatorBooking: (id: string) => request<RideDetail>(`/api/operator/bookings/${id}`),
  operatorBookingList: (q = '', page = 1, pageSize = 10, status?: TripStatus | '', from?: string, to?: string) => {
    const params = new URLSearchParams({
      q,
      page: String(page),
      pageSize: String(pageSize),
    })
    if (status) {
      params.set('status', status)
    }
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    return request<Paged<OperatorBookingListItem>>(`/api/operator/bookings/list?${params}`)
  },
  reassignOperatorBooking: (id: string, riderId: string) =>
    request<RideDetail>(`/api/operator/bookings/${id}/reassign`, {
      method: 'POST',
      body: JSON.stringify({ riderId }),
    }),
  scheduledBookings: (q = '', page = 1, pageSize = 10, status?: TripStatus | '') => {
    const params = new URLSearchParams({
      q,
      page: String(page),
      pageSize: String(pageSize),
    })
    if (status) {
      params.set('status', status)
    }
    return request<Paged<ScheduledBooking>>(`/api/operator/schedule?${params}`)
  },
  scheduledBooking: (id: string) => request<RideDetail>(`/api/operator/schedule/${id}`),
  createScheduledBooking: (body: {
    customerName: string
    phone: string
    riderId: string
    pickupBarangayId: string
    pickupDetails: string
    dropoffBarangayId: string
    dropoffDetails: string
    scheduledAtUtc: string
    notes?: string
    distanceKm: number
    paymentMethod: PaymentMethod
    paymentMethodOther?: string
  }) => request<RideDetail>('/api/operator/schedule', { method: 'POST', body: JSON.stringify(body) }),
  cancelScheduledBooking: (id: string) =>
    request<RideDetail>(`/api/operator/schedule/${id}/cancel`, { method: 'POST' }),
  mapsConfig: () => request<{ googleMapsBrowserKey: string }>('/api/public/maps'),
  operatorFleet: () => request<OperatorFleet>('/api/operator/fleet'),
  operatorCompany: () => request<OperatorDetail>('/api/operator/company'),
  changeOperatorPassword: (currentPassword: string, newPassword: string) =>
    request<{ message: string }>('/api/operator/password', {
      method: 'POST',
      body: JSON.stringify({ currentPassword, newPassword }),
    }),
  opCustomers: (q = '') =>
    request<CustomerListItem[]>(`/api/operator/customers?q=${encodeURIComponent(q)}`),
  opCustomer: (id: string) => request<CustomerDetail>(`/api/operator/customers/${id}`),
  opCustomerRides: (id: string, opts: RideQuery = {}) =>
    request<RiderRides>(`/api/operator/customers/${id}/rides?${rideQuery(opts)}`),
  opCustomerRide: (id: string, rideId: string) =>
    request<RideDetail>(`/api/operator/customers/${id}/rides/${rideId}`),
  opRiders: (q = '', page = 1, pageSize = 10) =>
    request<Paged<RiderListItem>>(`/api/operator/riders?q=${encodeURIComponent(q)}&page=${page}&pageSize=${pageSize}`),
  opRider: (id: string) => request<RiderDetail>(`/api/operator/riders/${id}`),
  createOperatorRider: (body: FormData) =>
    request<RiderDetail>('/api/operator/riders', { method: 'POST', body }),
  updateOperatorRider: (id: string, body: FormData) =>
    request<RiderDetail>(`/api/operator/riders/${id}`, { method: 'PUT', body }),
  setOperatorRiderActive: (id: string, isActive: boolean) =>
    request<RiderDetail>(`/api/operator/riders/${id}/active`, {
      method: 'POST',
      body: JSON.stringify({ isActive }),
    }),
  resetOperatorRiderPassword: (id: string, password: string) =>
    request<ResetPasswordResult>(`/api/operator/riders/${id}/reset-password`, {
      method: 'POST',
      body: JSON.stringify({ password }),
    }),
  opRiderRides: (id: string, opts: RideQuery = {}) =>
    request<RiderRides>(`/api/operator/riders/${id}/rides?${rideQuery(opts)}`),
  opRiderRide: (id: string, rideId: string) =>
    request<RideDetail>(`/api/operator/riders/${id}/rides/${rideId}`),
  operatorWalletOverview: () => request<OperatorWalletOverview>('/api/operator/wallet'),
  operatorWalletHistory: (opts: {
    q?: string
    kind?: WalletTransactionKind | ''
    riderId?: string
    page?: number
    pageSize?: number
  } = {}) => {
    const params = new URLSearchParams()
    if (opts.q?.trim()) params.set('q', opts.q.trim())
    if (opts.kind) params.set('kind', opts.kind)
    if (opts.riderId) params.set('riderId', opts.riderId)
    params.set('page', String(opts.page ?? 1))
    params.set('pageSize', String(opts.pageSize ?? 20))
    return request<Paged<WalletHistoryItem>>(`/api/operator/wallet/history?${params}`)
  },
  operatorWalletRequests: () => request<WalletRequest[]>('/api/operator/wallet/requests'),
  operatorRiderWallet: (riderId: string) => request<RiderWalletDetail>(`/api/operator/wallet/riders/${riderId}`),
  approveWalletRequest: (id: string) =>
    request<{ transaction: WalletTransaction; balance: number }>(`/api/operator/wallet/requests/${id}/approve`, { method: 'POST' }),
  rejectWalletRequest: (id: string, reason?: string) =>
    request<{ transaction: WalletTransaction; balance: number }>(`/api/operator/wallet/requests/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  operatorRiderCashIn: (riderId: string, body: { amount: number; paymentMethod: PaymentMethod; note?: string; approved?: boolean }) =>
    request<WalletTransaction>(`/api/operator/wallet/riders/${riderId}/cash-in`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  operatorRiderCashOut: (riderId: string, body: { amount: number; paymentMethod: PaymentMethod; note?: string; approved?: boolean }) =>
    request<WalletTransaction>(`/api/operator/wallet/riders/${riderId}/cash-out`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  completeOperatorBooking: (id: string) =>
    request<RideDetail>(`/api/operator/bookings/${id}/complete`, { method: 'POST' }),
  opFares: () => request<OperatorFareMatrix>('/api/operator/fares'),
  saveOperatorFares: (body: {
    vehicleType: VehicleType
    baseFare: number
    perKm: number
    minimumFare: number
    includedKm: number
    operatorCommissionPercent: number
    driverCommissionPercent: number
    isActive: boolean
  }) => request<OperatorFareMatrix>('/api/operator/fares', { method: 'PUT', body: JSON.stringify(body) }),
  saveOperatorFareMatrix: (body: {
    motorcycle: {
      baseFare: number
      perKm: number
      minimumFare: number
      includedKm: number
      operatorCommissionPercent: number
      driverCommissionPercent: number
      isActive: boolean
    }
    tricycle: {
      baseFare: number
      perKm: number
      minimumFare: number
      includedKm: number
      operatorCommissionPercent: number
      driverCommissionPercent: number
      isActive: boolean
    }
  }) => request<OperatorFareMatrix>('/api/operator/fares/matrix', { method: 'PUT', body: JSON.stringify(body) }),
  addOperatorSurcharges: (body: {
    vehicleTypes: VehicleType[]
    kind: SurchargeKind
    name: string
    amount: number
    windowStart?: string | null
    windowEnd?: string | null
    rangeStartUtc?: string | null
    rangeEndUtc?: string | null
    isActive: boolean
  }) => request<OperatorFareMatrix>('/api/operator/fares/surcharges', { method: 'POST', body: JSON.stringify(body) }),
  addOperatorSurcharge: (vehicleType: VehicleType, body: {
    kind: SurchargeKind
    name: string
    amount: number
    windowStart?: string | null
    windowEnd?: string | null
    rangeStartUtc?: string | null
    rangeEndUtc?: string | null
    isActive: boolean
  }) => request<OperatorFareMatrix>(`/api/operator/fares/${vehicleType}/surcharges`, {
    method: 'POST',
    body: JSON.stringify(body),
  }),
  updateOperatorSurcharge: (id: string, body: {
    kind: SurchargeKind
    name: string
    amount: number
    windowStart?: string | null
    windowEnd?: string | null
    rangeStartUtc?: string | null
    rangeEndUtc?: string | null
    isActive: boolean
  }) => request<OperatorFareMatrix>(`/api/operator/fares/surcharges/${id}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  }),
  deleteOperatorSurcharge: (id: string) =>
    request<OperatorFareMatrix>(`/api/operator/fares/surcharges/${id}/delete`, { method: 'POST' }),
  operatorSupport: (q = '', kind?: SupportKind | '', status?: SupportStatus | '', page = 1, pageSize = 10) => {
    const params = new URLSearchParams({ q, page: String(page), pageSize: String(pageSize) })
    if (kind) params.set('kind', kind)
    if (status) params.set('status', status)
    return request<SupportInbox>(`/api/operator/support?${params}`)
  },
  operatorSupportTicket: (id: string) => request<SupportTicketDetail>(`/api/operator/support/${id}`),
  addOperatorSupportNote: (id: string, notes: string) =>
    request<SupportTicket>(`/api/operator/support/${id}/notes`, {
      method: 'POST',
      body: JSON.stringify({ notes }),
    }),
  closeOperatorSupport: (id: string, closed: boolean) =>
    request<SupportTicket>(`/api/operator/support/${id}/close`, {
      method: 'POST',
      body: JSON.stringify({ closed }),
    }),
  operatorInbox: () => request<OperatorInboxItem[]>('/api/operator/inbox'),
  readOperatorInbox: (id: string) =>
    request<OperatorInboxItem>(`/api/operator/inbox/${id}/read`, { method: 'POST' }),
  readOperatorBillingInbox: () =>
    request<{ message: string }>('/api/operator/inbox/read-billing', { method: 'POST' }),
  operatorAlerts: () => request<OperatorNavAlerts>('/api/operator/alerts'),
  operatorBilling: () => request<BillingOperatorDetail>('/api/operator/billing'),
  operatorProvinces: () => request<IdName[]>('/api/operator/territories/provinces'),
  operatorMunicipalities: (provinceId: string) =>
    request<IdName[]>(`/api/operator/territories/municipalities?provinceId=${encodeURIComponent(provinceId)}`),
  operatorBarangays: (municipalityId: string) =>
    request<BarangayOption[]>(`/api/operator/territories/barangays?municipalityId=${encodeURIComponent(municipalityId)}`),
}
