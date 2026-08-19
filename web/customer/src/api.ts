const TOKEN_KEY = 'yapasakay-customer-access'

export type VehicleType = 'Motorcycle' | 'Tricycle'
export type PaymentMethod = 'Cash' | 'GCash' | 'Maya' | 'Other'
export type TripStatus = 'Pending' | 'Waiting' | 'Ongoing' | 'Completed' | 'Cancelled'
export type Gender = 'Male' | 'Female' | 'Other'
export type DeleteAccountStatus = 'None' | 'Pending' | 'Approved' | 'Rejected'

export type AuthResponse = {
  accessToken: string
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
  return new Date(value).toLocaleString('en-PH', { timeZone: 'Asia/Manila' })
}

export function getToken() {
  return localStorage.getItem(TOKEN_KEY)
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

export function saveToken(token: string) {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY)
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  if (!headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const res = await fetch(path, { ...init, headers })
  if (res.status === 401) {
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

async function requestForm<T>(path: string, body: FormData): Promise<T> {
  const headers = new Headers()
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const res = await fetch(path, { method: 'POST', headers, body })
  if (res.status === 401) {
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
  googleSignIn: (idToken: string) =>
    request<AuthResponse>('/api/auth/google', { method: 'POST', body: JSON.stringify({ idToken }) }),
  mapsConfig: () => request<{ googleMapsBrowserKey: string }>('/api/public/maps'),
  desk: () => request<Desk>('/api/customer/desk'),
  quote: (body: BookBody) => request<Quote>('/api/customer/quote', { method: 'POST', body: JSON.stringify(body) }),
  serviceCheck: (body: {
    pickupBarangayId?: string
    pickupDetails: string
    pickupLat: number
    pickupLng: number
    dropoffBarangayId?: string
    dropoffDetails: string
  }) => request<{ municipalityHasOperator: boolean; municipalityName: string | null }>('/api/customer/service-check', {
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
  updateProfile: (body: { firstName: string; lastName: string; gender: Gender; email: string }) =>
    request<Desk>('/api/customer/account/profile', { method: 'PUT', body: JSON.stringify(body) }),
  setPin: (pin: string, currentPin?: string) =>
    request<Desk>('/api/customer/account/pin', { method: 'POST', body: JSON.stringify({ pin, currentPin }) }),
  updateMobile: (newPhone: string) =>
    request<Desk>('/api/customer/account/mobile', { method: 'PUT', body: JSON.stringify({ newPhone }) }),
  deleteAccount: (reason: string, pin?: string) =>
    request<Desk>('/api/customer/account/delete', { method: 'POST', body: JSON.stringify({ reason, pin }) }),
}
