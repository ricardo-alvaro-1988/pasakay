const TOKEN_KEY = 'yapasakay-customer-access'
const REFRESH_KEY = 'yapasakay-customer-refresh'

export type VehicleType = 'Motorcycle' | 'Tricycle'
export type PaymentMethod = 'Cash' | 'GCash' | 'Maya' | 'Other'
export type TripStatus = 'Pending' | 'Waiting' | 'Ongoing' | 'Completed' | 'Cancelled'
export type Gender = 'Male' | 'Female' | 'Other'
export type DeleteAccountStatus = 'None' | 'Pending' | 'Approved' | 'Rejected'

export type AuthResponse = {
  accessToken: string
  refreshToken: string
  expiresAtUtc: string
  user: { role: string; fullName: string; phoneNumber: string }
}

export type Place = {
  barangayId: string
  label: string
  details: string
  barangay: string
  municipality: string
  lat: number
  lng: number
}

export type CustomerTrip = {
  id: string
  reference: string
  status: TripStatus
  pickup: string
  dropoff: string
  pickupLat: number | null
  pickupLng: number | null
  dropoffLat: number | null
  dropoffLng: number | null
  fare: number
  distanceKm: number
  passengerCount?: number
  vehicleType: VehicleType
  paymentMethod: PaymentMethod | number
  paymentMethodOther: string | null
  operatorName: string
  riderName: string | null
  riderPhone: string | null
  plateNumber: string | null
  vehicleModel: string | null
  riderPhotoUrl: string | null
  riderLat: number | null
  riderLng: number | null
  requestedAtUtc: string
  scheduledAtUtc: string | null
  canCancel: boolean
  canSos: boolean
  hailQr?: boolean
  rating?: number | null
  ratingComment?: string | null
  canRate?: boolean
  canViewChat?: boolean
  canChat?: boolean
  customerFare?: number
  promoDiscountAmount?: number
  isPromoSponsored?: boolean
  discountPercent?: number | null
  promoCode?: string | null
  customerBoostAmount?: number
}

export type Desk = {
  customerId: string
  fullName: string
  firstName: string
  lastName: string
  phoneNumber: string
  email: string | null
  gender: Gender | null
  hasPin: boolean
  deleteStatus: DeleteAccountStatus
  activeTrip: CustomerTrip | null
  scheduled: CustomerTrip[]
  recent: CustomerTrip[]
  places: Place[]
  mapLat: number | null
  mapLng: number | null
  hailedRider: HailRider | null
  pendingRating?: CustomerTrip | null
  needsMobile?: boolean
}

export type Quote = {
  fare: number
  distanceKm: number
  etaMinutes: number
  operatorName: string
  vehicleType: VehicleType
  paymentMethod: PaymentMethod
  riderAvailable?: boolean
  bookingDispatchMode?: BookingDispatchMode
  originalFare?: number
  customerFare?: number
  promoApplied?: boolean
  discountPercent?: number | null
  promoCode?: string | null
  hasActivePromos?: boolean
  customerBoostAmount?: number
}

export type BookingDispatchMode = 'Broadcast' | 'Selection' | 'Both'

export type PabiliOrderDetail = {
  id: string
  reference: string
  status: string
  merchantName: string
  pickupAddress: string
  dropoffAddress: string
  pickupLat: number
  pickupLng: number
  dropoffLat: number
  dropoffLng: number
  goodsSubtotal: number
  deliveryFee: number
  surchargeTotal: number
  adjustmentAmount: number
  adjustmentLabel: string
  customerTotal: number
  paymentMethod: string
  riderName: string | null
  riderPhone: string | null
  canCancel: boolean
  items: Array<{
    id: string
    name: string
    quantity: number
    lineSellingTotal: number
    addons: Array<{ name: string }>
  }>
}

export type Stop = {
  label: string
  details: string
  lat: number
  lng: number
  barangayId?: string
}

export type BookBody = {
  vehicleType: VehicleType
  pickupBarangayId?: string
  pickupDetails: string
  pickupLat: number
  pickupLng: number
  dropoffBarangayId?: string
  dropoffDetails: string
  dropoffLat: number
  dropoffLng: number
  paymentMethod: PaymentMethod
  paymentMethodOther?: string
  scheduledAtUtc?: string
  riderId?: string
  passengerCount?: number
  promoCode?: string
  customerBoostAmount?: number
}

export type HailRider = {
  riderId: string
  fullName: string
  plateNumber: string
  vehicleType: VehicleType
  vehicleModel: string | null
  photoUrl: string | null
  phoneNumber: string | null
  isOnline: boolean
  isBusy: boolean
  companyName: string
  paymentMethods: PaymentMethod[]
  distanceKm?: number | null
}

export type ChatMessage = {
  id: string
  sender: string | number
  body: string
  sentAtUtc: string
  photoUrl?: string | null
}

export type RideStop = {
  details: string
  barangay: string
  municipality: string
  province: string
  fullAddress: string
}

export type CustomerTripDetail = {
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
  paymentMethod: PaymentMethod | number
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
  chat: ChatMessage[]
  customerFare?: number
  promoDiscountAmount?: number
  isPromoSponsored?: boolean
  discountPercent?: number | null
  promoCode?: string | null
  customerBoostAmount?: number
}

export function chatFromRider(sender: unknown) {
  const value = String(sender ?? '').toLowerCase()
  return value === 'rider' || value === '2'
}

const LIVE_CHAT_STATUSES = new Set(['Waiting', 'Ongoing', '5', '3'])

export function tripCanViewChat(trip: { status: unknown; canChat?: boolean | null }) {
  return tripCanSendChat(trip)
}

export function tripCanSendChat(trip: {
  status: unknown
  canChat?: boolean | null
}) {
  if (typeof trip.canChat === 'boolean') return trip.canChat
  return LIVE_CHAT_STATUSES.has(String(trip.status))
}

export function paymentLabel(method: unknown, other?: string | null) {
  const value = String(method ?? 'Cash').toLowerCase()
  const name = value === 'gcash' || value === '2' ? 'GCASH'
    : value === 'maya' || value === '3' ? 'MAYA'
    : value === 'other' || value === '4' ? 'OTHERS'
    : 'CASH'
  const extra = (other ?? '').trim()
  return extra ? `${name} · ${extra}` : name
}

export function peso(value: number) {
  return `₱${Number(value || 0).toFixed(0)}`
}

export function kmLabel(value: number | null | undefined) {
  const km = Number(value || 0)
  if (!km) return ''
  return `${km.toFixed(km >= 10 ? 0 : 1)} km`
}

export function passengerLabel(value: number | null | undefined) {
  const count = Math.max(1, Number(value || 1))
  return `${count} passenger${count === 1 ? '' : 's'}`
}

export function tripHeadline(status: string) {
  if (status === 'Pending') return 'Finding a rider'
  if (status === 'Waiting') return 'Rider on the way'
  if (status === 'Ongoing') return 'On your trip'
  if (status === 'Completed') return 'Trip completed'
  if (status === 'Cancelled') return 'Cancelled'
  return status
}

export function isOperatorCoverageError(message: string) {
  const lower = message.toLowerCase()
  return lower.includes('no operator covers')
    || lower.includes('must match a philippine barangay')
    || lower.includes('service area')
    || lower.includes('outside this operator')
    || lower.includes('outside that rider')
}

export const NO_OPERATOR_NOTICE_DELAY_MS = 10_000
export const NO_OPERATOR_FACEBOOK_URL = 'https://www.facebook.com/profile.php?id=61592066454711'
export const NO_OPERATOR_EMAIL = 'contactus@enovasoftware.com'

export function phWhen(value: string) {
  const raw = String(value ?? '').trim()
  if (!raw) return '—'
  const stamp = /[zZ]$|[+-]\d{2}:?\d{2}$/.test(raw)
    ? new Date(raw)
    : new Date(raw.includes('T') ? `${raw}Z` : `${raw}T00:00:00Z`)
  return stamp.toLocaleString('en-PH', { timeZone: 'Asia/Manila' })
}

export function mediaUrl(path?: string | null) {
  const raw = String(path ?? '').trim()
  if (!raw) return ''
  if (raw.startsWith('blob:') || raw.startsWith('data:')) return raw
  if (/^https?:\/\//i.test(raw)) {
    try {
      const url = new URL(raw)
      if (url.pathname.startsWith('/uploads') || url.pathname.startsWith('/api')) {
        return `${url.pathname}${url.search}`
      }
    } catch {
      return raw
    }
    return raw
  }
  return raw.startsWith('/') ? raw : `/uploads/${raw.replace(/^uploads\//i, '')}`
}

export async function toChatJpeg(file: File) {
  const bitmap = await createImageBitmap(file)
  const max = 1600
  const scale = Math.min(1, max / Math.max(bitmap.width, bitmap.height, 1))
  const width = Math.max(1, Math.round(bitmap.width * scale))
  const height = Math.max(1, Math.round(bitmap.height * scale))
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const ctx = canvas.getContext('2d')
  if (!ctx) {
    bitmap.close()
    return file
  }
  ctx.drawImage(bitmap, 0, 0, width, height)
  bitmap.close()
  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.82))
  if (!blob) return file
  return new File([blob], 'chat.jpg', { type: 'image/jpeg' })
}

export function getToken() {
  return localStorage.getItem(TOKEN_KEY)
}

function getRefreshToken() {
  return localStorage.getItem(REFRESH_KEY)
}

export function saveAuth(auth: AuthResponse) {
  localStorage.setItem(TOKEN_KEY, auth.accessToken)
  localStorage.setItem(REFRESH_KEY, auth.refreshToken)
}

/** @deprecated prefer saveAuth — kept for call sites during transition */
export function saveToken(token: string) {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(REFRESH_KEY)
}

let refreshInFlight: Promise<boolean> | null = null

async function refreshSession(): Promise<boolean> {
  const refreshToken = getRefreshToken()
  if (!refreshToken) {
    return false
  }
  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      try {
        const res = await fetch('/api/auth/refresh', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken }),
        })
        if (!res.ok) {
          clearToken()
          return false
        }
        const auth = (await res.json()) as AuthResponse
        if (!auth.accessToken || !auth.refreshToken) {
          clearToken()
          return false
        }
        saveAuth(auth)
        return true
      } catch {
        return false
      } finally {
        refreshInFlight = null
      }
    })()
  }
  return refreshInFlight
}

function shouldAttemptRefresh(path: string) {
  return !path.startsWith('/api/auth/refresh')
    && !path.startsWith('/api/auth/google')
    && !path.startsWith('/api/auth/login')
}

async function request<T>(path: string, init?: RequestInit, retried = false): Promise<T> {
  const headers = new Headers(init?.headers)
  if (!headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const res = await fetch(path, { ...init, headers })
  if (res.status === 401) {
    if (!retried && shouldAttemptRefresh(path) && (await refreshSession())) {
      return request<T>(path, init, true)
    }
    clearToken()
    throw new Error('Session expired. Sign in again.')
  }
  if (!res.ok) {
    let message = res.status === 404
      ? 'This action is not available yet. Restart the API and try again.'
      : 'Request failed.'
    try {
      const body = (await res.json()) as { message?: string; title?: string; detail?: string }
      if (body.message) message = body.message
      else if (body.detail) message = body.detail
      else if (body.title && res.status !== 404) message = body.title
    } catch {
      /* ignore */
    }
    throw new Error(message)
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

async function requestForm<T>(path: string, body: FormData, retried = false): Promise<T> {
  const headers = new Headers()
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const res = await fetch(path, { method: 'POST', headers, body })
  if (res.status === 401) {
    if (!retried && shouldAttemptRefresh(path) && (await refreshSession())) {
      return requestForm<T>(path, body, true)
    }
    clearToken()
    throw new Error('Session expired. Sign in again.')
  }
  if (!res.ok) {
    let message = 'Request failed.'
    try {
      const json = (await res.json()) as { message?: string }
      if (json.message) message = json.message
    } catch {
      /* ignore */
    }
    throw new Error(message)
  }
  return (await res.json()) as T
}

export const api = {
  authConfig: () => request<{ googleClientId: string }>('/api/public/auth'),
  branding: () =>
    request<{
      brandName: string
      shortName: string
      logoUrl: string | null
      faviconUrl: string | null
      themeId: string
      accent: string
      good: string
    }>('/api/public/branding'),
  googleSignIn: (idToken: string) =>
    request<AuthResponse>('/api/auth/google', { method: 'POST', body: JSON.stringify({ idToken }) }),
  mapsConfig: () => request<{ googleMapsBrowserKey: string }>('/api/public/maps'),
  desk: () => request<Desk>('/api/customer/desk'),
  customerServices: (opts?: { lat?: number; lng?: number; barangayId?: string }) => {
    const params = new URLSearchParams()
    if (opts?.lat != null) params.set('lat', String(opts.lat))
    if (opts?.lng != null) params.set('lng', String(opts.lng))
    if (opts?.barangayId) params.set('barangayId', opts.barangayId)
    const q = params.toString()
    return request<{ pabiliEnabled: boolean }>(`/api/customer/services${q ? `?${q}` : ''}`)
  },
  quote: (body: BookBody) => request<Quote>('/api/customer/quote', { method: 'POST', body: JSON.stringify(body) }),
  availableRiders: (opts: {
    vehicleType: VehicleType
    paymentMethod: PaymentMethod
    pickupLat: number
    pickupLng: number
    pickupDetails: string
    pickupBarangayId?: string
  }) => {
    const params = new URLSearchParams({
      vehicleType: opts.vehicleType,
      paymentMethod: opts.paymentMethod,
      pickupLat: String(opts.pickupLat),
      pickupLng: String(opts.pickupLng),
      pickupDetails: opts.pickupDetails,
    })
    if (opts.pickupBarangayId) params.set('pickupBarangayId', opts.pickupBarangayId)
    return request<HailRider[]>(`/api/customer/riders/available?${params}`)
  },
  serviceCheck: (body: {
    pickupBarangayId?: string
    pickupDetails: string
    pickupLat: number
    pickupLng: number
    dropoffBarangayId?: string
    dropoffDetails: string
  }) => request<{
    municipalityHasOperator: boolean
    municipalityName: string | null
    motorcycleAvailable: boolean
    tricycleAvailable: boolean
  }>('/api/customer/service-check', {
    method: 'POST',
    body: JSON.stringify(body),
  }),
  book: (body: BookBody) => request<Desk>('/api/customer/book', { method: 'POST', body: JSON.stringify(body) }),
  clearHail: () => request<Desk>('/api/customer/hail/clear', { method: 'POST' }),
  cancel: (id: string) => request<Desk>(`/api/customer/trips/${id}/cancel`, { method: 'POST' }),
  tripDetail: (id: string) => request<CustomerTripDetail>(`/api/customer/trips/${id}`),
  rate: (id: string, rating: number, comment?: string) =>
    request<Desk>(`/api/customer/trips/${id}/rate`, {
      method: 'POST',
      body: JSON.stringify({ rating, comment }),
    }),
  registerDevice: (token: string, platform = 'Web') =>
    request('/api/devices/register', { method: 'POST', body: JSON.stringify({ token, platform }) }),
  chat: (tripId: string) => request<ChatMessage[]>(`/api/customer/trips/${tripId}/chat`),
  sendChat: (tripId: string, body: string) =>
    request<ChatMessage>(`/api/customer/trips/${tripId}/chat`, { method: 'POST', body: JSON.stringify({ body }) }),
  sendChatPhoto: (tripId: string, file: File, body?: string) => {
    const data = new FormData()
    data.append('photo', file)
    if (body?.trim()) data.append('body', body.trim())
    return requestForm<ChatMessage>(`/api/customer/trips/${tripId}/chat/photo`, data)
  },
  sos: (tripId: string, lat?: number, lng?: number) =>
    request('/api/sos', { method: 'POST', body: JSON.stringify({ tripId, message: 'Customer SOS', lat, lng }) }),
  pabiliMerchants: (opts: { lat: number; lng: number; barangayId?: string; q?: string }) => {
    const params = new URLSearchParams({
      lat: String(opts.lat),
      lng: String(opts.lng),
    })
    if (opts.barangayId) params.set('barangayId', opts.barangayId)
    if (opts.q?.trim()) params.set('q', opts.q.trim())
    return request<Array<{
      id: string
      name: string
      address: string
      logoUrl: string | null
      coverUrl: string | null
      isOpen: boolean
      latitude: number
      longitude: number
    }>>(`/api/customer/pabili/merchants?${params}`)
  },
  pabiliPopularProducts: (opts: { lat: number; lng: number; barangayId?: string; q?: string }) => {
    const params = new URLSearchParams({
      lat: String(opts.lat),
      lng: String(opts.lng),
    })
    if (opts.barangayId) params.set('barangayId', opts.barangayId)
    if (opts.q?.trim()) params.set('q', opts.q.trim())
    return request<Array<{
      id: string
      merchantId: string
      merchantName: string
      name: string
      description: string
      sellingPrice: number
      imageUrl: string | null
      merchantOpen: boolean
    }>>(`/api/customer/pabili/products/popular?${params}`)
  },
  pabiliAds: (opts: { lat: number; lng: number; barangayId?: string }) => {
    const params = new URLSearchParams({
      lat: String(opts.lat),
      lng: String(opts.lng),
    })
    if (opts.barangayId) params.set('barangayId', opts.barangayId)
    return request<Array<{
      id: string
      title: string
      imageUrl: string | null
      redirectUrl: string
    }>>(`/api/customer/pabili/ads?${params}`)
  },
  pabiliSuggest: (opts: { lat: number; lng: number; barangayId?: string; q: string }) => {
    const params = new URLSearchParams({
      lat: String(opts.lat),
      lng: String(opts.lng),
      q: opts.q.trim(),
    })
    if (opts.barangayId) params.set('barangayId', opts.barangayId)
    return request<{
      merchants: Array<{ id: string; name: string; address: string; logoUrl: string | null }>
      products: Array<{
        id: string
        merchantId: string
        merchantName: string
        name: string
        sellingPrice: number
        imageUrl: string | null
      }>
    }>(`/api/customer/pabili/search/suggest?${params}`)
  },
  pabiliStore: (id: string) =>
    request<{
      id: string
      name: string
      address: string
      logoUrl: string | null
      coverUrl: string | null
      isOpen: boolean
      latitude: number
      longitude: number
      categories: Array<{ id: string; name: string }>
      products: Array<{
        id: string
        categoryId: string | null
        categoryName: string
        name: string
        description: string
        sellingPrice: number
        imageUrl: string | null
        addonGroups: Array<{
          id: string
          name: string
          minSelect: number
          maxSelect: number
          options: Array<{ id: string; name: string; sellingPrice: number; basePrice: number }>
        }>
      }>
    }>(`/api/customer/pabili/merchants/${id}`),
  pabiliQuote: (body: {
    merchantId: string
    dropoffLat: number
    dropoffLng: number
    dropoffBarangayId?: string
    items: Array<{ productId: string; quantity: number; addons?: Array<{ optionId: string; quantity: number }> }>
  }) =>
    request<{
      merchantId: string
      merchantName: string
      goodsSubtotal: number
      deliveryFee: number
      surchargeTotal: number
      distanceKm: number
      adjustmentAmount: number
      adjustmentLabel: string
      customerTotal: number
    }>('/api/customer/pabili/quote', { method: 'POST', body: JSON.stringify(body) }),
  pabiliPlace: (body: {
    merchantId: string
    dropoffAddress: string
    dropoffLat: number
    dropoffLng: number
    dropoffBarangayId?: string
    paymentMethod: PaymentMethod
    notes?: string
    items: Array<{ productId: string; quantity: number; addons?: Array<{ optionId: string; quantity: number }> }>
  }) =>
    request<PabiliOrderDetail>('/api/customer/pabili/orders', { method: 'POST', body: JSON.stringify(body) }),
  pabiliOrders: () =>
    request<PabiliOrderDetail[]>('/api/customer/pabili/orders'),
  pabiliOrder: (id: string) =>
    request<PabiliOrderDetail>(`/api/customer/pabili/orders/${id}`),
  pabiliCancelOrder: (id: string, reason?: string) =>
    request<PabiliOrderDetail>(`/api/customer/pabili/orders/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  updateProfile: (body: { firstName: string; lastName: string; gender: Gender; email: string }) =>
    request<Desk>('/api/customer/account/profile', { method: 'PUT', body: JSON.stringify(body) }),
  setPin: (pin: string, currentPin?: string) =>
    request<Desk>('/api/customer/account/pin', { method: 'POST', body: JSON.stringify({ pin, currentPin }) }),
  updateMobile: (newPhone: string) =>
    request<Desk>('/api/customer/account/mobile', { method: 'PUT', body: JSON.stringify({ newPhone }) }),
  deleteAccount: (reason: string, pin?: string) =>
    request<Desk>('/api/customer/account/delete', { method: 'POST', body: JSON.stringify({ reason, pin }) }),
}
