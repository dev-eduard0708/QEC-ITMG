import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  isRemoteEndpointReady,
  remoteSupportApi,
  type RemoteEndpoint,
} from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { remoteSupportKeys } from '@/features/it/query-keys'

function endpointStatusVariant(endpoint: RemoteEndpoint) {
  if (isRemoteEndpointReady(endpoint)) return 'success' as const
  if (endpoint.connectionStatus === 'Offline') return 'warning' as const
  if (endpoint.connectionStatus === 'Failed') return 'warning' as const
  return 'outline' as const
}

function EndpointRow({
  endpoint,
  onRemove,
  removing,
}: {
  endpoint: RemoteEndpoint
  onRemove?: (id: string) => void
  removing?: boolean
}) {
  const { t } = useTranslation()
  const ready = isRemoteEndpointReady(endpoint)
  const osLine = [endpoint.operatingSystem, endpoint.operatingSystemVersion, endpoint.architecture]
    .filter(Boolean)
    .join(' ')

  return (
    <li className="space-y-2 border-b border-border/60 pb-3 last:border-0 last:pb-0">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="min-w-0">
          <p className="text-sm font-medium">
            <span className="me-2" aria-hidden>
              {ready ? '●' : endpoint.connectionStatus === 'Offline' ? '○' : '◐'}
            </span>
            {endpoint.deviceName}
          </p>
          {osLine ? <p className="text-xs text-muted-foreground">{osLine}</p> : null}
        </div>
        <Badge variant={endpointStatusVariant(endpoint)}>
          {ready
            ? t('employee.remote.readyForSupport')
            : endpoint.connectionStatus === 'Offline'
              ? t('employee.remote.offline')
              : endpoint.connectionStatus === 'WaitingForAgent' ||
                  endpoint.connectionStatus === 'AgentInstalling' ||
                  endpoint.connectionStatus === 'Registering'
                ? t('employee.remote.waitingForAgent')
                : endpoint.connectionStatus}
        </Badge>
      </div>
      {ready ? (
        <p className="text-sm text-muted-foreground">{t('employee.remote.noSetupNeeded')}</p>
      ) : endpoint.connectionStatus === 'Offline' ? (
        <p className="text-xs text-muted-foreground">
          {t('employee.remote.lastSeen')}: {new Date(endpoint.lastSeenAtUtc).toLocaleString()}
        </p>
      ) : null}
      {endpoint.endpointKind === 'Temporary' && onRemove ? (
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={removing}
          onClick={() => onRemove(endpoint.id)}
        >
          {t('employee.remote.removeComputer')}
        </Button>
      ) : null}
    </li>
  )
}

export function EmployeeRemoteSupportSetupPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const setupQuery = useQuery({
    queryKey: remoteSupportKeys.setup(),
    queryFn: () => remoteSupportApi.setup(),
    refetchInterval: (query) => {
      const endpoints = query.state.data?.endpoints ?? []
      const waiting = endpoints.some(
        (e) =>
          !isRemoteEndpointReady(e) &&
          e.connectionStatus !== 'Offline' &&
          e.connectionStatus !== 'Failed',
      )
      return waiting ? 5_000 : false
    },
  })

  const downloadMutation = useMutation({
    mutationFn: () => remoteSupportApi.downloadHelper(),
  })

  const unpairMutation = useMutation({
    mutationFn: (endpointId: string) => remoteSupportApi.unpairEndpoint(endpointId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.setup() })
      await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.myEndpoints() })
    },
  })

  const backButton = (
    <Button asChild variant="outline">
      <Link to="/employee/remote-support">{t('remote.back')}</Link>
    </Button>
  )

  if (setupQuery.isLoading) {
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

  const setup = setupQuery.data
  const endpoints = setup?.endpoints ?? []
  const hasReady = endpoints.some(isRemoteEndpointReady)
  const showDownload = !hasReady

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={t('employee.remote.setupTitle')}
        description={t('employee.remote.setupHint')}
        actions={backButton}
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('employee.remote.yourComputers')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {endpoints.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('employee.remote.noComputersReady')}</p>
          ) : (
            <ul className="space-y-3">
              {endpoints.map((endpoint) => (
                <EndpointRow
                  key={endpoint.id}
                  endpoint={endpoint}
                  removing={unpairMutation.isPending}
                  onRemove={(id) => unpairMutation.mutate(id)}
                />
              ))}
            </ul>
          )}

          {showDownload ? (
            <div className="space-y-3 border-t border-border/60 pt-4">
              <p className="text-sm font-medium">{t('employee.remote.setupRequired')}</p>
              <Button
                type="button"
                disabled={!setup?.helperAvailable || downloadMutation.isPending}
                onClick={() => downloadMutation.mutate()}
              >
                {t('employee.remote.downloadQecHelper')}
              </Button>
              {!setup?.helperAvailable ? (
                <p className="text-sm text-muted-foreground">{t('employee.remote.helperUnavailable')}</p>
              ) : null}
              {downloadMutation.isError ? (
                <p className="text-sm text-destructive">{t('employee.remote.downloadFailed')}</p>
              ) : null}
              <ol className="list-decimal space-y-1 ps-5 text-sm text-muted-foreground">
                <li>{t('employee.remote.setupSteps.download')}</li>
                <li>{t('employee.remote.setupSteps.run')}</li>
                <li>{t('employee.remote.setupSteps.pair')}</li>
                <li>{t('employee.remote.setupSteps.waitReady')}</li>
              </ol>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">{t('employee.remote.noSetupNeeded')}</p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('employee.remote.setupStep3')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <p className="text-sm text-muted-foreground">{t('employee.remote.consentAlwaysHint')}</p>
          <Button asChild>
            <Link to="/employee/remote-support/new">{t('employee.remote.getHelp')}</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
