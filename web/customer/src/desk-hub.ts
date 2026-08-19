import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { ChatMessage, getToken } from './api'

type ChatHandler = (message: ChatMessage) => void
const chatListeners = new Set<ChatHandler>()

export function listenDeskChat(handler: ChatHandler) {
  chatListeners.add(handler)
  return () => {
    chatListeners.delete(handler)
  }
}

export function emitDeskChat(message: ChatMessage) {
  if (!message?.id) return
  chatListeners.forEach((handler) => handler(message))
}

export function createDeskConnection() {
  const token = getToken()
  if (!token) throw new Error('Sign in again.')
  return new HubConnectionBuilder()
    .withUrl('/hubs/desk', { accessTokenFactory: () => getToken() || token })
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()
}

export async function startDeskHub(
  connection: HubConnection,
  onChanged: (reason?: string) => void,
  onChat?: (message: ChatMessage) => void,
) {
  connection.off('deskChanged')
  connection.on('deskChanged', (payload?: { reason?: string }) => {
    onChanged(payload?.reason)
  })
  connection.off('chatMessage')
  if (onChat) {
    connection.on('chatMessage', (payload: ChatMessage) => {
      if (payload?.id) onChat(payload)
    })
  }

  if (connection.state === HubConnectionState.Disconnected) {
    await connection.start()
  }
}

export async function stopDeskHub(connection: HubConnection | null) {
  if (!connection) return
  try {
    await connection.stop()
  } catch {
    /* ignore */
  }
}
