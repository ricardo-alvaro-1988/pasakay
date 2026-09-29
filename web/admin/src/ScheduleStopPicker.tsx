import { MerchantPin, MerchantPinMap } from './MerchantPinMap'

export type ScheduleStop = MerchantPin

type Props = {
  label: string
  value: ScheduleStop | null
  onChange: (pin: ScheduleStop) => void
  height?: number
}

const EMPTY: ScheduleStop = { lat: 13.4115, lng: 121.1803, address: '' }

/** Map search + pin for operator desk pickup/dropoff (same pattern as merchant pin). */
export function ScheduleStopPicker({ label, value, onChange, height = 240 }: Props) {
  const pin = value ?? EMPTY
  return (
    <div className="field wide schedule-stop-picker">
      <span>{label}</span>
      <MerchantPinMap value={pin} onChange={onChange} height={height} />
      {value?.address ? (
        <small className="muted" style={{ display: 'block', marginTop: 6 }}>{value.address}</small>
      ) : (
        <small className="muted" style={{ display: 'block', marginTop: 6 }}>
          Search or tap the map to set this stop.
        </small>
      )}
    </div>
  )
}
