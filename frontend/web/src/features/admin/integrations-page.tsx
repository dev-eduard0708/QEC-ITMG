import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { integrationsApi, type IntegrationReadinessItem, type IntegrationRunItem } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

function statusVariant(status: string): 'default' | 'secondary' | 'success' | 'warning' | 'outline' {
  switch (status) {
    case 'Healthy':
      return 'success'
    case 'Configured':
      return 'secondary'
    case 'Unhealthy':
      return 'warning'
    case 'NotConfigured':
      return 'outline'
    default:
      return 'default'
  }
}

export function IntegrationsAdminPage() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const readiness = useQuery({
    queryKey: ['admin', 'integrations', 'readiness'],
    queryFn: () => integrationsApi.readiness(),
  })
  const runs = useQuery({
    queryKey: ['admin', 'integrations', 'runs'],
    queryFn: () => integrationsApi.runs(undefined, 20),
  })
  const sync = useMutation({
    mutationFn: (provider: string) => integrationsApi.sync(provider),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['admin', 'integrations'] })
    },
  })

  return (
    <div className="space-y-6">
      <PageHeader title={t('admin.integrations.title')} description={t('admin.integrations.description')} />
      <p className="text-sm text-muted-foreground">{t('admin.integrations.secretsNote')}</p>

      <div className="grid gap-3 lg:grid-cols-2">
        {(readiness.data ?? []).map((item: IntegrationReadinessItem) => (
          <Card key={item.provider}>
            <CardHeader className="flex flex-row items-center justify-between gap-2 space-y-0 pb-2">
              <CardTitle className="text-base">{item.provider}</CardTitle>
              <Badge variant={statusVariant(item.status)}>{item.status}</Badge>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              <div className="grid grid-cols-2 gap-2">
                <span className="text-muted-foreground">{t('admin.integrations.enabled')}</span>
                <span>{item.enabled ? t('admin.integrations.yes') : t('admin.integrations.no')}</span>
                <span className="text-muted-foreground">{t('admin.integrations.configured')}</span>
                <span>{item.configured ? t('admin.integrations.yes') : t('admin.integrations.no')}</span>
                <span className="text-muted-foreground">{t('admin.integrations.runtime')}</span>
                <span>{item.runtimeMode}</span>
                <span className="text-muted-foreground">{t('admin.integrations.lastSuccess')}</span>
                <span>{item.lastSuccessfulSyncUtc ? new Date(item.lastSuccessfulSyncUtc).toLocaleString() : '—'}</span>
                <span className="text-muted-foreground">{t('admin.integrations.lastFailure')}</span>
                <span>{item.lastFailureUtc ? new Date(item.lastFailureUtc).toLocaleString() : '—'}</span>
                <span className="text-muted-foreground">{t('admin.integrations.processed')}</span>
                <span>{item.lastProcessedCount ?? '—'}</span>
                <span className="text-muted-foreground">{t('admin.integrations.unmatched')}</span>
                <span>{item.lastUnmatchedCount ?? '—'}</span>
              </div>
              {item.lastErrorSummary ? (
                <p className="text-xs text-destructive">{item.lastErrorSummary}</p>
              ) : null}
              {item.enabled && item.configured ? (
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  disabled={sync.isPending}
                  onClick={() => sync.mutate(item.provider)}
                >
                  {t('admin.integrations.syncNow')}
                </Button>
              ) : (
                <p className="text-xs text-muted-foreground">{t('admin.integrations.syncDisabledHint')}</p>
              )}
            </CardContent>
          </Card>
        ))}
      </div>

      <div>
        <h3 className="mb-2 text-sm font-medium">{t('admin.integrations.history')}</h3>
        <ul className="space-y-1 text-sm text-muted-foreground">
          {(runs.data ?? []).map((run: IntegrationRunItem) => (
            <li key={run.id}>
              {new Date(run.startedAtUtc).toLocaleString()} · {run.provider} · {run.operation} · {run.status} ·
              p={run.processedCount}/u={run.unmatchedCount}
            </li>
          ))}
          {(runs.data?.length ?? 0) === 0 ? <li>{t('admin.integrations.noRuns')}</li> : null}
        </ul>
      </div>
    </div>
  )
}
