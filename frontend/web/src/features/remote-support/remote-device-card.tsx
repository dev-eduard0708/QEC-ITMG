import { useTranslation } from 'react-i18next'
import type { RemoteEndpoint } from '@/api/client'
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
  const os = [endpoint.operatingSystem, endpoint.operatingSystemVersion]
    .filter(Boolean)
    .join(' ')

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div>
          <CardTitle className="text-base">{t('remote.device.title')}</CardTitle>
          <p className="mt-1 font-medium">{endpoint.deviceName}</p>
        </div>
        <Badge variant={endpoint.isReadyForRemote ? 'success' : 'warning'}>
          {endpoint.isReadyForRemote
            ? t('remote.device.ready')
            : t('remote.device.waiting')}
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
        <Row label={t('remote.device.connectionStatus')} value={endpoint.connectionStatus} />
        {variant === 'technician' ? (
          <Row
            label={t('remote.device.engine')}
            value={endpoint.hasEngineNode ? t('remote.device.ready') : t('remote.device.waiting')}
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
