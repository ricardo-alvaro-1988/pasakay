import { FormEvent, useEffect, useRef, useState } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import { api, ChatMessage, chatFromRider, mediaUrl, phWhen, toChatJpeg } from './api'
import { createTripChatConnection, joinTripChat, leaveTripChat } from './chat-hub'
import { listenDeskChat } from './desk-hub'

export function TripChatPanel({
  tripId,
  open,
  onClose,
  onError,
  onUnread,
  canSend = true,
}: {
  tripId: string
  open: boolean
  onClose: () => void
  onError: (text: string) => void
  onUnread?: (count: number) => void
  canSend?: boolean
}) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [live, setLive] = useState(false)
  const [linkError, setLinkError] = useState<string | null>(null)
  const bottom = useRef<HTMLDivElement>(null)
  const hub = useRef<HubConnection | null>(null)
  const camera = useRef<HTMLInputElement>(null)
  const gallery = useRef<HTMLInputElement>(null)
  const openRef = useRef(open)
  const unreadRef = useRef(0)
  const seenIds = useRef(new Set<string>())
  const liveRef = useRef(false)
  const connectingRef = useRef(false)
  openRef.current = open
  liveRef.current = live

  useEffect(() => {
    let ignore = false
    seenIds.current = new Set()

    function messageId(message: ChatMessage) {
      return String(message.id || (message as ChatMessage & { Id?: string }).Id || '')
    }

    function remember(rows: ChatMessage[]) {
      return rows.map((row) => {
        const id = messageId(row)
        if (id) seenIds.current.add(id)
        return {
          ...row,
          id: id || row.id,
          photoUrl: row.photoUrl || (row as ChatMessage & { PhotoUrl?: string }).PhotoUrl || null,
        }
      })
    }

    function upsert(message: ChatMessage) {
      const id = messageId(message)
      if (!id || seenIds.current.has(id)) return
      seenIds.current.add(id)
      const photoUrl = message.photoUrl || (message as ChatMessage & { PhotoUrl?: string }).PhotoUrl || null
      const next = { ...message, id, photoUrl }
      if (!openRef.current && chatFromRider(next.sender)) {
        unreadRef.current += 1
        onUnread?.(unreadRef.current)
      }
      setMessages((prev) => (prev.some((m) => m.id === id) ? prev : [...prev, next]))
    }

    async function connectLive() {
      if (ignore || connectingRef.current || liveRef.current) return
      connectingRef.current = true
      const existing = hub.current
      if (existing) {
        try { await existing.stop() } catch { /* retrying */ }
        hub.current = null
      }
      try {
        const connection = createTripChatConnection()
        hub.current = connection
        connection.onreconnecting(() => {
          if (!ignore) {
            setLive(false)
            setLinkError('Reconnecting to live chat…')
          }
        })
        connection.onreconnected(() => {
          void connection.invoke('JoinTrip', tripId).then(() => {
            if (!ignore) {
              setLive(true)
              setLinkError(null)
            }
          }).catch(() => {
            if (!ignore) {
              setLive(false)
              setLinkError('Could not rejoin live chat.')
            }
          })
        })
        connection.onclose(() => {
          if (ignore) return
          setLive(false)
          setLinkError('Live chat disconnected. Retrying…')
        })
        await joinTripChat(connection, tripId, upsert)
        if (!ignore) {
          setLive(true)
          setLinkError(null)
        }
      } catch (err) {
        if (!ignore) {
          setLive(false)
          setLinkError(err instanceof Error ? err.message : 'Could not connect to live chat. Retrying…')
        }
      } finally {
        connectingRef.current = false
      }
    }

    async function boot() {
      try {
        const rows = await api.chat(tripId)
        if (ignore) return
        setMessages(remember(rows))
      } catch (err) {
        if (!ignore) onError(err instanceof Error ? err.message : 'Could not load chat.')
      }
      await connectLive()
    }

    void boot()
    const stopDesk = listenDeskChat(upsert)

    const fallback = window.setInterval(() => {
      if (ignore) return
      if (!liveRef.current) {
        api.chat(tripId).then((rows) => {
          if (!ignore) setMessages(remember(rows))
        }).catch(() => {})
        void connectLive()
      }
    }, 2500)

    return () => {
      ignore = true
      window.clearInterval(fallback)
      stopDesk()
      const connection = hub.current
      hub.current = null
      void leaveTripChat(connection, tripId)
      setLive(false)
    }
  }, [tripId])

  useEffect(() => {
    if (open) {
      unreadRef.current = 0
      onUnread?.(0)
    }
  }, [open, onUnread])

  useEffect(() => {
    if (open) bottom.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages.length, open])

  async function send(e: FormEvent) {
    e.preventDefault()
    const body = text.trim()
    if (!body || busy || !canSend) return
    setBusy(true)
    try {
      const sent = await api.sendChat(tripId, body)
      if (sent.id) seenIds.current.add(sent.id)
      setMessages((prev) => (prev.some((m) => m.id === sent.id) ? prev : [...prev, sent]))
      setText('')
    } catch (err) {
      onError(err instanceof Error ? err.message : 'Could not send message.')
    } finally {
      setBusy(false)
    }
  }

  async function sendPhoto(file: File | undefined) {
    if (!file || busy || !canSend) return
    setBusy(true)
    const localUrl = URL.createObjectURL(file)
    const tempId = `local-${Date.now()}`
    setMessages((prev) => [
      ...prev,
      {
        id: tempId,
        sender: 'Customer',
        body: text.trim(),
        sentAtUtc: new Date().toISOString(),
        photoUrl: localUrl,
      },
    ])
    try {
      const jpeg = await toChatJpeg(file).catch(() => file)
      const sent = await api.sendChatPhoto(tripId, jpeg, text.trim() || undefined)
      if (sent.id) seenIds.current.add(sent.id)
      setMessages((prev) => {
        const photoUrl = sent.photoUrl || (sent as ChatMessage & { PhotoUrl?: string }).PhotoUrl || localUrl
        const withoutTemp = prev.filter((item) => item.id !== tempId && item.id !== sent.id)
        return [...withoutTemp, { ...sent, photoUrl }]
      })
      setText('')
      window.setTimeout(() => URL.revokeObjectURL(localUrl), 2000)
    } catch (err) {
      setMessages((prev) => prev.filter((item) => item.id !== tempId))
      URL.revokeObjectURL(localUrl)
      onError(err instanceof Error ? err.message : 'Could not send photo.')
    } finally {
      setBusy(false)
      if (camera.current) camera.current.value = ''
      if (gallery.current) gallery.current.value = ''
    }
  }

  if (!open) return null

  return (
    <div className="chat-sheet">
      <div className="chat-head">
        <b>{live ? 'Chat with rider' : linkError || 'Chat with rider · reconnecting'}</b>
        <button type="button" className="ghost" onClick={onClose}>Close</button>
      </div>
      <div className="chat-log">
        {messages.length === 0 && <p className="muted">No messages yet. Say hello, send a photo, or share a landmark.</p>}
        {messages.map((msg) => (
          <div key={msg.id} className={`chat-bubble ${chatFromRider(msg.sender) ? 'theirs' : 'mine'}`}>
            {msg.photoUrl ? <ChatPhoto src={msg.photoUrl} /> : null}
            {msg.body ? <p>{msg.body}</p> : null}
            <small>{phWhen(msg.sentAtUtc)}</small>
          </div>
        ))}
        <div ref={bottom} />
      </div>
      {canSend ? (
        <form className="chat-compose" onSubmit={(e) => void send(e)}>
          <input ref={camera} type="file" accept="image/*" capture="environment" hidden onChange={(e) => void sendPhoto(e.target.files?.[0])} />
          <input ref={gallery} type="file" accept="image/*" hidden onChange={(e) => void sendPhoto(e.target.files?.[0])} />
          <button className="chat-icon-btn" type="button" disabled={busy} title="Camera" onClick={() => camera.current?.click()}>📷</button>
          <button className="chat-icon-btn" type="button" disabled={busy} title="Photo" onClick={() => gallery.current?.click()}>🖼</button>
          <input
            value={text}
            maxLength={400}
            placeholder="Type a message"
            onChange={(e) => setText(e.target.value)}
          />
          <button className="primary" type="submit" disabled={busy || !text.trim()}>
            {busy ? '…' : 'Send'}
          </button>
        </form>
      ) : (
        <p className="chat-closed muted">Chat closed. History stays on this booking.</p>
      )}
    </div>
  )
}

function ChatPhoto({ src }: { src: string }) {
  const candidates = photoSrcs(src)
  const [attempt, setAttempt] = useState(0)
  const url = candidates[attempt]
  if (!url || attempt >= candidates.length) {
    return <p className="chat-photo-error">Photo could not load. Close and open chat to retry.</p>
  }
  return (
    <img
      className="chat-photo"
      src={url}
      alt="Chat photo"
      loading="lazy"
      onError={() => setAttempt((n) => n + 1)}
    />
  )
}

function photoSrcs(src: string) {
  const urls: string[] = []
  const primary = mediaUrl(src)
  if (primary) urls.push(primary)
  if (src.startsWith('blob:') || src.startsWith('data:')) return urls
  try {
    if (/^https?:\/\//i.test(src)) {
      const path = new URL(src).pathname
      if (path.startsWith('/uploads') && !urls.includes(path)) urls.push(path)
    }
  } catch {
    /* ignore */
  }
  if (src.startsWith('/uploads') && !urls.includes(src)) urls.push(src)
  return urls
}
