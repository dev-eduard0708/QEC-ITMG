import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError, policiesApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'

export function EmployeePolicyDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [accepted, setAccepted] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  const policyQuery = useQuery({
    queryKey: ['me', 'policies', 'detail', id],
    queryFn: () => policiesApi.mineGet(id),
    enabled: Boolean(id),
  })

  const ackMutation = useMutation({
    mutationFn: () => policiesApi.mineAcknowledge(id, true),
    onSuccess: async () => {
      setError(null)
      setSuccess(t('employee.policies.ackSuccess', { date: new Date().toLocaleString() }))
      await qc.invalidateQueries({ queryKey: ['me', 'policies'] })
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : t('docs.error.generic'))
    },
  })

  if (policyQuery.isLoading) {
    return <p className="text-sm text-muted-foreground">{t('docs.loading')}</p>
  }

  const policy = policyQuery.data
  if (!policy) {
    return <p className="text-sm text-destructive">{t('employee.policies.notAssigned')}</p>
  }

  const needsAck = policy.status === 'NeedsAcknowledgement' || policy.status === 'Overdue'
  const body = policy.contentText ?? policy.summary

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={policy.title}
        description={`${policy.documentNumber} · v${policy.versionNumber}`}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/policies">{t('employee.policies.back')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">{policy.classification}</Badge>
        <Badge variant={policy.status === 'Overdue' ? 'warning' : 'outline'}>
          {policy.status === 'Acknowledged'
            ? t('employee.policies.badge.acknowledged')
            : policy.status === 'Overdue'
              ? t('employee.policies.badge.overdue')
              : t('employee.policies.badge.needs')}
        </Badge>
      </div>

      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-muted-foreground">{t('docs.columns.effective')}</dt>
          <dd>{policy.effectiveDate ? new Date(policy.effectiveDate).toLocaleDateString() : '—'}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('employee.policies.dueLabel')}</dt>
          <dd>{policy.dueAtUtc ? new Date(policy.dueAtUtc).toLocaleString() : '—'}</dd>
        </div>
      </dl>

      <article className="prose prose-sm dark:prose-invert max-w-none rounded-2xl border bg-card p-5">
        <h2 className="text-base font-semibold">{t('employee.policies.readPolicy')}</h2>
        {body ? (
          <pre className="whitespace-pre-wrap font-sans text-sm leading-relaxed text-foreground">{body}</pre>
        ) : (
          <p className="text-sm text-muted-foreground">{t('employee.policies.noBody')}</p>
        )}
      </article>

      {policy.acknowledgedAtUtc ? (
        <div className="rounded-xl border border-primary/30 bg-primary/5 px-4 py-3 text-sm">
          {t('employee.policies.ackedOn', {
            date: new Date(policy.acknowledgedAtUtc).toLocaleString(),
          })}
        </div>
      ) : null}

      {success ? (
        <div className="rounded-xl border border-primary/30 bg-primary/5 px-4 py-3 text-sm">{success}</div>
      ) : null}
      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      {needsAck ? (
        <section className="space-y-4 rounded-2xl border p-4">
          <div className="flex items-start gap-3">
            <input
              id="policy-ack"
              type="checkbox"
              className="mt-1 h-4 w-4"
              checked={accepted}
              onChange={(e) => setAccepted(e.target.checked)}
            />
            <Label htmlFor="policy-ack" className="text-sm leading-relaxed">
              {t('employee.policies.statement')}
            </Label>
          </div>
          <Button
            type="button"
            size="lg"
            disabled={!accepted || ackMutation.isPending}
            onClick={() => ackMutation.mutate()}
          >
            {t('employee.policies.acknowledge')}
          </Button>
        </section>
      ) : null}
    </div>
  )
}
