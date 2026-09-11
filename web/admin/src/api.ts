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
  | 'derive-fares'
  | 'surcharges'
  | 'billing'
  | 'commission'
  | 'booking-report'
  | 'rider-report'
  | 'customer-report'
  | 'announcements'
  | 'support'
  | 'audit'
  | 'settings'
  | 'roles'
  | 'admins'
  | 'employees'
  | 'profile'
  | 'riders'
  | 'fleet'
  | 'dashboard'
  | 'bookings'
  | 'schedule'
  | 'inbox'
  | 'company'
  | 'wallet'
  | 'promos'
  | 'merchants'
  | 'product-categories'

export type Me = {
  id: string
  phoneNumber: string
  fullName: string
  role: UserRole
  operatorId: string | null
  isActive: boolean
  isMainAdmin: boolean
  isMainOperator?: boolean
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
  bookingDispatchMode?: BookingDispatchMode
  broadcastRadiusKm?: number
  liveBookingExpiryMinutes?: number
  scheduledBookingGraceMinutes?: number
  riderCount: number
  ridersMotorcycle: number
  ridersTricycle: number
  createdAtUtc: string
}

export type BookingDispatchMode = 'Broadcast' | 'Selection' | 'Both'

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
  vehicleFranchiseNumber: string
  vehicleModel: string | null
  isActive: boolean
  licenseType: string
  licenseNumber: string
  profilePhotoUrl: string | null
  licensePhotoUrl: string | null
  acceptedPaymentMethods: PaymentMethod[]
  credibilityScore: number
  riderCancelCount: number
}

export type RiderDetail = RiderListItem & {
  fullAddress: string
  address: OperatorAddress
}

export type RiderInviteLink = {
  token: string
  joinPath: string
  statusPath: string
  createdAtUtc: string
  kind: 'Rotating' | 'Permanent' | string
}

export type RiderInviteLinks = {
  rotating: RiderInviteLink
  permanent: RiderInviteLink
}

export type RiderInvitePublicInfo = {
  token: string
  companyName: string
  statusPath: string
}

export type RiderApplicationListItem = {
  id: string
  fullName: string
  phoneNumber: string
  vehicleType: string
  plateNumber: string
  status: string
  createdAtUtc: string
}

export type RiderApplicationDetail = {
  id: string
  fullName: string
  phoneNumber: string
  vehicleType: string
  plateNumber: string
  vehicleFranchiseNumber: string
  vehicleModel: string | null
  licenseType: string
  licenseNumber: string
  status: string
  fullAddress: string
  address: OperatorAddress
  acceptedPaymentMethods: string[]
  profilePhotoUrl: string | null
  licensePhotoUrl: string | null
  reviewNote: string | null
  createdAtUtc: string
  reviewedAtUtc: string | null
  riderProfileId: string | null
}

export type RiderApplicationStatusResult = {
  status: string
  label: string
  companyName: string | null
  message: string | null
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
  adminAmount: number | null
  operatorAmount: number | null
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

export type OperatorCashInBank = {
  id: string
  bankName: string
  accountName: string
  accountNumber: string
  qrUrl: string | null
  sortOrder: number
  isActive: boolean
}

export type OperatorCashInDestinations = {
  gCashNumber: string
  gCashQrUrl: string | null
  gCashIsActive: boolean
  mayaNumber: string
  mayaQrUrl: string | null
  mayaIsActive: boolean
  banks: OperatorCashInBank[]
}

export type OperatorPromoItem = {
  id: string
  code: string
  displayCode: string
  discountPercent: number
  isActive: boolean
  startsAtUtc: string | null
  endsAtUtc: string | null
  maxRedemptions: number | null
  redemptionCount: number
  createdAtUtc: string
}

export type SaveOperatorPromoBody = {
  discountPercent: number
  isActive: boolean
  startsAtUtc: string | null
  endsAtUtc: string | null
  maxRedemptions: number | null
}

export type MerchantOperatingHourItem = {
  dayOfWeek: number
  isClosed: boolean
  openTime: string | null
  closeTime: string | null
}

export type MerchantListItem = {
  id: string
  businessName: string
  contactPerson: string
  pinnedAddress: string
  managedByMerchant: boolean
  mobile: string
  email: string
  logoUrl: string | null
  backgroundUrl: string | null
  isActive: boolean
  sortOrder: number
  createdAtUtc: string
}

export type MerchantDetailItem = MerchantListItem & {
  latitude: number
  longitude: number
  operatingHours: MerchantOperatingHourItem[]
}

export type SaveMerchantBody = {
  businessName: string
  contactPerson: string
  latitude: number
  longitude: number
  pinnedAddress: string
  managedByMerchant: boolean
  mobile?: string | null
  email?: string | null
  password?: string | null
  isActive: boolean
  sortOrder: number
  operatingHours: MerchantOperatingHourItem[]
}

export type MerchantProductCategoryItem = {
  id: string
  name: string
  sortOrder: number
  isActive: boolean
}

export type ProductAddonOptionItem = {
  id?: string | null
  name: string
  basePrice: number
  sellingPrice: number
  sortOrder: number
  isActive: boolean
}

export type ProductAddonGroupItem = {
  id?: string | null
  name: string
  minSelect: number
  maxSelect: number
  sortOrder: number
  isActive: boolean
  options: ProductAddonOptionItem[]
}

export type MerchantProductItem = {
  id: string
  merchantId: string
  categoryId: string | null
  categoryName: string | null
  name: string
  description: string
  basePrice: number
  sellingPrice: number
  availableOnStorefront: boolean
  availableAllDay: boolean
  availableFromTime: string | null
  availableToTime: string | null
  sortOrder: number
  imageUrl: string | null
  adoptedAddonGroupIds: string[]
  addonGroups: ProductAddonGroupItem[]
}

export type SaveMerchantProductBody = {
  categoryId?: string | null
  name: string
  description: string
  basePrice: number
  sellingPrice: number
  availableOnStorefront: boolean
  availableAllDay: boolean
  availableFromTime?: string | null
  availableToTime?: string | null
  sortOrder: number
}

export type RideStop = {
  details: string
  barangay: string
  municipality: string
  province: string
  fullAddress: string
}

export type RideCommissionBreakdown = {
  systemPercent: number
  systemAmount: number
  operatorPercent: number
  operatorAmount: number
  driverPercent: number
  driverAmount: number
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
  passengerCount?: number
  paymentMethod: PaymentMethod
  paymentMethodOther: string | null
  commission: RideCommissionBreakdown | null
  customerFare?: number
  promoDiscountAmount?: number
  isPromoSponsored?: boolean
  discountPercent?: number | null
  promoCode?: string | null
  customerBoostAmount?: number
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
  passengerCount?: number
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
  commission: RideCommissionBreakdown | null
  customerFare?: number
  promoDiscountAmount?: number
  isPromoSponsored?: boolean
  discountPercent?: number | null
  promoCode?: string | null
  customerBoostAmount?: number
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
  systemAmount: number
  operatorAmount: number
  driverAmount: number
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

export type FarePassengerTier = {
  passengerCount: number
  baseFare: number
  perKm: number
  minimumFare: number
  includedKm: number
}

export type FareRates = {
  vehicleType: VehicleType
  municipalityId: string
  municipalityName: string
  baseFare: number
  perKm: number
  minimumFare: number
  includedKm: number
  operatorCommissionPercent: number
  driverCommissionPercent: number
  isActive: boolean
  passengerTiers: FarePassengerTier[]
  surcharges: FareSurcharge[]
  samples: FareSample[]
}

export type DeriveFareLatLng = { lat: number; lng: number }

export type DeriveFareZoneListItem = {
  id: string
  name: string
  maxDropoffKm: number
  isActive: boolean
  priority: number
  pointCount: number
  createdAtUtc: string
}

export type DeriveFareRates = {
  vehicleType: VehicleType
  baseFare: number
  perKm: number
  minimumFare: number
  includedKm: number
  operatorCommissionPercent: number
  driverCommissionPercent: number
  isActive: boolean
  passengerTiers: FarePassengerTier[]
  samples: FareSample[]
}

export type DeriveFareZoneDetail = {
  id: string
  name: string
  maxDropoffKm: number
  isActive: boolean
  priority: number
  polygon: DeriveFareLatLng[]
  motorcycleCommissionPercent: number
  tricycleCommissionPercent: number
  motorcycle: DeriveFareRates | null
  tricycle: DeriveFareRates | null
}

export type SaveDeriveFareZoneBody = {
  name: string
  maxDropoffKm: number
  isActive: boolean
  priority: number
  polygon: DeriveFareLatLng[]
  motorcycle: {
    baseFare: number
    perKm: number
    minimumFare: number
    includedKm: number
    operatorCommissionPercent: number
    driverCommissionPercent: number
    isActive: boolean
    passengerTiers: FarePassengerTier[]
  }
  tricycle: {
    baseFare: number
    perKm: number
    minimumFare: number
    includedKm: number
    operatorCommissionPercent: number
    driverCommissionPercent: number
    isActive: boolean
    passengerTiers: FarePassengerTier[]
  }
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
  municipalityId: string | null
  municipalityName: string | null
  municipalities: IdName[]
  motorcycle: FareRates | null
  tricycle: FareRates | null
}

export type OperatorFareListItem = {
  operatorId: string
  operatorName: string
  operatorActive: boolean
  municipalityId: string
  municipalityName: string
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

export type CommissionReportItem = {
  id: string
  reference: string
  riderName: string
  riderId: string
  operatorName: string
  operatorId: string
  riderCommission: number
  systemCommission: number
  adminCommission: number
  bookingAmount: number
  dateUtc: string
  status: TripStatus
}

export type CommissionReportResponse = {
  page: Paged<CommissionReportItem>
  summary: {
    bookingAmount: number
    riderCommission: number
    systemCommission: number
    adminCommission: number
    count: number
  }
}

export type RiderReportItem = {
  riderId: string
  riderName: string
  plateNumber: string
  vehicleFranchiseNumber: string
  mobile: string
  joinedAtUtc: string
  isActive: boolean
  totalRides: number
  totalCancel: number
  riderIncome: number
  bookingAmount: number
}

export type RiderReportResponse = {
  page: Paged<RiderReportItem>
  summary: {
    riderCount: number
    totalRides: number
    totalCancel: number
    riderIncome: number
    bookingAmount: number
  }
}

export type CustomerReportItem = {
  customerId: string
  customerName: string
  mobile: string
  joinedAtUtc: string
  isActive: boolean
  totalRides: number
  totalCancel: number
  bookingAmount: number
  totalSpent: number
}

export type CustomerReportResponse = {
  page: Paged<CustomerReportItem>
  summary: {
    customerCount: number
    totalRides: number
    totalCancel: number
    bookingAmount: number
    totalSpent: number
  }
}

export type BookingReportItem = {
  id: string
  dateUtc: string
  reference: string
  riderName: string
  riderId: string
  customerName: string
  customerId: string | null
  riderCommission: number
  systemCommission: number
  operatorCommission: number
  promo: number
  fare: number
  status: TripStatus
}

export type BookingReportResponse = {
  page: Paged<BookingReportItem>
  summary: {
    count: number
    riderCommission: number
    systemCommission: number
    operatorCommission: number
    promo: number
    fare: number
  }
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
  customerFare?: number
  promoDiscountAmount?: number
  isPromoSponsored?: boolean
  discountPercent?: number | null
  promoCode?: string | null
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
  isMainOperator?: boolean
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
      const body = (await res.json()) as {
        message?: string
        title?: string
        detail?: string
        errors?: Record<string, string[] | string>
      }
      if (body.message) {
        message = body.message
      } else if (body.detail) {
        message = body.detail
      } else if (body.title) {
        message = body.title
      } else if (body.errors) {
        const first = Object.values(body.errors).flat()[0]
        if (first) message = first
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
    return request<Paged<OperatorFareListItem>>(`/api/admin/fares?${params}`)
  },
  operatorFares: (operatorId: string, municipalityId?: string) => {
    const params = new URLSearchParams()
    if (municipalityId) params.set('municipalityId', municipalityId)
    const qs = params.toString()
    return request<OperatorFareMatrix>(`/api/admin/operators/${operatorId}/fares${qs ? `?${qs}` : ''}`)
  },
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
  operatorAccessPages: () => request<AccessPage[]>('/api/operator/access/pages'),
  operatorAccessGroups: () => request<AccessGroup[]>('/api/operator/access/groups'),
  createOperatorAccessGroup: (body: { name: string; description: string; pages: PageId[] }) =>
    request<AccessGroup>('/api/operator/access/groups', { method: 'POST', body: JSON.stringify(body) }),
  updateOperatorAccessGroup: (id: string, body: { name: string; description: string; pages: PageId[] }) =>
    request<AccessGroup>(`/api/operator/access/groups/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteOperatorAccessGroup: (id: string) =>
    request<{ ok: boolean }>(`/api/operator/access/groups/${id}/delete`, { method: 'POST' }),
  operatorAccessUsers: () => request<AccessStaff[]>('/api/operator/access/users'),
  createOperatorAccessUser: (body: { fullName: string; phone: string; accessGroupId: string; password: string }) =>
    request<AccessStaff>('/api/operator/access/users', { method: 'POST', body: JSON.stringify(body) }),
  updateOperatorAccessUser: (id: string, body: { fullName: string; phone: string; accessGroupId: string; password?: string }) =>
    request<AccessStaff>(`/api/operator/access/users/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  setOperatorAccessUserActive: (id: string, isActive: boolean) =>
    request<AccessStaff>(`/api/operator/access/users/${id}/active`, {
      method: 'POST',
      body: JSON.stringify({ isActive }),
    }),
  resetOperatorAccessUserPassword: (id: string, password: string) =>
    request<ResetPasswordResult>(`/api/operator/access/users/${id}/reset-password`, {
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
  publicProvinces: () => request<IdName[]>('/api/territories/provinces'),
  publicMunicipalities: (provinceId: string) =>
    request<IdName[]>(`/api/territories/municipalities?provinceId=${encodeURIComponent(provinceId)}`),
  publicBarangays: (municipalityId: string) =>
    request<BarangayOption[]>(`/api/territories/barangays?municipalityId=${encodeURIComponent(municipalityId)}`),
  publicRiderInvite: (token: string) =>
    request<RiderInvitePublicInfo>(`/api/public/rider-invite/${encodeURIComponent(token)}`),
  publicRiderApply: (token: string, body: FormData) =>
    request<{ message: string; status: string; statusPath: string }>(
      `/api/public/rider-invite/${encodeURIComponent(token)}/apply`,
      { method: 'POST', body },
    ),
  publicRiderApplicationStatus: (phone: string) =>
    request<RiderApplicationStatusResult>('/api/public/rider-application/status', {
      method: 'POST',
      body: JSON.stringify({ phone }),
    }),
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
    passengerCount?: number
    paymentMethod: PaymentMethod
    paymentMethodOther?: string
  }) => request<RideDetail>('/api/operator/schedule', { method: 'POST', body: JSON.stringify(body) }),
  cancelScheduledBooking: (id: string) =>
    request<RideDetail>(`/api/operator/schedule/${id}/cancel`, { method: 'POST' }),
  mapsConfig: () => request<{ googleMapsBrowserKey: string }>('/api/public/maps'),
  publicBranding: () =>
    request<{
      brandName: string
      shortName: string
      logoUrl: string | null
      faviconUrl: string | null
      themeId: string
      accent: string
      good: string
    }>('/api/public/branding'),
  getBranding: () =>
    request<{
      brandName: string
      shortName: string
      logoUrl: string | null
      faviconUrl: string | null
      themeId: string
      accent: string
      good: string
      themes: { id: string; label: string; accent: string; good: string }[]
    }>('/api/admin/branding'),
  updateBranding: (body: {
    brandName: string
    shortName?: string
    themeId: string
    clearLogo?: boolean
    clearFavicon?: boolean
  }) =>
    request<{
      brandName: string
      shortName: string
      logoUrl: string | null
      faviconUrl: string | null
      themeId: string
      accent: string
      good: string
      themes: { id: string; label: string; accent: string; good: string }[]
    }>('/api/admin/branding', { method: 'PUT', body: JSON.stringify(body) }),
  uploadBrandLogo: (file: File) => {
    const data = new FormData()
    data.append('file', file)
    return request<{
      brandName: string
      shortName: string
      logoUrl: string | null
      faviconUrl: string | null
      themeId: string
      accent: string
      good: string
      themes: { id: string; label: string; accent: string; good: string }[]
    }>('/api/admin/branding/logo', { method: 'POST', body: data })
  },
  uploadBrandFavicon: (file: File) => {
    const data = new FormData()
    data.append('file', file)
    return request<{
      brandName: string
      shortName: string
      logoUrl: string | null
      faviconUrl: string | null
      themeId: string
      accent: string
      good: string
      themes: { id: string; label: string; accent: string; good: string }[]
    }>('/api/admin/branding/favicon', { method: 'POST', body: data })
  },
  operatorFleet: () => request<OperatorFleet>('/api/operator/fleet'),
  operatorCompany: () => request<OperatorDetail>('/api/operator/company'),
  saveOperatorDispatchMode: (
    bookingDispatchMode: BookingDispatchMode,
    options?: {
      broadcastRadiusKm?: number
      liveBookingExpiryMinutes?: number
      scheduledBookingGraceMinutes?: number
    },
  ) =>
    request<OperatorDetail>('/api/operator/dispatch', {
      method: 'PUT',
      body: JSON.stringify({
        bookingDispatchMode,
        ...(options?.broadcastRadiusKm != null ? { broadcastRadiusKm: options.broadcastRadiusKm } : {}),
        ...(options?.liveBookingExpiryMinutes != null
          ? { liveBookingExpiryMinutes: options.liveBookingExpiryMinutes }
          : {}),
        ...(options?.scheduledBookingGraceMinutes != null
          ? { scheduledBookingGraceMinutes: options.scheduledBookingGraceMinutes }
          : {}),
      }),
    }),
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
  operatorRiderInvite: () => request<RiderInviteLinks>('/api/operator/rider-invite'),
  regenerateOperatorRiderInvite: () =>
    request<RiderInviteLinks>('/api/operator/rider-invite/regenerate', { method: 'POST' }),
  operatorRiderApplications: (opts: { status?: string; q?: string; page?: number; pageSize?: number } = {}) => {
    const params = new URLSearchParams({
      page: String(opts.page ?? 1),
      pageSize: String(opts.pageSize ?? 10),
    })
    if (opts.status) params.set('status', opts.status)
    if (opts.q?.trim()) params.set('q', opts.q.trim())
    return request<Paged<RiderApplicationListItem>>(`/api/operator/rider-applications?${params}`)
  },
  operatorRiderApplication: (id: string) =>
    request<RiderApplicationDetail>(`/api/operator/rider-applications/${id}`),
  approveOperatorRiderApplication: (id: string) =>
    request<RiderApplicationDetail>(`/api/operator/rider-applications/${id}/approve`, { method: 'POST' }),
  rejectOperatorRiderApplication: (id: string, note?: string) =>
    request<RiderApplicationDetail>(`/api/operator/rider-applications/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ note }),
    }),
  operatorWalletOverview: () => request<OperatorWalletOverview>('/api/operator/wallet'),
  operatorCommissionReport: (opts: {
    bookingNo?: string
    rider?: string
    from?: string
    to?: string
    page?: number
    pageSize?: number
  }) => {
    const params = new URLSearchParams({
      page: String(opts.page ?? 1),
      pageSize: String(opts.pageSize ?? 10),
    })
    if (opts.bookingNo?.trim()) params.set('bookingNo', opts.bookingNo.trim())
    if (opts.rider?.trim()) params.set('rider', opts.rider.trim())
    if (opts.from) params.set('from', opts.from)
    if (opts.to) params.set('to', opts.to)
    return request<CommissionReportResponse>(`/api/operator/reports/commission?${params}`)
  },
  operatorRiderReport: (opts: {
    q?: string
    from?: string
    to?: string
    page?: number
    pageSize?: number
  }) => {
    const params = new URLSearchParams({
      page: String(opts.page ?? 1),
      pageSize: String(opts.pageSize ?? 10),
    })
    if (opts.q?.trim()) params.set('q', opts.q.trim())
    if (opts.from) params.set('from', opts.from)
    if (opts.to) params.set('to', opts.to)
    return request<RiderReportResponse>(`/api/operator/reports/riders?${params}`)
  },
  exportOperatorRiderReport: async (opts: { q?: string; from?: string; to?: string }) => {
    const params = new URLSearchParams()
    if (opts.q?.trim()) params.set('q', opts.q.trim())
    if (opts.from) params.set('from', opts.from)
    if (opts.to) params.set('to', opts.to)
    const headers = new Headers()
    const token = getToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    const res = await fetch(`/api/operator/reports/riders/export?${params}`, { headers })
    if (!res.ok) {
      let message = 'Could not export rider report.'
      try {
        const body = (await res.json()) as { message?: string }
        if (body.message) message = body.message
      } catch {
        /* ignore */
      }
      throw new Error(message)
    }
    const blob = await res.blob()
    const disposition = res.headers.get('Content-Disposition') ?? ''
    const match = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(disposition)
    const filename = match?.[1]?.replace(/['"]/g, '') || `rider-report.xlsx`
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },
  operatorCustomerReport: (opts: {
    q?: string
    from?: string
    to?: string
    page?: number
    pageSize?: number
  }) => {
    const params = new URLSearchParams({
      page: String(opts.page ?? 1),
      pageSize: String(opts.pageSize ?? 10),
    })
    if (opts.q?.trim()) params.set('q', opts.q.trim())
    if (opts.from) params.set('from', opts.from)
    if (opts.to) params.set('to', opts.to)
    return request<CustomerReportResponse>(`/api/operator/reports/customers?${params}`)
  },
  exportOperatorCustomerReport: async (opts: { q?: string; from?: string; to?: string }) => {
    const params = new URLSearchParams()
    if (opts.q?.trim()) params.set('q', opts.q.trim())
    if (opts.from) params.set('from', opts.from)
    if (opts.to) params.set('to', opts.to)
    const headers = new Headers()
    const token = getToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    const res = await fetch(`/api/operator/reports/customers/export?${params}`, { headers })
    if (!res.ok) {
      let message = 'Could not export customer report.'
      try {
        const body = (await res.json()) as { message?: string }
        if (body.message) message = body.message
      } catch {
        /* ignore */
      }
      throw new Error(message)
    }
    const blob = await res.blob()
    const disposition = res.headers.get('Content-Disposition') ?? ''
    const match = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(disposition)
    const filename = match?.[1]?.replace(/['"]/g, '') || `customer-report.xlsx`
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },
  operatorBookingReport: (opts: {
    bookingNo?: string
    rider?: string
    customer?: string
    from?: string
    to?: string
    page?: number
    pageSize?: number
  }) => {
    const params = new URLSearchParams({
      page: String(opts.page ?? 1),
      pageSize: String(opts.pageSize ?? 10),
    })
    if (opts.bookingNo?.trim()) params.set('bookingNo', opts.bookingNo.trim())
    if (opts.rider?.trim()) params.set('rider', opts.rider.trim())
    if (opts.customer?.trim()) params.set('customer', opts.customer.trim())
    if (opts.from) params.set('from', opts.from)
    if (opts.to) params.set('to', opts.to)
    return request<BookingReportResponse>(`/api/operator/reports/bookings?${params}`)
  },
  exportOperatorBookingReport: async (opts: {
    bookingNo?: string
    rider?: string
    customer?: string
    from?: string
    to?: string
  }) => {
    const params = new URLSearchParams()
    if (opts.bookingNo?.trim()) params.set('bookingNo', opts.bookingNo.trim())
    if (opts.rider?.trim()) params.set('rider', opts.rider.trim())
    if (opts.customer?.trim()) params.set('customer', opts.customer.trim())
    if (opts.from) params.set('from', opts.from)
    if (opts.to) params.set('to', opts.to)
    const headers = new Headers()
    const token = getToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    const res = await fetch(`/api/operator/reports/bookings/export?${params}`, { headers })
    if (!res.ok) {
      let message = 'Could not export booking report.'
      try {
        const body = (await res.json()) as { message?: string }
        if (body.message) message = body.message
      } catch {
        /* ignore */
      }
      throw new Error(message)
    }
    const blob = await res.blob()
    const disposition = res.headers.get('Content-Disposition') ?? ''
    const match = /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/.exec(disposition)
    const filename = match?.[1]?.replace(/['"]/g, '') || `booking-report.xlsx`
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },
  adminCommissionReport: (opts: {
    operatorId?: string
    bookingNo?: string
    rider?: string
    from?: string
    to?: string
    page?: number
    pageSize?: number
  }) => {
    const params = new URLSearchParams({
      page: String(opts.page ?? 1),
      pageSize: String(opts.pageSize ?? 10),
    })
    if (opts.operatorId) params.set('operatorId', opts.operatorId)
    if (opts.bookingNo?.trim()) params.set('bookingNo', opts.bookingNo.trim())
    if (opts.rider?.trim()) params.set('rider', opts.rider.trim())
    if (opts.from) params.set('from', opts.from)
    if (opts.to) params.set('to', opts.to)
    return request<CommissionReportResponse>(`/api/admin/reports/commission?${params}`)
  },
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
  operatorCashInDestinations: () =>
    request<OperatorCashInDestinations>('/api/operator/wallet/cash-in-destinations'),
  saveOperatorCashInEwallets: (body: FormData) =>
    request<OperatorCashInDestinations>('/api/operator/wallet/cash-in-destinations/ewallets', {
      method: 'PUT',
      body,
    }),
  addOperatorCashInBank: (body: FormData) =>
    request<OperatorCashInDestinations>('/api/operator/wallet/cash-in-destinations/banks', {
      method: 'POST',
      body,
    }),
  updateOperatorCashInBank: (id: string, body: FormData) =>
    request<OperatorCashInDestinations>(`/api/operator/wallet/cash-in-destinations/banks/${id}`, {
      method: 'PUT',
      body,
    }),
  deleteOperatorCashInBank: (id: string) =>
    request<OperatorCashInDestinations>(`/api/operator/wallet/cash-in-destinations/banks/${id}`, {
      method: 'DELETE',
    }),
  operatorPromos: () => request<{ items: OperatorPromoItem[] }>('/api/operator/promos'),
  createOperatorPromo: (body: SaveOperatorPromoBody) =>
    request<OperatorPromoItem>('/api/operator/promos', {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  updateOperatorPromo: (id: string, body: SaveOperatorPromoBody) =>
    request<OperatorPromoItem>(`/api/operator/promos/${id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  toggleOperatorPromo: (id: string) =>
    request<OperatorPromoItem>(`/api/operator/promos/${id}/toggle`, { method: 'POST' }),
  operatorMerchants: () => request<{ items: MerchantListItem[] }>('/api/operator/merchants'),
  operatorMerchant: (id: string) => request<MerchantDetailItem>(`/api/operator/merchants/${id}`),
  createOperatorMerchant: (body: SaveMerchantBody) =>
    request<MerchantDetailItem>('/api/operator/merchants', {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  updateOperatorMerchant: (id: string, body: SaveMerchantBody) =>
    request<MerchantDetailItem>(`/api/operator/merchants/${id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  toggleOperatorMerchant: (id: string) =>
    request<MerchantDetailItem>(`/api/operator/merchants/${id}/toggle`, { method: 'POST' }),
  uploadOperatorMerchantLogo: (id: string, file: File) => {
    const data = new FormData()
    data.append('file', file)
    return request<MerchantDetailItem>(`/api/operator/merchants/${id}/logo`, { method: 'POST', body: data })
  },
  uploadOperatorMerchantBackground: (id: string, file: File) => {
    const data = new FormData()
    data.append('file', file)
    return request<MerchantDetailItem>(`/api/operator/merchants/${id}/background`, { method: 'POST', body: data })
  },
  operatorMerchantCategories: (merchantId: string) =>
    request<{ items: MerchantProductCategoryItem[] }>(`/api/operator/merchants/${merchantId}/categories`),
  createOperatorMerchantCategory: (merchantId: string, body: { name: string; sortOrder: number; isActive: boolean }) =>
    request<MerchantProductCategoryItem>(`/api/operator/merchants/${merchantId}/categories`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  updateOperatorMerchantCategory: (
    merchantId: string,
    id: string,
    body: { name: string; sortOrder: number; isActive: boolean },
  ) =>
    request<MerchantProductCategoryItem>(`/api/operator/merchants/${merchantId}/categories/${id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  deleteOperatorMerchantCategory: (merchantId: string, id: string) =>
    request<void>(`/api/operator/merchants/${merchantId}/categories/${id}`, { method: 'DELETE' }),
  operatorMerchantProducts: (merchantId: string) =>
    request<{ items: MerchantProductItem[] }>(`/api/operator/merchants/${merchantId}/products`),
  createOperatorMerchantProduct: (merchantId: string, body: SaveMerchantProductBody) =>
    request<MerchantProductItem>(`/api/operator/merchants/${merchantId}/products`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  updateOperatorMerchantProduct: (merchantId: string, id: string, body: SaveMerchantProductBody) =>
    request<MerchantProductItem>(`/api/operator/merchants/${merchantId}/products/${id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  toggleOperatorMerchantProductStorefront: (merchantId: string, id: string) =>
    request<MerchantProductItem>(`/api/operator/merchants/${merchantId}/products/${id}/storefront`, {
      method: 'POST',
    }),
  deleteOperatorMerchantProduct: (merchantId: string, id: string) =>
    request<void>(`/api/operator/merchants/${merchantId}/products/${id}`, { method: 'DELETE' }),
  uploadOperatorMerchantProductImage: (merchantId: string, id: string, file: File) => {
    const data = new FormData()
    data.append('file', file)
    return request<MerchantProductItem>(`/api/operator/merchants/${merchantId}/products/${id}/image`, {
      method: 'POST',
      body: data,
    })
  },
  operatorMerchantAddonGroups: (merchantId: string) =>
    request<{ items: ProductAddonGroupItem[] }>(`/api/operator/merchants/${merchantId}/addon-groups`),
  createOperatorMerchantAddonGroup: (merchantId: string, body: ProductAddonGroupItem) =>
    request<ProductAddonGroupItem>(`/api/operator/merchants/${merchantId}/addon-groups`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  updateOperatorMerchantAddonGroup: (merchantId: string, id: string, body: ProductAddonGroupItem) =>
    request<ProductAddonGroupItem>(`/api/operator/merchants/${merchantId}/addon-groups/${id}`, {
      method: 'PUT',
      body: JSON.stringify(body),
    }),
  deleteOperatorMerchantAddonGroup: (merchantId: string, id: string) =>
    request<void>(`/api/operator/merchants/${merchantId}/addon-groups/${id}`, { method: 'DELETE' }),
  saveOperatorMerchantProductAddons: (merchantId: string, id: string, addonGroupIds: string[]) =>
    request<MerchantProductItem>(`/api/operator/merchants/${merchantId}/products/${id}/addons`, {
      method: 'PUT',
      body: JSON.stringify({ addonGroupIds }),
    }),
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
  opFares: (municipalityId?: string) => {
    const params = new URLSearchParams()
    if (municipalityId) params.set('municipalityId', municipalityId)
    const qs = params.toString()
    return request<OperatorFareMatrix>(`/api/operator/fares${qs ? `?${qs}` : ''}`)
  },
  saveOperatorFares: (body: {
    vehicleType: VehicleType
    municipalityId: string
    baseFare: number
    perKm: number
    minimumFare: number
    includedKm: number
    operatorCommissionPercent: number
    driverCommissionPercent: number
    isActive: boolean
  }) => request<OperatorFareMatrix>('/api/operator/fares', { method: 'PUT', body: JSON.stringify(body) }),
  saveOperatorFareMatrix: (body: {
    municipalityId: string
    motorcycle: {
      baseFare: number
      perKm: number
      minimumFare: number
      includedKm: number
      operatorCommissionPercent: number
      driverCommissionPercent: number
      isActive: boolean
      passengerTiers: FarePassengerTier[]
    }
    tricycle: {
      baseFare: number
      perKm: number
      minimumFare: number
      includedKm: number
      operatorCommissionPercent: number
      driverCommissionPercent: number
      isActive: boolean
      passengerTiers: FarePassengerTier[]
    }
  }) => request<OperatorFareMatrix>('/api/operator/fares/matrix', { method: 'PUT', body: JSON.stringify(body) }),
  deriveFareZones: () => request<{ items: DeriveFareZoneListItem[] }>('/api/operator/derive-fares'),
  deriveFareZone: (id: string) => request<DeriveFareZoneDetail>(`/api/operator/derive-fares/${id}`),
  createDeriveFareZone: (body: SaveDeriveFareZoneBody) =>
    request<DeriveFareZoneDetail>('/api/operator/derive-fares', { method: 'POST', body: JSON.stringify(body) }),
  updateDeriveFareZone: (id: string, body: SaveDeriveFareZoneBody) =>
    request<DeriveFareZoneDetail>(`/api/operator/derive-fares/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  toggleDeriveFareZone: (id: string) =>
    request<DeriveFareZoneDetail>(`/api/operator/derive-fares/${id}/toggle`, { method: 'POST' }),
  deleteDeriveFareZone: (id: string) =>
    request<void>(`/api/operator/derive-fares/${id}`, { method: 'DELETE' }),
  addOperatorSurcharges: (body: {
    municipalityId: string
    municipalityIds: string[]
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
  addOperatorSurcharge: (vehicleType: VehicleType, municipalityId: string, body: {
    kind: SurchargeKind
    name: string
    amount: number
    windowStart?: string | null
    windowEnd?: string | null
    rangeStartUtc?: string | null
    rangeEndUtc?: string | null
    isActive: boolean
  }) => request<OperatorFareMatrix>(`/api/operator/fares/${vehicleType}/surcharges?municipalityId=${encodeURIComponent(municipalityId)}`, {
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
