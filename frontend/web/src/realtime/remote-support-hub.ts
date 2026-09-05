import type { HubConnection } from '@microsoft/signalr'
import type { RemoteSessionMessage } from '@/api/client'

const HUB_PATH = '/hubs/remote-support'
const CHAT_EVENT = 'remoteChatMessage'

/** The hub protocol may serialize DTOs as camelCase or PascalCase depending on host options. */
function readString(raw: Record<string, unknown>, name: string): string | null {
  const camel = raw[name]
  if (typeof camel === 'string') return camel
  const pascal = raw[name.charAt(0).toUpperCase() + name.slice(1)]
  return typeof pascal === 'string' ? pascal : null
}

export function normalizeChatMessage(payload: unknown): RemoteSessionMessage | null {
  if (typeof payload !== 'object' || payload === null) return null
  const raw = payload as Record<string, unknown>
  const id = readString(raw, 'id')
  const sessionId = readString(raw, 'remoteSessionRequestId')
  const sentAtUtc = readString(raw, 'sentAtUtc')
  if (!id || !sessionId || !sentAtUtc) return null

  return {
    id,
    remoteSessionRequestId: sessionId,
    senderUserId: readString(raw, 'senderUserId'),
    messageText: readString(raw, 'messageText') ?? '',
    messageType: readString(raw, 'messageType') === 'System' ? 'System' : 'User',
    systemEventKey: readString(raw, 'systemEventKey'),
    sentAtUtc,
  }
}

export type RemoteSessionChatSubscription = {
  stop: () => Promise<void>
}

type SubscribeOptions = {
  sessionId: string
  onMessage: (message: RemoteSessionMessage) => void
  /** Called after a reconnect so callers can reload history and close any gap. */
  onResync?: () => void
}

/**
 * Joins the remote support hub group for a session. Rejects when the hub is
 * unavailable so callers can fall back to REST polling.
 */
export async function subscribeToRemoteSessionChat({
  sessionId,
  onMessage,
  onResync,
}: SubscribeOptions): Promise<RemoteSessionChatSubscription> {
  const signalr = await import('@microsoft/signalr')
  const connection: HubConnection = new signalr.HubConnectionBuilder()
    .withUrl(HUB_PATH)
    .withAutomaticReconnect()
    .configureLogging(signalr.LogLevel.Warning)
    .build()

  connection.on(CHAT_EVENT, (payload: unknown) => {
    const message = normalizeChatMessage(payload)
    if (message) onMessage(message)
  })

  connection.onreconnected(() => {
    void connection
      .invoke('JoinSession', sessionId)
      .then(() => onResync?.())
      .catch(() => undefined)
  })

  try {
    await connection.start()
    await connection.invoke('JoinSession', sessionId)
  } catch (error) {
    await connection.stop().catch(() => undefined)
    throw error
  }

  return {
    stop: async () => {
      await connection.invoke('LeaveSession', sessionId).catch(() => undefined)
      await connection.stop().catch(() => undefined)
    },
  }
}
