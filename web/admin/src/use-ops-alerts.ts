import { useEffect, useRef } from 'react'
import {
  createOpsConnection,
  dispatchSosAlert,
  OpsAlert,
  startOpsConnection,
  stopOpsConnection,
} from './ops-hub'
import { bindSosAudioUnlock, playSosAlarm, unlockSosAudio } from './sos-alert'
import type { HubConnection } from '@microsoft/signalr'
import { HubConnectionState } from '@microsoft/signalr'

async function connectWithRetry(connection: HubConnection) {
  for (let attempt = 0; attempt < 6; attempt += 1) {
    try {
      await startOpsConnection(connection)
      return
    } catch {
      await new Promise((resolve) => window.setTimeout(resolve, 1500 * (attempt + 1)))
    }
  }
}

export function useOpsAlerts(onRefresh: () => void) {
  const refreshRef = useRef(onRefresh)
  refreshRef.current = onRefresh

  useEffect(() => {
    unlockSosAudio()
    bindSosAudioUnlock()

    const connection = createOpsConnection((payload: OpsAlert) => {
      if (payload.reason === 'sos') {
        playSosAlarm()
        dispatchSosAlert(payload)
      }
      refreshRef.current()
    })

    void connectWithRetry(connection)
    const ping = window.setInterval(() => {
      if (connection.state === HubConnectionState.Disconnected) {
        void connectWithRetry(connection)
      }
    }, 8000)

    return () => {
      window.clearInterval(ping)
      void stopOpsConnection(connection)
    }
  }, [])
}
