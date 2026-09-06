import { useEffect, useRef } from 'react'
import { api } from './api'
import { loadGoogleMaps } from './FleetMap'

export type DeriveMapPoint = { lat: number; lng: number }

type Props = {
  points: DeriveMapPoint[]
  onChange: (points: DeriveMapPoint[]) => void
  height?: number
}

type GMaps = {
  Map: new (el: HTMLElement, opts: Record<string, unknown>) => GMap
  Marker: new (opts: Record<string, unknown>) => GMarker
  Polygon: new (opts: Record<string, unknown>) => GPolygon
  LatLngBounds: new () => GBounds
  event: { addListener: (target: unknown, name: string, handler: (e: { latLng?: { lat: () => number; lng: () => number } }) => void) => void }
}

type GMap = {
  setCenter: (p: DeriveMapPoint) => void
  setZoom: (z: number) => void
  fitBounds: (bounds: GBounds, padding?: number) => void
}

type GMarker = { setMap: (map: GMap | null) => void }
type GPolygon = { setMap: (map: GMap | null) => void }
type GBounds = { extend: (p: DeriveMapPoint) => void }

const DEFAULT_CENTER = { lat: 13.4115, lng: 121.1803 }

export function DeriveZoneMap({ points, onChange, height = 360 }: Props) {
  const host = useRef<HTMLDivElement>(null)
  const mapRef = useRef<GMap | null>(null)
  const gmapsRef = useRef<GMaps | null>(null)
  const polygonRef = useRef<GPolygon | null>(null)
  const markersRef = useRef<GMarker[]>([])
  const pointsRef = useRef(points)
  pointsRef.current = points
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange

  useEffect(() => {
    let cancelled = false
    async function boot() {
      if (!host.current) return
      const { googleMapsBrowserKey: key } = await api.mapsConfig()
      if (!key || cancelled) return
      const gmaps = await loadGoogleMaps(key) as unknown as GMaps
      if (cancelled || !host.current) return
      gmapsRef.current = gmaps
      const map = new gmaps.Map(host.current, {
        center: pointsRef.current[0] ?? DEFAULT_CENTER,
        zoom: pointsRef.current.length > 0 ? 14 : 12,
        mapTypeControl: false,
        streetViewControl: false,
        fullscreenControl: false,
      })
      mapRef.current = map
      gmaps.event.addListener(map, 'click', (e) => {
        if (!e.latLng) return
        const next = [...pointsRef.current, { lat: e.latLng.lat(), lng: e.latLng.lng() }]
        onChangeRef.current(next)
      })
      draw(gmaps, map, pointsRef.current)
    }
    void boot().catch(() => {})
    return () => {
      cancelled = true
      markersRef.current.forEach((m) => m.setMap(null))
      markersRef.current = []
      polygonRef.current?.setMap(null)
      polygonRef.current = null
      mapRef.current = null
      gmapsRef.current = null
    }
  }, [])

  useEffect(() => {
    const map = mapRef.current
    const gmaps = gmapsRef.current
    if (!map || !gmaps) return
    draw(gmaps, map, points)
    if (points.length >= 1) {
      const bounds = new gmaps.LatLngBounds()
      points.forEach((p) => bounds.extend(p))
      if (points.length === 1) {
        map.setCenter(points[0])
        map.setZoom(15)
      } else {
        map.fitBounds(bounds, 48)
      }
    }
  }, [points])

  function draw(gmaps: GMaps, map: GMap, ring: DeriveMapPoint[]) {
    markersRef.current.forEach((m) => m.setMap(null))
    markersRef.current = ring.map((p, index) => new gmaps.Marker({
      map,
      position: p,
      label: { text: String(index + 1), color: '#fff', fontSize: '11px', fontWeight: '700' },
    }))
    polygonRef.current?.setMap(null)
    polygonRef.current = null
    if (ring.length >= 3) {
      polygonRef.current = new gmaps.Polygon({
        map,
        paths: ring,
        strokeColor: '#047857',
        strokeOpacity: 0.95,
        strokeWeight: 2,
        fillColor: '#10b981',
        fillOpacity: 0.28,
        clickable: false,
      })
    }
  }

  return (
    <div>
      <div ref={host} style={{ width: '100%', height, borderRadius: 14, border: '1px solid var(--line)' }} />
      <div style={{ display: 'flex', gap: 8, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
        <p className="muted" style={{ margin: 0, flex: 1 }}>
          Click the map to add polygon points ({points.length} point{points.length === 1 ? '' : 's'}). Need at least 3.
        </p>
        <button className="btn tiny" type="button" disabled={points.length === 0} onClick={() => onChange(points.slice(0, -1))}>
          Undo point
        </button>
        <button className="btn tiny danger" type="button" disabled={points.length === 0} onClick={() => onChange([])}>
          Clear polygon
        </button>
      </div>
    </div>
  )
}
