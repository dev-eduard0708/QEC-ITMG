import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, eventsApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { eventKeys } from '@/features/it/query-keys'

export function EventDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const eventQuery = useQuery({
    queryKey: eventKeys.detail(id),
    queryFn: () => eventsApi.get(id),
    enabled: Boolean(id),
  })

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: eventKeys.detail(id) }),
      queryClient.invalidateQueries({ queryKey: eventKeys.all }),
    ])
  }

  const onError = (err: unknown) => {
    setError(err instanceof ApiError ? err.message : t('events.error.generic'))
  }

  const ackMutation = useMutation({
    mutationFn: () => eventsApi.acknowledge(id),
    onSuccess: async () => {
      setError(null)
      await refresh()
    },
    onError,
  })

  const closeMutation = useMutation({
    mutationFn: () => eventsApi.close(id),
    onSuccess: async () => {
      setError(null)
      await refresh()
    },
    onError,
  })

  const promoteMutation = useMutation({
    mutationFn: () => eventsApi.promote(id),
    onSuccess: async (result) => {
      setError(null)
      await refresh()
      navigate(`/it/tickets/${result.ticketId}`)
    },
    onError,
  })

  if (eventQuery.isLoading) return <Skeleton className="h-40 w-full" />
  const event = eventQuery.data
  if (!event) return <p className="text-sm text-muted-foreground">{t('events.notFound')}</p>

  const canAct = event.status !== 'Closed' && event.status !== 'Promoted'

  return (
    <div className="space-y-6">
      <PageHeader
        title={event.eventNumber}
        description={event.title}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/events">{t('events.back')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge>{event.status}</Badge>
        <Badge variant="outline">{event.severity}</Badge>
        <Badge variant="secondary">{event.source}</Badge>
        <Badge variant="secondary">×{event.occurrenceCount}</Badge>
      </div>

      <div className="flex flex-wrap gap-2">
        {can('event.acknowledge') && canAct ? (
          <Button type="button" variant="secondary" onClick={() => ackMutation.mutate()}>
            {t('events.actions.acknowledge')}
          </Button>
        ) : null}
        {can('event.promote') && event.status !== 'Closed' && event.status !== 'Promoted' ? (
          <Button type="button" onClick={() => promoteMutation.mutate()}>
            {t('events.actions.promote')}
          </Button>
        ) : null}
        {can('event.acknowledge') && event.status !== 'Closed' ? (
          <Button type="button" variant="outline" onClick={() => closeMutation.mutate()}>
            {t('events.actions.close')}
          </Button>
        ) : null}
        {event.linkedTicketId ? (
          <Button asChild variant="outline">
            <Link to={`/it/tickets/${event.linkedTicketId}`}>{t('events.actions.openTicket')}</Link>
          </Button>
        ) : null}
      </div>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <section className="grid gap-3 rounded-md border border-border p-4 text-sm sm:grid-cols-2">
        <p>
          <span className="text-muted-foreground">{t('events.fields.sourceKey')}: </span>
          <span className="font-mono text-xs">{event.sourceEventKey}</span>
        </p>
        <p>
          <span className="text-muted-foreground">{t('events.columns.lastSeen')}: </span>
          {new Date(event.lastSeenAtUtc).toLocaleString()}
        </p>
        <p>
          <span className="text-muted-foreground">{t('events.fields.firstSeen')}: </span>
          {new Date(event.firstSeenAtUtc).toLocaleString()}
        </p>
        <p>
          <span className="text-muted-foreground">{t('events.fields.ci')}: </span>
          {event.configurationItemId?.slice(0, 8) ?? '—'}
        </p>
        <div className="sm:col-span-2">
          <p className="text-muted-foreground">{t('events.fields.summary')}</p>
          <p className="mt-1 whitespace-pre-wrap">{event.summary}</p>
        </div>
      </section>
    </div>
  )
}
