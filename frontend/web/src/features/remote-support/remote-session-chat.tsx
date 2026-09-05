import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError, remoteSupportApi, type RemoteSessionMessage } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { remoteSupportKeys } from '@/features/it/query-keys'
import { cn } from '@/lib/utils'
import { realtimeStatusLabelKey, type RealtimeStatus } from '@/realtime/status'
import { subscribeToRemoteSessionChat } from '@/realtime/remote-support-hub'

const FALLBACK_POLL_MS = 8_000
const BOTTOM_THRESHOLD_PX = 64
const CREDENTIAL_PATTERN = /(password|passcode|pass\s?word|pwd|otp|credential|كلمة\s*المرور|كلمة\s*السر)/i

function formatTimestamp(value: string) {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString()
}

function SystemMessage({ message }: { message: RemoteSessionMessage }) {
  const { t } = useTranslation()
  const text = message.systemEventKey
    ? t(`remote.chat.system.${message.systemEventKey}`, { defaultValue: message.messageText })
    : message.messageText

  return (
    <li className="flex items-center gap-3 py-2 text-xs text-muted-foreground">
      <span className="h-px flex-1 bg-border" />
      <span className="text-center">
        {text} · {formatTimestamp(message.sentAtUtc)}
      </span>
      <span className="h-px flex-1 bg-border" />
    </li>
  )
}

function ChatBubble({ message, isSelf }: { message: RemoteSessionMessage; isSelf: boolean }) {
  const { t } = useTranslation()

  return (
    <li className={cn('flex flex-col gap-1', isSelf ? 'items-end' : 'items-start')}>
      <div
        className={cn(
          'max-w-[85%] whitespace-pre-wrap break-words rounded-lg px-3 py-2 text-sm',
          isSelf
            ? 'bg-primary text-primary-foreground'
            : 'bg-muted text-foreground',
        )}
      >
        {message.messageText}
      </div>
      <span className="text-xs text-muted-foreground">
        {isSelf ? t('remote.chat.you') : t('remote.chat.otherParty')} ·{' '}
        {formatTimestamp(message.sentAtUtc)}
      </span>
    </li>
  )
}

type RemoteSessionChatProps = {
  sessionId: string
  currentUserId: string | null
  /** Set false when the session is archived and posting is no longer meaningful. */
  canPost?: boolean
  closedHint?: string | null
  className?: string
}

export function RemoteSessionChat({
  sessionId,
  currentUserId,
  canPost = true,
  closedHint = null,
  className,
}: RemoteSessionChatProps) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState('')
  const [status, setStatus] = useState<RealtimeStatus>('connecting')
  const [sendError, setSendError] = useState<string | null>(null)
  const scrollRef = useRef<HTMLDivElement | null>(null)
  const stickToBottomRef = useRef(true)

  const messagesKey = useMemo(() => remoteSupportKeys.messages(sessionId), [sessionId])

  const messagesQuery = useQuery({
    queryKey: messagesKey,
    queryFn: () => remoteSupportApi.listMessages(sessionId),
    enabled: Boolean(sessionId),
    refetchInterval: status === 'polling' ? FALLBACK_POLL_MS : false,
  })

  const appendMessage = useCallback(
    (incoming: RemoteSessionMessage) => {
      queryClient.setQueryData<RemoteSessionMessage[]>(messagesKey, (current) => {
        const existing = current ?? []
        if (existing.some((item) => item.id === incoming.id)) return existing
        return [...existing, incoming].sort(
          (a, b) => Date.parse(a.sentAtUtc) - Date.parse(b.sentAtUtc),
        )
      })
    },
    [queryClient, messagesKey],
  )

  useEffect(() => {
    if (!sessionId) return
    let cancelled = false
    let subscription: { stop: () => Promise<void> } | null = null

    void subscribeToRemoteSessionChat({
      sessionId,
      onMessage: appendMessage,
      onResync: () => {
        void queryClient.invalidateQueries({ queryKey: messagesKey })
      },
    })
      .then((created) => {
        if (cancelled) {
          void created.stop()
          return
        }
        subscription = created
        setStatus('live')
      })
      .catch(() => {
        if (!cancelled) setStatus('polling')
      })

    return () => {
      cancelled = true
      void subscription?.stop()
    }
  }, [sessionId, appendMessage, queryClient, messagesKey])

  const messages = messagesQuery.data ?? []

  useEffect(() => {
    const container = scrollRef.current
    if (!container || !stickToBottomRef.current) return
    container.scrollTop = container.scrollHeight
  }, [messages.length])

  const handleScroll = () => {
    const container = scrollRef.current
    if (!container) return
    const distanceFromBottom =
      container.scrollHeight - container.scrollTop - container.clientHeight
    stickToBottomRef.current = distanceFromBottom <= BOTTOM_THRESHOLD_PX
  }

  const sendMutation = useMutation({
    mutationFn: (text: string) => remoteSupportApi.postMessage(sessionId, text),
    onSuccess: (created) => {
      setSendError(null)
      setDraft('')
      stickToBottomRef.current = true
      appendMessage(created)
    },
    onError: (error) => {
      setSendError(error instanceof ApiError ? error.message : t('remote.chat.sendFailed'))
    },
  })

  const submit = () => {
    const text = draft.trim()
    if (!text || sendMutation.isPending) return
    sendMutation.mutate(text)
  }

  const showCredentialWarning = CREDENTIAL_PATTERN.test(draft)

  return (
    <Card className={className}>
      <CardHeader className="flex flex-row items-center justify-between gap-3 space-y-0">
        <CardTitle className="text-base">{t('remote.chat.title')}</CardTitle>
        <Badge variant={status === 'live' ? 'success' : status === 'polling' ? 'warning' : 'outline'}>
          {t(realtimeStatusLabelKey(status))}
        </Badge>
      </CardHeader>
      <CardContent className="space-y-3">
        <div
          ref={scrollRef}
          onScroll={handleScroll}
          className="h-72 overflow-y-auto rounded-md border border-border/60 p-3"
        >
          {messagesQuery.isLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-10 w-2/3" />
              <Skeleton className="h-10 w-1/2" />
            </div>
          ) : messages.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('remote.chat.empty')}</p>
          ) : (
            <ul className="space-y-3">
              {messages.map((message) =>
                message.messageType === 'System' ? (
                  <SystemMessage key={message.id} message={message} />
                ) : (
                  <ChatBubble
                    key={message.id}
                    message={message}
                    isSelf={Boolean(currentUserId) && message.senderUserId === currentUserId}
                  />
                ),
              )}
            </ul>
          )}
        </div>

        {canPost ? (
          <div className="space-y-2">
            <Textarea
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && !event.shiftKey) {
                  event.preventDefault()
                  submit()
                }
              }}
              placeholder={t('remote.chat.placeholder')}
              rows={3}
              className="min-h-16"
            />
            <p
              className={cn(
                'text-xs',
                showCredentialWarning ? 'font-medium text-destructive' : 'text-muted-foreground',
              )}
            >
              {t('remote.chat.passwordWarning')}
            </p>
            {sendError ? <p className="text-sm text-destructive">{sendError}</p> : null}
            <div className="flex items-center justify-between gap-3">
              <span className="text-xs text-muted-foreground">{t('remote.chat.sendHint')}</span>
              <Button
                type="button"
                size="sm"
                onClick={submit}
                disabled={!draft.trim() || sendMutation.isPending}
              >
                {t('remote.chat.send')}
              </Button>
            </div>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">{closedHint ?? t('remote.chat.closed')}</p>
        )}
      </CardContent>
    </Card>
  )
}
