import motorcycleImg from './assets/rider-motorcycle-map.png'
import tricycleImg from './assets/tricycle.png'
import { VehicleType } from './api'

export const VEHICLE_ART: Record<VehicleType, string> = {
  Motorcycle: motorcycleImg,
  Tricycle: tricycleImg,
}
