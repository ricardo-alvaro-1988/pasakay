import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
} from '@microsoft/signalr'
import { getToken } from './api'

export type OpsAlert = {
  reason?: string
  tripId?: string
  ticketId?: string
  reference?: string
  operatorId?: string
  lat?: number | null
  lng?: number | null
  openedBy?: string
  atUtc?: string | null
}

export const SOS_ALERT_EVENT = 'yp-sos-alert'

export function dispatchSosAlert(payload: OpsAlert) {
  window.dispatchEvent(new CustomEvent(SOS_ALERT_EVENT, { detail: payload }))
}

export function createOpsConnection(onAlert: (payload: OpsAlert) => void) {
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/ops', {
      accessTokenFactory: () => getToken() ?? '',
    })
    .withAutomaticReconnect([0, 500, 1000, 2000, 5000])
    .withKeepAliveInterval(10_000)
    .withServerTimeout(40_000)
    .build()

  connection.on('opsAlert', (payload: OpsAlert) => {
    onAlert(payload)
  })

  return connection
}

export async function startOpsConnection(connection: HubConnection | null) {
  if (!connection || connection.state !== HubConnectionState.Disconnected) {
    return
  }
  await connection.start()
}

export async function stopOpsConnection(connection: HubConnection | null) {
  if (!connection) {
    return
  }
  try {
    await connection.stop()
  } catch {
    /* ignore */
  }
}
