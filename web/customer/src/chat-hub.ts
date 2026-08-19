import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { ChatMessage, getToken } from './api'

export function createTripChatConnection() {
  const token = getToken()
  if (!token) throw new Error('Sign in again to use chat.')
  return new HubConnectionBuilder()
    .withUrl('/hubs/chat', { accessTokenFactory: () => getToken() || token })
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()
}

export async function joinTripChat(
  connection: HubConnection,
  tripId: string,
  onMessage: (message: ChatMessage) => void,
) {
  connection.off('chatMessage')
  connection.on('chatMessage', (payload: ChatMessage) => {
    if (payload?.id) onMessage(payload)
  })

  if (connection.state === HubConnectionState.Disconnected) {
    await connection.start()
  }
  await connection.invoke('JoinTrip', tripId)
}

export async function leaveTripChat(connection: HubConnection | null, tripId: string) {
  if (!connection) return
  try {
    if (connection.state === HubConnectionState.Connected) {
      await connection.invoke('LeaveTrip', tripId)
    }
  } catch {
    /* closing anyway */
  }
  try {
    await connection.stop()
  } catch {
    /* ignore */
  }
}
