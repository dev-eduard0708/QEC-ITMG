import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, policiesApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
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
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={t('employee.policies.title')}
        description={t('employee.policies.description')}
      />
      <p className="text-sm text-muted-foreground">
        {t('employee.policies.outstandingCount', {
          count: summaryQuery.data?.outstandingForUser ?? 0,
        })}
      </p>
      <ul className="space-y-3">
        {(listQuery.data ?? []).map((item) => (
          <li
            key={item.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border p-4"
          >
            <div>
              <div className="font-medium">{item.title}</div>
              <div className="text-sm text-muted-foreground">
                {item.documentNumber}
                {item.currentVersionNumber ? ` · v${item.currentVersionNumber}` : ''}
              </div>
            </div>
            <Button
              type="button"
              size="sm"
              disabled={ackMutation.isPending}
              onClick={() => ackMutation.mutate(item.id)}
            >
              {t('employee.policies.acknowledge')}
            </Button>
          </li>
        ))}
        {!listQuery.isLoading && (listQuery.data?.length ?? 0) === 0 ? (
          <li className="rounded-2xl border border-dashed px-6 py-10 text-center text-sm text-muted-foreground">
            {t('employee.policies.none')}
          </li>
        ) : null}
      </ul>
      {ackMutation.error ? (
        <p className="text-sm text-destructive">
          {ackMutation.error instanceof ApiError
            ? ackMutation.error.message
            : t('docs.error.generic')}
        </p>
      ) : null}
    </div>
  )
}
