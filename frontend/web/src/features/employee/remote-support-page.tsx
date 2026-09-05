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
        <Row label={t('remote.fields.technician')} value={t('employee.remote.itTechnician')} />
        <Row
          label={t('remote.fields.requestedAt')}
          value={new Date(session.requestedAtUtc).toLocaleString()}
        />
        {session.expiresAtUtc ? (
          <Row
            label={t('remote.fields.expiresAt')}
            value={new Date(session.expiresAtUtc).toLocaleString()}
          />
        ) : null}
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
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div>
          <CardTitle className="text-base">{t('employee.remote.yourComputers')}</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">
            {ready.length > 0
              ? t('employee.remote.readyForSupport')
              : hasAny
                ? t('employee.remote.setupRequired')
                : t('employee.remote.noComputerSetup')}
          </p>
        </div>
        <Badge variant={ready.length > 0 ? 'success' : 'outline'}>
          {ready.length > 0
            ? t('employee.remote.overallBadge.Ready')
            : t('employee.remote.overallBadge.SetupRequired')}
        </Badge>
      </CardHeader>
      <CardContent className="space-y-4">
        {hasAny ? (
          <ul className="space-y-2">
            {endpoints.map((endpoint) => (
              <li
                key={endpoint.id}
                className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 pb-2 text-sm last:border-0 last:pb-0"
              >
                <span className="min-w-0 font-medium">
                  <span className="me-2" aria-hidden>
                    {isRemoteEndpointReady(endpoint) ? '●' : '○'}
                  </span>
                  {endpoint.deviceName}
                </span>
                <Badge variant={isRemoteEndpointReady(endpoint) ? 'success' : 'outline'}>
                  {isRemoteEndpointReady(endpoint)
                    ? t('employee.remote.readyForSupport')
                    : endpoint.connectionStatus === 'Offline'
                      ? t('employee.remote.offline')
                      : t('employee.remote.waitingForAgent')}
                </Badge>
              </li>
            ))}
          </ul>
        ) : null}

        {ready.length > 0 ? (
          <div className="flex flex-wrap gap-2">
            <Button asChild>
              <Link to="/employee/remote-support/new">{t('employee.remote.getHelp')}</Link>
            </Button>
            <Button asChild variant="outline" size="sm">
              <Link to="/employee/remote-support/setup">{t('employee.remote.setupReview')}</Link>
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
    refetchInterval: 10_000,
  })

  const sessionsQuery = useQuery({
    queryKey: remoteSupportKeys.mine(''),
    queryFn: () => remoteSupportApi.myList({ pageSize: 50 }),
  })

  const sessions = sessionsQuery.data?.items ?? []
  const endpoints = endpointsQuery.data ?? []

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('employee.remote.listTitle')}
        description={t('employee.remote.listHint')}
        actions={
          <Button asChild>
            <Link to="/employee/remote-support/new">{t('employee.remote.getHelp')}</Link>
          </Button>
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
