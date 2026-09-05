import { useTranslation } from 'react-i18next'
import { isRemoteEndpointReady, type RemoteEndpoint } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

type RemoteDeviceCardProps = {
  endpoint: RemoteEndpoint
  variant: 'employee' | 'technician'
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-2 text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-end">{value}</span>
    </div>
  )
}

export function RemoteDeviceCard({ endpoint, variant }: RemoteDeviceCardProps) {
  const { t } = useTranslation()
  const ready = isRemoteEndpointReady(endpoint)
  const state =
    ready
      ? 'ready'
      : endpoint.connectionStatus === 'Offline' || endpoint.connectionStatus === 'Expired'
        ? 'offline'
        : endpoint.connectionStatus === 'Failed'
          ? 'failed'
          : 'waiting'
  const os = [endpoint.operatingSystem, endpoint.operatingSystemVersion]
    .filter(Boolean)
    .join(' ')

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div>
          <CardTitle className="text-base">{t('remote.device.title')}</CardTitle>
          <p className="mt-1 font-medium">{endpoint.deviceName}</p>
          {variant === 'employee' && ready ? (
            <p className="mt-1 text-sm text-muted-foreground">
              {t('remote.device.computerReady')}
            </p>
          ) : null}
        </div>
        <Badge
          variant={
            state === 'ready'
              ? 'success'
              : state === 'failed' || state === 'offline'
                ? 'outline'
                : 'warning'
          }
        >
          {t(`remote.device.simpleStatus.${state}`)}
        </Badge>
      </CardHeader>
      <CardContent className="space-y-2">
        {variant === 'technician' ? (
          <Row
            label={t('remote.device.kind')}
            value={t(`remote.device.kindValue.${endpoint.endpointKind}`, {
              defaultValue: endpoint.endpointKind,
            })}
          />
        ) : null}
        <Row label={t('remote.device.operatingSystem')} value={os || '—'} />
        <Row label={t('remote.device.architecture')} value={endpoint.architecture ?? '—'} />
        <Row
          label={t('remote.device.connectionStatus')}
          value={t(`remote.device.connection.${endpoint.connectionStatus}`, {
            defaultValue: endpoint.connectionStatus,
          })}
        />
        {variant === 'technician' ? (
          <Row
            label={t('remote.device.engine')}
            value={t(`remote.device.engineStatus.${state}`)}
          />
        ) : null}
        <Row
          label={t('remote.device.lastSeen')}
          value={new Date(endpoint.lastSeenAtUtc).toLocaleString()}
        />
      </CardContent>
    </Card>
  )
}
