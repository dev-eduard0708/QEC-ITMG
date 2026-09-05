import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  remoteSupportApi,
  type EmployeeRemoteOnboarding,
  type RemoteSessionRequest,
} from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { remoteSupportKeys } from '@/features/it/query-keys'
import {
  deviceReadinessVariant,
  friendlySessionStatusKey,
  overallStatusVariant,
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

function ReadinessCard({ onboarding }: { onboarding: EmployeeRemoteOnboarding }) {
  const { t } = useTranslation()
  const readyCount = onboarding.devices.filter((device) => device.remoteReady).length
  const isReady = onboarding.overallStatus === 'Ready'

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div>
          <CardTitle className="text-base">{t('employee.remote.readinessTitle')}</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">
            {t(`employee.remote.overall.${onboarding.overallStatus}`)}
          </p>
        </div>
        <Badge variant={overallStatusVariant(onboarding.overallStatus)}>
          {t(`employee.remote.overallBadge.${onboarding.overallStatus}`)}
        </Badge>
      </CardHeader>
      <CardContent className="space-y-4">
        {onboarding.devices.length > 0 ? (
          <>
            <p className="text-sm text-muted-foreground">
              {t('employee.remote.readyDeviceCount', {
                ready: readyCount,
                total: onboarding.devices.length,
              })}
            </p>
            <ul className="space-y-2">
              {onboarding.devices.map((device) => (
                <li
                  key={device.assetId}
                  className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 pb-2 text-sm last:border-0 last:pb-0"
                >
                  <span className="min-w-0">
                    <span className="font-medium">{device.assetName}</span>
                    <span className="text-muted-foreground"> · {device.assetNumber}</span>
                  </span>
                  <Badge variant={deviceReadinessVariant(device.readinessStatus)}>
                    {t(`employee.remote.readiness.${device.readinessStatus}`)}
                  </Badge>
                </li>
              ))}
            </ul>
          </>
        ) : (
          <p className="text-sm text-muted-foreground">{t('employee.remote.noDevices')}</p>
        )}

        {!isReady ? (
          <Button asChild>
            <Link to="/employee/remote-support/setup">{t('employee.remote.setupCta')}</Link>
          </Button>
        ) : (
          <Button asChild variant="outline" size="sm">
            <Link to="/employee/remote-support/setup">{t('employee.remote.setupReview')}</Link>
          </Button>
        )}
      </CardContent>
    </Card>
  )
}

export function EmployeeRemoteSupportPage() {
  const { t } = useTranslation()

  const onboardingQuery = useQuery({
    queryKey: remoteSupportKeys.onboarding(),
    queryFn: () => remoteSupportApi.onboarding(),
  })

  const sessionsQuery = useQuery({
    queryKey: remoteSupportKeys.mine(''),
    queryFn: () => remoteSupportApi.myList({ pageSize: 50 }),
  })

  const sessions = sessionsQuery.data?.items ?? []

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('employee.remote.listTitle')}
        description={t('employee.remote.listHint')}
      />

      {onboardingQuery.isLoading ? (
        <Skeleton className="h-48 w-full" />
      ) : onboardingQuery.data ? (
        <ReadinessCard onboarding={onboardingQuery.data} />
      ) : (
        <p className="text-sm text-muted-foreground">{t('employee.remote.readinessUnavailable')}</p>
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
