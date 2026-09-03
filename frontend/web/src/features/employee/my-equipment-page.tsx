import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { meApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { equipmentKeys } from '@/features/it/query-keys'

export function MyEquipmentPage() {
  const { t } = useTranslation()
  const query = useQuery({
    queryKey: equipmentKeys.mine,
    queryFn: () => meApi.listEquipment(),
  })

  return (
    <div className="space-y-6">
      <PageHeader title={t('equipment.title')} description={t('equipment.description')} />

      {query.isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-28 w-full" />
          <Skeleton className="h-28 w-full" />
        </div>
      ) : (query.data ?? []).length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('equipment.empty')}</p>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {(query.data ?? []).map((asset) => (
            <Card key={asset.id}>
              <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
                <div>
                  <CardTitle className="text-base">{asset.name}</CardTitle>
                  <p className="mt-1 text-sm text-muted-foreground">{asset.assetNumber}</p>
                </div>
                <Badge variant="secondary">{asset.status}</Badge>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                <Row label={t('equipment.fields.model')} value={[asset.manufacturer, asset.model].filter(Boolean).join(' ') || '—'} />
                <Row label={t('equipment.fields.serial')} value={asset.serialNumber ?? '—'} />
                <Row
                  label={t('equipment.fields.assignedAt')}
                  value={
                    asset.activeAssignedAtUtc
                      ? new Date(asset.activeAssignedAtUtc).toLocaleString()
                      : '—'
                  }
                />
                <Row
                  label={t('equipment.fields.location')}
                  value={asset.locationId ? asset.locationId.slice(0, 8) : '—'}
                />
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-2">
      <span className="text-muted-foreground">{label}</span>
      <span>{value}</span>
    </div>
  )
}
