import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ApiError, isRemoteEndpointReady, remoteSupportApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { remoteSupportKeys } from '@/features/it/query-keys'
import { RemoteSessionChat } from '@/features/remote-support/remote-session-chat'
import { RemoteDeviceCard } from '@/features/remote-support/remote-device-card'
import { isChatOpen } from '@/features/remote-support/chat-window'
import { useEffect, useState } from 'react'

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-2 text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="max-w-[70%] break-all text-end">{value}</span>
    </div>
  )
}

function formatTime(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString() : '—'
}

export function RemoteSupportDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can, user } = useAuth()
  const queryClient = useQueryClient()
  const [endReason, setEndReason] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const sessionQuery = useQuery({
    queryKey: remoteSupportKeys.detail(id),
    queryFn: () => remoteSupportApi.getSession(id),
    enabled: Boolean(id),
  })

  const readinessQuery = useQuery({
    queryKey: remoteSupportKeys.readiness(),
    queryFn: () => remoteSupportApi.readiness(),
  })

  const endpointsQuery = useQuery({
    queryKey: [...remoteSupportKeys.detail(id), 'endpoints'],
    queryFn: () => remoteSupportApi.listSessionEndpoints(id),
    enabled: Boolean(id),
    refetchInterval: 5_000,
  })

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.detail(id) })
    await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.all })
  }

  const startMutation = useMutation({
    mutationFn: () => remoteSupportApi.start(id),
    onSuccess: async (started) => {
      setFormError(null)
      await invalidate()
      if (started.engineJoinUrl) {
        window.open(started.engineJoinUrl, '_blank', 'noopener,noreferrer')
      }
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  const endMutation = useMutation({
    mutationFn: () => remoteSupportApi.end(id, endReason.trim() || null),
    onSuccess: async () => {
      setFormError(null)
      setEndReason('')
      await invalidate()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  const takeMutation = useMutation({
    mutationFn: () => remoteSupportApi.takeSession(id),
    onSuccess: invalidate,
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  const requestAccessMutation = useMutation({
    mutationFn: () => remoteSupportApi.requestAccess(id),
    onSuccess: invalidate,
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  const selectEndpointMutation = useMutation({
    mutationFn: (endpointId: string) => remoteSupportApi.selectEndpoint(id, endpointId),
    onSuccess: async () => {
      setFormError(null)
      await invalidate()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })
  const {
    mutate: selectEndpoint,
    isPending: isSelectingEndpoint,
  } = selectEndpointMutation

  const endpoints = endpointsQuery.data ?? []
  const readyEndpoints = endpoints.filter(isRemoteEndpointReady)
  const soleReadyEndpointId = readyEndpoints.length === 1 ? readyEndpoints[0].id : null
  const selectedEndpointId = sessionQuery.data?.remoteEndpointId

  useEffect(() => {
    if (
      !selectedEndpointId &&
      soleReadyEndpointId &&
      !isSelectingEndpoint
    ) {
      selectEndpoint(soleReadyEndpointId)
    }
  }, [
    isSelectingEndpoint,
    selectEndpoint,
    selectedEndpointId,
    soleReadyEndpointId,
  ])

  if (sessionQuery.isLoading) {
    return <Skeleton className="h-40 w-full" />
  }

  const session = sessionQuery.data
  if (!session) {
    return <p className="text-sm text-muted-foreground">{t('remote.notFound')}</p>
  }

  const endpoint = endpoints.find((item) => item.id === session.remoteEndpointId) ?? null
  const selectedEndpointReady = isRemoteEndpointReady(endpoint)
  const canStart =
    session.status === 'Allowed' && selectedEndpointReady && can('remote.attended')
  const canEnd = session.status === 'InSession' && can('remote.attended')
  const canRequestAccess =
    session.status === 'Requested' &&
    Boolean(session.technicianUserId) &&
    can('remote.attended')
  const readiness = readinessQuery.data

  return (
    <div className="space-y-6">
      <PageHeader
        title={session.remoteNumber}
        description={session.reason}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/remote-support">{t('remote.back')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">
          {t(`remote.status.${session.status}`, { defaultValue: session.status })}
        </Badge>
        <Badge variant="outline">{session.sessionType}</Badge>
        {readiness ? (
          <Badge variant="outline">
            {t('remote.engineStatus')}: {readiness.status}
          </Badge>
        ) : null}
      </div>

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('remote.device.select')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {endpointsQuery.isLoading ? (
            <Skeleton className="h-20 w-full" />
          ) : endpoints.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('remote.device.waitingForAgent')}</p>
          ) : (
            endpoints.map((item) => (
              <label
                key={item.id}
                className="flex cursor-pointer items-center gap-3 rounded-md border p-3"
              >
                <input
                  type="radio"
                  name="remote-endpoint"
                  value={item.id}
                  checked={session.remoteEndpointId === item.id}
                  onChange={() => selectEndpoint(item.id)}
                  disabled={isSelectingEndpoint}
                />
                <span className="flex-1">
                  <span className="block font-medium">{item.deviceName}</span>
                  <span className="text-sm text-muted-foreground">
                    {t(`remote.device.connection.${item.connectionStatus}`, {
                      defaultValue: item.connectionStatus,
                    })}
                  </span>
                </span>
                <Badge variant={isRemoteEndpointReady(item) ? 'success' : 'outline'}>
                  {isRemoteEndpointReady(item)
                    ? t('remote.device.ready')
                    : t('remote.device.waiting')}
                </Badge>
              </label>
            ))
          )}
        </CardContent>
      </Card>

      {endpoint ? <RemoteDeviceCard endpoint={endpoint} variant="technician" /> : null}

      {readiness && !readiness.configured ? (
        <p className="text-sm font-medium text-destructive">
          {t('remote.engineNotConfigured')}
        </p>
      ) : endpoint && !selectedEndpointReady ? (
        <p className="text-sm text-muted-foreground">{t('remote.device.waitingForAgent')}</p>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('remote.sections.context')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <DetailRow
              label={t('remote.fields.configurationItem')}
              value={session.configurationItemId ?? '—'}
            />
            {can('remote.admin') && session.configurationItemId ? (
              <Button asChild size="sm" variant="ghost" className="h-auto p-0">
                <Link to={`/it/cmdb?ci=${session.configurationItemId}`}>
                  {t('remote.openCiMapping')}
                </Link>
              </Button>
            ) : null}
            {session.ticketId ? (
              <DetailRow label={t('remote.fields.ticket')} value={session.ticketId} />
            ) : null}
            {session.ticketId && can('tickets.read') ? (
              <Button asChild size="sm" variant="ghost" className="h-auto p-0">
                <Link to={`/it/tickets/${session.ticketId}`}>{t('remote.openTicket')}</Link>
              </Button>
            ) : null}
            {session.changeRequestId ? (
              <DetailRow label={t('remote.fields.change')} value={session.changeRequestId} />
            ) : null}
            {session.changeRequestId && can('change.read') ? (
              <Button asChild size="sm" variant="ghost" className="h-auto p-0">
                <Link to={`/it/changes/${session.changeRequestId}`}>{t('remote.openChange')}</Link>
              </Button>
            ) : null}
            <DetailRow
              label={t('remote.fields.requestedBy')}
              value={session.requestedByUserId.slice(0, 8)}
            />
            <DetailRow
              label={t('remote.fields.targetUser')}
              value={session.targetUserId?.slice(0, 8) ?? '—'}
            />
            <DetailRow
              label={t('remote.fields.technician')}
              value={session.technicianUserId?.slice(0, 8) ?? '—'}
            />
            <DetailRow
              label={t('remote.fields.privileges')}
              value={session.requestedPrivileges ?? '—'}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('remote.sections.consent')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <DetailRow label={t('remote.fields.consentUser')} value={session.consentUserId?.slice(0, 8) ?? '—'} />
            <DetailRow label={t('remote.fields.consentIp')} value={session.consentIpAddress ?? '—'} />
            <DetailRow label={t('remote.fields.expiresAt')} value={formatTime(session.expiresAtUtc)} />
            <DetailRow label={t('remote.fields.allowedAt')} value={formatTime(session.allowedAtUtc)} />
            <DetailRow label={t('remote.fields.declinedAt')} value={formatTime(session.declinedAtUtc)} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('remote.sections.session')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <DetailRow label={t('remote.fields.requestedAt')} value={formatTime(session.requestedAtUtc)} />
            <DetailRow label={t('remote.fields.connectingAt')} value={formatTime(session.connectingAtUtc)} />
            <DetailRow label={t('remote.fields.startedAt')} value={formatTime(session.startedAtUtc)} />
            <DetailRow label={t('remote.fields.endedAt')} value={formatTime(session.endedAtUtc)} />
            <DetailRow label={t('remote.fields.duration')} value={session.durationSeconds != null ? `${session.durationSeconds}s` : '—'} />
            <DetailRow label={t('remote.fields.outcome')} value={session.outcome ?? '—'} />
            <DetailRow label={t('remote.fields.endReason')} value={session.endReason ?? '—'} />
            <DetailRow
              label={t('remote.fields.elevationUsed')}
              value={
                session.elevationUsed == null
                  ? '—'
                  : session.elevationUsed
                    ? t('remote.yes')
                    : t('remote.no')
              }
            />
            <DetailRow label={t('remote.fields.engineSession')} value={session.engineSessionId ?? '—'} />
            {session.lastEngineError ? (
              <p className="text-xs text-destructive">{session.lastEngineError}</p>
            ) : null}
            {session.engineJoinUrl ? (
              <div className="space-y-1 pt-2">
                <Label>{t('remote.fields.joinUrl')}</Label>
                <a
                  href={session.engineJoinUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="break-all text-sm text-primary hover:underline"
                >
                  {session.engineJoinUrl}
                </a>
              </div>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <RemoteSessionChat
        sessionId={session.id}
        currentUserId={user?.id ?? null}
        canPost={isChatOpen(session)}
      />

      {(!session.technicianUserId || canRequestAccess || canStart || canEnd) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('remote.actions.title')}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap items-end gap-3">
            {!session.technicianUserId ? (
              <Button
                type="button"
                onClick={() => takeMutation.mutate()}
                disabled={takeMutation.isPending}
              >
                {t('remote.actions.take')}
              </Button>
            ) : null}
            {canRequestAccess ? (
              <Button
                type="button"
                onClick={() => requestAccessMutation.mutate()}
                disabled={requestAccessMutation.isPending || !selectedEndpointReady}
              >
                {t('remote.actions.requestAccess')}
              </Button>
            ) : null}
            {canStart ? (
              <Button type="button" onClick={() => startMutation.mutate()} disabled={startMutation.isPending}>
                {t('remote.actions.openSession')}
              </Button>
            ) : null}
            {canEnd ? (
              <>
                <div className="space-y-2">
                  <Label>{t('remote.fields.endReasonOptional')}</Label>
                  <Input value={endReason} onChange={(event) => setEndReason(event.target.value)} className="max-w-sm" />
                </div>
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => endMutation.mutate()}
                  disabled={endMutation.isPending}
                >
                  {t('remote.actions.end')}
                </Button>
              </>
            ) : null}
          </CardContent>
        </Card>
      )}
    </div>
  )
}
