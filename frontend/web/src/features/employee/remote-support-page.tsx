import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  isRemoteEndpointReady,
  remoteSupportApi,
  type RemoteEndpoint,
  type RemoteSessionRequest,
} from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { remoteSupportKeys } from '@/features/it/query-keys'
import {
  friendlySessionStatusKey,
  sessionStatusVariant,
} from '@/features/remote-support/employee-remote-helpers'

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-2">
      <span className="text-muted-foreground">{label}</span>
      <span>{value}</span>
    </div>
  )
}

function SessionCard({ session }: { session: RemoteSessionRequest }) {
  const { t } = useTranslation()

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div>
          <CardTitle className="text-base">{session.remoteNumber}</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">{session.reason}</p>
        </div>
        <Badge variant={sessionStatusVariant(session.status)}>
          {t(friendlySessionStatusKey(session.status))}
        </Badge>
      </CardHeader>
      <CardContent className="space-y-2 text-sm">
        <Row
          label={t('remote.fields.technician')}
          value={
            session.technicianDisplayName?.trim()
              ? session.technicianDisplayName
              : t('employee.remote.waitingForIt')
          }
        />
        {session.endpointDeviceName ? (
          <Row label={t('employee.remote.deviceLabel')} value={session.endpointDeviceName} />
        ) : null}
        <Row
          label={t('remote.fields.requestedAt')}
          value={new Date(session.requestedAtUtc).toLocaleString()}
        />
        <Button asChild size="sm" variant="outline">
          <Link to={`/employee/remote-support/${session.id}`}>{t('remote.viewDetail')}</Link>
        </Button>
      </CardContent>
    </Card>
  )
}

function ComputersCard({ endpoints }: { endpoints: RemoteEndpoint[] }) {
  const { t } = useTranslation()
  const ready = endpoints.filter(isRemoteEndpointReady)
  const hasAny = endpoints.length > 0

  return (
    <Card>
      <CardHeader className="space-y-1">
        <CardTitle className="text-base">{t('employee.remote.yourComputers')}</CardTitle>
        <p className="text-sm text-muted-foreground">
          {ready.length > 0
            ? t('employee.remote.readyForSupport')
            : hasAny
              ? t('employee.remote.setupRequired')
              : t('employee.remote.noComputerSetup')}
        </p>
      </CardHeader>
      <CardContent className="space-y-4">
        {hasAny ? (
          <ul className="space-y-3">
            {endpoints.map((endpoint) => {
              const readyDevice = isRemoteEndpointReady(endpoint)
              const osLine = [
                endpoint.operatingSystem,
                endpoint.operatingSystemVersion,
                endpoint.architecture,
              ]
                .filter(Boolean)
                .join(' ')
              return (
                <li key={endpoint.id} className="space-y-1 border-b border-border/60 pb-3 last:border-0 last:pb-0">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="font-medium">
                      <span className="me-2" aria-hidden>
                        {readyDevice ? '●' : endpoint.connectionStatus === 'Offline' ? '○' : '◐'}
                      </span>
                      {endpoint.deviceName}
                    </span>
                    <Badge variant={readyDevice ? 'success' : 'outline'}>
                      {readyDevice
                        ? t('employee.remote.readyForSupport')
                        : endpoint.connectionStatus === 'Offline'
                          ? t('employee.remote.offline')
                          : t('employee.remote.waitingForAgent')}
                    </Badge>
                  </div>
                  {osLine ? <p className="text-xs text-muted-foreground">{osLine}</p> : null}
                  {endpoint.connectionStatus === 'Offline' ? (
                    <p className="text-xs text-muted-foreground">
                      {t('employee.remote.lastSeen')}:{' '}
                      {new Date(endpoint.lastSeenAtUtc).toLocaleString()}
                    </p>
                  ) : null}
                </li>
              )
            })}
          </ul>
        ) : null}

        {ready.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            <Button asChild>
              <Link to="/employee/remote-support/new">{t('employee.remote.getHelp')}</Link>
            </Button>
            <Button asChild variant="outline" size="sm">
              <Link to="/employee/remote-support/setup">{t('employee.remote.manageSetup')}</Link>
            </Button>
          </div>
        ) : (
          <Button asChild>
            <Link to="/employee/remote-support/setup">{t('employee.remote.setupCta')}</Link>
          </Button>
        )}
      </CardContent>
    </Card>
  )
}

export function EmployeeRemoteSupportPage() {
  const { t } = useTranslation()

  const endpointsQuery = useQuery({
    queryKey: remoteSupportKeys.myEndpoints(),
    queryFn: () => remoteSupportApi.listMyEndpoints(),
    refetchInterval: 8_000,
  })

  const sessionsQuery = useQuery({
    queryKey: remoteSupportKeys.mine(''),
    queryFn: () => remoteSupportApi.myList({ pageSize: 50 }),
    refetchInterval: 10_000,
  })

  const sessions = sessionsQuery.data?.items ?? []
  const endpoints = endpointsQuery.data ?? []
  const hasReady = endpoints.some(isRemoteEndpointReady)

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('employee.remote.listTitle')}
        description={t('employee.remote.listHint')}
        actions={
          hasReady ? (
            <Button asChild>
              <Link to="/employee/remote-support/new">{t('employee.remote.getHelp')}</Link>
            </Button>
          ) : (
            <Button asChild>
              <Link to="/employee/remote-support/setup">{t('employee.remote.setupCta')}</Link>
            </Button>
          )
        }
      />

      {endpointsQuery.isLoading ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        <ComputersCard endpoints={endpoints} />
      )}

      <div className="space-y-3">
        <h2 className="text-sm font-semibold text-foreground">
          {t('employee.remote.sessionsTitle')}
        </h2>
        {sessionsQuery.isLoading ? (
          <div className="space-y-3">
            <Skeleton className="h-28 w-full" />
            <Skeleton className="h-28 w-full" />
          </div>
        ) : sessions.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('remote.myEmpty')}</p>
        ) : (
          <div className="grid gap-4 md:grid-cols-2">
            {sessions.map((session) => (
              <SessionCard key={session.id} session={session} />
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
