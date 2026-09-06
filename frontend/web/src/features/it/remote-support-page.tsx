import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link, useSearchParams, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  ApiError,
  adminApi,
  cmdbApi,
  remoteSupportApi,
  type RemoteSessionRequest,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { remoteSupportKeys, cmdbKeys } from '@/features/it/query-keys'
import { cn } from '@/lib/utils'

type TabKey =
  | 'waiting'
  | 'assigned'
  | 'waitingDevice'
  | 'waitingApproval'
  | 'ready'
  | 'connecting'
  | 'inSession'
  | 'completed'

function engineStatusVariant(
  status: string,
): 'default' | 'secondary' | 'success' | 'warning' | 'outline' {
  switch (status) {
    case 'Healthy':
      return 'success'
    case 'Configured':
      return 'secondary'
    case 'Unhealthy':
      return 'warning'
    case 'NotConfigured':
      return 'outline'
    case 'Disabled':
      return 'warning'
    default:
      return 'default'
  }
}

function sessionStatusVariant(status: string): 'default' | 'secondary' | 'success' | 'warning' | 'outline' {
  switch (status) {
    case 'InSession':
      return 'success'
    case 'Connecting':
      return 'warning'
    case 'NotifyUser':
    case 'Requested':
      return 'outline'
    case 'Allowed':
    case 'Authorized':
      return 'secondary'
    case 'Declined':
    case 'Expired':
      return 'warning'
    default:
      return 'default'
  }
}

function filterByTab(tab: TabKey, session: RemoteSessionRequest): boolean {
  const closed = ['Ended', 'Declined', 'Expired'].includes(session.status)
  const deviceReady = session.endpointIsReady === true
  switch (tab) {
    case 'waiting':
      return session.status === 'Requested' && !session.technicianUserId
    case 'assigned':
      return (
        session.status === 'Requested' &&
        Boolean(session.technicianUserId) &&
        deviceReady
      )
    case 'waitingDevice':
      // Assigned (or self-help in progress) but no confirmed ready endpoint yet.
      return (
        !closed &&
        !deviceReady &&
        session.status === 'Requested' &&
        Boolean(session.technicianUserId || session.remoteEndpointId || session.endpointDeviceName)
      )
    case 'waitingApproval':
      return session.status === 'NotifyUser'
    case 'ready':
      return (
        (session.status === 'Allowed' || session.status === 'Authorized') && deviceReady
      )
    case 'connecting':
      return session.status === 'Connecting'
    case 'inSession':
      return session.status === 'InSession'
    case 'completed':
      return closed
    default:
      return false
  }
}

function deviceSummary(session: RemoteSessionRequest, t: (key: string) => string) {
  if (session.endpointIsReady && session.endpointDeviceName) {
    return {
      marker: '●',
      title: session.endpointDeviceName,
      detail: session.endpointOperatingSystem ?? t('remote.device.ready'),
      label: t('remote.queue.deviceReady'),
    }
  }
  if (session.endpointDeviceName) {
    return {
      marker: session.endpointConnectionStatus === 'Offline' ? '○' : '◐',
      title: session.endpointDeviceName,
      detail: session.endpointOperatingSystem ?? session.endpointConnectionStatus ?? '',
      label:
        session.endpointConnectionStatus === 'Offline'
          ? t('employee.remote.offline')
          : t('remote.queue.deviceSetupInProgress'),
    }
  }
  return {
    marker: '○',
    title: t('remote.queue.noReadyComputer'),
    detail: '',
    label: t('remote.queue.deviceSetupInProgress'),
  }
}

function SessionRow({ session }: { session: RemoteSessionRequest }) {
  const { t } = useTranslation()
  const { can, user } = useAuth()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const device = deviceSummary(session, t)
  const isMine = session.technicianUserId === user?.id
  const deviceReady = session.endpointIsReady === true

  const invalidate = () => queryClient.invalidateQueries({ queryKey: remoteSupportKeys.all })

  const takeMutation = useMutation({
    mutationFn: () => remoteSupportApi.takeSession(session.id),
    onSuccess: invalidate,
  })
  const requestAccessMutation = useMutation({
    mutationFn: () => remoteSupportApi.requestAccess(session.id),
    onSuccess: invalidate,
  })
  const connectMutation = useMutation({
    mutationFn: () => remoteSupportApi.start(session.id),
    onSuccess: async (started) => {
      await invalidate()
      if (started.engineJoinUrl) {
        window.open(started.engineJoinUrl, '_blank', 'noopener,noreferrer')
      }
    },
  })

  const openChat = () => navigate(`/it/remote-support/${session.id}#chat`)

  return (
    <li className="space-y-3 border-b border-border/60 py-4 last:border-0">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <Link
              to={`/it/remote-support/${session.id}`}
              className="font-medium text-foreground hover:underline"
            >
              {session.remoteNumber}
            </Link>
            <Badge variant={sessionStatusVariant(session.status)}>
              {t(`remote.status.${session.status}`, { defaultValue: session.status })}
            </Badge>
          </div>
          <p className="text-sm font-medium">
            {session.employeeDisplayName?.trim() || t('remote.queue.unknownEmployee')}
          </p>
          {session.employeeEmail ? (
            <p className="text-xs text-muted-foreground">{session.employeeEmail}</p>
          ) : null}
          <p className="text-sm text-muted-foreground">{session.reason}</p>
          <p className="text-sm">
            <span className="me-2" aria-hidden>
              {device.marker}
            </span>
            {device.title}
            {device.detail ? (
              <span className="ms-2 text-muted-foreground">{device.detail}</span>
            ) : null}
            <Badge variant="outline" className="ms-2">
              {device.label}
            </Badge>
          </p>
          <p className="text-xs text-muted-foreground">
            {t('remote.fields.requestedAt')}: {new Date(session.requestedAtUtc).toLocaleString()}
            {' · '}
            {t('remote.fields.technician')}:{' '}
            {session.technicianDisplayName?.trim() ||
              (session.technicianUserId ? t('remote.queue.assigned') : t('remote.queue.unassigned'))}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {!session.technicianUserId ? (
            <Button
              type="button"
              size="sm"
              onClick={() => takeMutation.mutate()}
              disabled={takeMutation.isPending}
            >
              {t('remote.actions.take')}
            </Button>
          ) : null}

          {session.technicianUserId &&
          session.status === 'Requested' &&
          deviceReady &&
          (isMine || can('remote.admin')) &&
          can('remote.attended') ? (
            <Button
              type="button"
              size="sm"
              onClick={() => requestAccessMutation.mutate()}
              disabled={requestAccessMutation.isPending}
            >
              {t('remote.actions.requestAccess')}
            </Button>
          ) : null}

          {session.status === 'NotifyUser' ? (
            <Badge variant="outline">{t('remote.queue.waitingApproval')}</Badge>
          ) : null}

          {(session.status === 'Allowed' || session.status === 'Authorized') &&
          deviceReady &&
          (isMine || can('remote.admin')) &&
          can('remote.attended') ? (
            <Button
              type="button"
              size="sm"
              onClick={() => connectMutation.mutate()}
              disabled={connectMutation.isPending}
            >
              {t('remote.actions.connect')}
            </Button>
          ) : null}

          {session.status === 'InSession' ? (
            <Button asChild size="sm">
              <Link to={`/it/remote-support/${session.id}`}>{t('remote.actions.openSession')}</Link>
            </Button>
          ) : null}

          <Button type="button" size="sm" variant="outline" onClick={openChat}>
            {t('remote.actions.openChat')}
          </Button>
          <Button asChild size="sm" variant="ghost">
            <Link to={`/it/remote-support/${session.id}`}>{t('remote.viewDetail')}</Link>
          </Button>
        </div>
      </div>
    </li>
  )
}

export function RemoteSupportPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()
  const ticketIdFromUrl = searchParams.get('ticketId') ?? ''
  const [activeTab, setActiveTab] = useState<TabKey>('waiting')
  const [configurationItemId, setConfigurationItemId] = useState('')
  const [targetUserId, setTargetUserId] = useState('')
  const [reason, setReason] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const readinessQuery = useQuery({
    queryKey: remoteSupportKeys.readiness(),
    queryFn: () => remoteSupportApi.readiness(),
  })

  const listQuery = useQuery({
    queryKey: remoteSupportKeys.list(ticketIdFromUrl),
    queryFn: () =>
      remoteSupportApi.listSessions({
        pageSize: 100,
        ticketId: ticketIdFromUrl || undefined,
      }),
    enabled:
      can('remote.request') ||
      can('remote.audit.read') ||
      can('remote.attended') ||
      can('remote.admin'),
    refetchInterval: 8_000,
  })

  const cisQuery = useQuery({
    queryKey: cmdbKeys.cis('remote-create'),
    queryFn: () => cmdbApi.listCis(),
    enabled: can('remote.request'),
  })

  const usersQuery = useQuery({
    queryKey: ['admin', 'users', 'remote-create'],
    queryFn: () => adminApi.listUsers(),
    enabled: can('remote.request') && can('admin.users'),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      remoteSupportApi.createAttended({
        configurationItemId,
        targetUserId,
        reason: reason.trim(),
        ticketId: ticketIdFromUrl || null,
      }),
    onSuccess: async (created) => {
      setReason('')
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.all })
      setActiveTab('assigned')
      navigate(`/it/remote-support/${created.id}`)
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  const tabCounts = useMemo(() => {
    const items = listQuery.data?.items ?? []
    return {
      waiting: items.filter((s) => filterByTab('waiting', s)).length,
      assigned: items.filter((s) => filterByTab('assigned', s)).length,
      waitingDevice: items.filter((s) => filterByTab('waitingDevice', s)).length,
      waitingApproval: items.filter((s) => filterByTab('waitingApproval', s)).length,
      ready: items.filter((s) => filterByTab('ready', s)).length,
      connecting: items.filter((s) => filterByTab('connecting', s)).length,
      inSession: items.filter((s) => filterByTab('inSession', s)).length,
      completed: items.filter((s) => filterByTab('completed', s)).length,
    }
  }, [listQuery.data?.items])

  const filteredSessions = useMemo(
    () => (listQuery.data?.items ?? []).filter((s) => filterByTab(activeTab, s)),
    [listQuery.data?.items, activeTab],
  )

  const readiness = readinessQuery.data
  const tabs: { key: TabKey; label: string }[] = [
    { key: 'waiting', label: t('remote.tabs.waiting') },
    { key: 'assigned', label: t('remote.tabs.assigned') },
    { key: 'waitingDevice', label: t('remote.tabs.waitingDevice') },
    { key: 'waitingApproval', label: t('remote.tabs.waitingApproval') },
    { key: 'ready', label: t('remote.tabs.readyConnect') },
    { key: 'connecting', label: t('remote.tabs.connecting') },
    { key: 'inSession', label: t('remote.tabs.inSession') },
    { key: 'completed', label: t('remote.tabs.completed') },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('remote.title')}
        description={t('remote.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/remote-support/endpoints">{t('remote.endpoints.title')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        {readinessQuery.isLoading ? (
          <Skeleton className="h-6 w-32" />
        ) : readiness ? (
          <>
            <span className="text-sm text-muted-foreground">{t('remote.engine.provider')}</span>
            <Badge variant="outline">{readiness.providerKind || '—'}</Badge>
            <span className="text-sm text-muted-foreground">{t('remote.engine.configured')}</span>
            <Badge variant={readiness.configured ? 'success' : 'warning'}>
              {readiness.configured ? t('remote.yes') : t('remote.no')}
            </Badge>
            <span className="text-sm text-muted-foreground">{t('remote.engine.health')}</span>
            <Badge variant={engineStatusVariant(readiness.status)}>
              {t(`remote.engine.${readiness.status}`, { defaultValue: readiness.status })}
            </Badge>
            {readiness.agentEnrollmentAvailable != null ? (
              <>
                <span className="text-sm text-muted-foreground">
                  {t('remote.engine.agentEnrollment')}
                </span>
                <Badge variant={readiness.agentEnrollmentAvailable ? 'success' : 'warning'}>
                  {readiness.agentEnrollmentAvailable ? t('remote.yes') : t('remote.no')}
                </Badge>
              </>
            ) : null}
            {readiness.sessionCreationAvailable != null ? (
              <>
                <span className="text-sm text-muted-foreground">
                  {t('remote.engine.sessionCreation')}
                </span>
                <Badge variant={readiness.sessionCreationAvailable ? 'success' : 'warning'}>
                  {readiness.sessionCreationAvailable ? t('remote.yes') : t('remote.no')}
                </Badge>
              </>
            ) : null}
          </>
        ) : null}
      </div>

      {can('remote.request') ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('remote.createAttended')}</CardTitle>
          </CardHeader>
          <CardContent>
            {ticketIdFromUrl ? (
              <p className="mb-3 text-sm text-muted-foreground">
                {t('remote.linkedTicket')}: {ticketIdFromUrl.slice(0, 8)}…
              </p>
            ) : null}
            <form
              className="grid gap-4 sm:grid-cols-2"
              onSubmit={(event) => {
                event.preventDefault()
                if (!configurationItemId || !targetUserId || !reason.trim()) {
                  setFormError(t('remote.error.required'))
                  return
                }
                createMutation.mutate()
              }}
            >
              <div className="space-y-2">
                <Label>{t('remote.fields.configurationItem')}</Label>
                <Select value={configurationItemId} onValueChange={setConfigurationItemId}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('remote.fields.configurationItemPlaceholder')} />
                  </SelectTrigger>
                  <SelectContent>
                    {(cisQuery.data ?? []).map((ci) => (
                      <SelectItem key={ci.id} value={ci.id}>
                        {ci.ciNumber} — {ci.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label>{t('remote.fields.targetUser')}</Label>
                {usersQuery.data ? (
                  <Select value={targetUserId} onValueChange={setTargetUserId}>
                    <SelectTrigger>
                      <SelectValue placeholder={t('remote.fields.targetUserPlaceholder')} />
                    </SelectTrigger>
                    <SelectContent>
                      {usersQuery.data.map((user) => (
                        <SelectItem key={user.id} value={user.id}>
                          {user.displayName} ({user.upn})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <Input
                    value={targetUserId}
                    onChange={(event) => setTargetUserId(event.target.value)}
                    placeholder={t('remote.fields.targetUserIdPlaceholder')}
                  />
                )}
              </div>
              <div className="space-y-2 sm:col-span-2">
                <Label>{t('remote.fields.reason')}</Label>
                <Input value={reason} onChange={(event) => setReason(event.target.value)} />
              </div>
              {formError ? (
                <p className="text-sm text-destructive sm:col-span-2">{formError}</p>
              ) : null}
              <div className="sm:col-span-2">
                <Button type="submit" disabled={createMutation.isPending}>
                  {t('remote.createRequest')}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      ) : null}

      <div className="flex flex-wrap gap-2">
        {tabs.map((tab) => (
          <Button
            key={tab.key}
            type="button"
            size="sm"
            variant={activeTab === tab.key ? 'default' : 'outline'}
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
            <span className={cn('ms-1.5 text-xs opacity-80')}>({tabCounts[tab.key]})</span>
          </Button>
        ))}
      </div>

      <Card>
        <CardContent className="pt-6">
          {listQuery.isLoading ? (
            <Skeleton className="h-24 w-full" />
          ) : filteredSessions.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('remote.empty')}</p>
          ) : (
            <ul>
              {filteredSessions.map((session) => (
                <SessionRow key={session.id} session={session} />
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  )
}