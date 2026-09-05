import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { remoteSupportApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { remoteSupportKeys } from '@/features/it/query-keys'
import {
  deviceReadinessVariant,
  overallStatusVariant,
} from '@/features/remote-support/employee-remote-helpers'

export function EmployeeRemoteSupportSetupPage() {
  const { t } = useTranslation()

  const onboardingQuery = useQuery({
    queryKey: remoteSupportKeys.onboarding(),
    queryFn: () => remoteSupportApi.onboarding(),
  })

  const backButton = (
    <Button asChild variant="outline">
      <Link to="/employee/remote-support">{t('remote.back')}</Link>
    </Button>
  )

  if (onboardingQuery.isLoading) {
    return (
      <div className="space-y-6">
        <PageHeader
          title={t('employee.remote.setupTitle')}
          description={t('employee.remote.setupHint')}
          actions={backButton}
        />
        <Skeleton className="h-48 w-full" />
      </div>
    )
  }

  const onboarding = onboardingQuery.data
  if (!onboarding) {
    return (
      <div className="space-y-6">
        <PageHeader
          title={t('employee.remote.setupTitle')}
          description={t('employee.remote.setupHint')}
          actions={backButton}
        />
        <p className="text-sm text-muted-foreground">{t('employee.remote.readinessUnavailable')}</p>
      </div>
    )
  }

  const waitingForIt = onboarding.devices.some(
    (device) => device.readinessStatus === 'WaitingForIt' || (device.hasEngineMapping && !device.remoteReady),
  )
  const needsItRegistration = onboarding.devices.some(
    (device) => device.readinessStatus === 'DeviceNotLinked',
  )

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={t('employee.remote.setupTitle')}
        description={t('employee.remote.setupHint')}
        actions={backButton}
      />

      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-muted-foreground">{t('employee.remote.readinessTitle')}</span>
        <Badge variant={overallStatusVariant(onboarding.overallStatus)}>
          {t(`employee.remote.overallBadge.${onboarding.overallStatus}`)}
        </Badge>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('employee.remote.setupStep1')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {onboarding.agentDownloadConfigured && onboarding.agentDownloadUrl ? (
            <>
              <p className="text-sm text-muted-foreground">{t('employee.remote.oneTimeInstall')}</p>
              <Button asChild>
                <a
                  href={onboarding.agentDownloadUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  {t('employee.remote.downloadHelper')}
                </a>
              </Button>
              <p className="text-xs text-muted-foreground">
                {t('employee.remote.downloadAdminHint')}
              </p>
            </>
          ) : (
            <p className="text-sm text-muted-foreground">{t('employee.remote.notConfiguredYet')}</p>
          )}
        </CardContent>
      </Card>

      {onboarding.agentInstallInstructions ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('employee.remote.instructionsTitle')}</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm text-muted-foreground">
              {onboarding.agentInstallInstructions}
            </p>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('employee.remote.setupStep2')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {onboarding.devices.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('employee.remote.noDevices')}</p>
          ) : (
            <ul className="space-y-3">
              {onboarding.devices.map((device) => (
                <li key={device.assetId} className="space-y-1 border-b border-border/60 pb-3 last:border-0 last:pb-0">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="text-sm font-medium">{device.assetName}</span>
                    <Badge variant={deviceReadinessVariant(device.readinessStatus)}>
                      {t(`employee.remote.readiness.${device.readinessStatus}`)}
                    </Badge>
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {device.assetNumber}
                    {device.configurationItemNumber ? ` · ${device.configurationItemNumber}` : ''}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {t(`employee.remote.readinessHelp.${device.readinessStatus}`)}
                  </p>
                </li>
              ))}
            </ul>
          )}

          {waitingForIt ? (
            <p className="text-sm text-muted-foreground">{t('employee.remote.waitingForItHint')}</p>
          ) : null}
          {needsItRegistration ? (
            <p className="text-sm text-muted-foreground">{t('employee.remote.notLinkedHint')}</p>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('employee.remote.setupStep3')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <p className="text-sm text-muted-foreground">{t('employee.remote.consentAlwaysHint')}</p>
          <p className="text-sm text-muted-foreground">{t('employee.remote.needHelpHint')}</p>
          <Button asChild variant="outline" size="sm">
            <Link to="/employee/requests/new">{t('nav.getHelp')}</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
