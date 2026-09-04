import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, policiesApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

export function MyPoliciesPage() {
  const { t } = useTranslation()
  const qc = useQueryClient()

  const summaryQuery = useQuery({
    queryKey: ['me', 'policies', 'summary'],
    queryFn: () => policiesApi.summary(),
  })
  const listQuery = useQuery({
    queryKey: ['me', 'policies', 'outstanding'],
    queryFn: () => policiesApi.outstanding(),
  })

  const ackMutation = useMutation({
    mutationFn: (id: string) => policiesApi.acknowledge(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['me', 'policies'] })
    },
  })

  return (
    <div className="space-y-6">
      <PageHeader title={t('docs.myPoliciesTitle')} description={t('docs.myPoliciesDescription')} />
      <p className="text-sm text-muted-foreground">
        {t('docs.outstandingCount', { count: summaryQuery.data?.outstandingForUser ?? 0 })}
      </p>
      <ul className="space-y-3">
        {(listQuery.data ?? []).map((item) => (
          <li key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3">
            <div>
              <div className="font-medium">
                {item.documentNumber} · {item.title}
              </div>
              <div className="text-sm text-muted-foreground">
                v{item.currentVersionNumber ?? '—'} · {item.classification}
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Badge variant="outline">{item.status}</Badge>
              <Button
                type="button"
                size="sm"
                disabled={ackMutation.isPending}
                onClick={() => ackMutation.mutate(item.id)}
              >
                {t('docs.actions.acknowledge')}
              </Button>
            </div>
          </li>
        ))}
        {!listQuery.isLoading && (listQuery.data?.length ?? 0) === 0 ? (
          <li className="text-sm text-muted-foreground">{t('docs.noOutstanding')}</li>
        ) : null}
      </ul>
      {ackMutation.error ? (
        <p className="text-sm text-destructive">
          {ackMutation.error instanceof ApiError ? ackMutation.error.message : t('docs.error.generic')}
        </p>
      ) : null}
    </div>
  )
}
