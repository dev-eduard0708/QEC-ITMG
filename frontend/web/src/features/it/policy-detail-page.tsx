import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, policiesApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

export function PolicyDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [error, setError] = useState<string | null>(null)

  const docQuery = useQuery({
    queryKey: ['policies', id],
    queryFn: () => policiesApi.get(id),
    enabled: !!id,
  })
  const versionsQuery = useQuery({
    queryKey: ['policies', id, 'versions'],
    queryFn: () => policiesApi.listVersions(id),
    enabled: !!id,
  })

  const run = useMutation({
    mutationFn: async (fn: () => Promise<unknown>) => fn(),
    onSuccess: async () => {
      setError(null)
      await qc.invalidateQueries({ queryKey: ['policies', id] })
      await qc.invalidateQueries({ queryKey: ['policies'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  const doc = docQuery.data
  if (docQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('docs.loading')}</p>
  if (!doc) return <p className="text-sm text-destructive">{t('docs.notFound')}</p>

  return (
    <div className="space-y-6">
      <PageHeader
        title={doc.documentNumber}
        description={doc.title}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/policies">{t('docs.back')}</Link>
          </Button>
        }
      />
      <div className="flex flex-wrap gap-2">
        <Badge variant="outline">Policy</Badge>
        <Badge variant="secondary">{doc.status}</Badge>
        <Badge variant="outline">{doc.classification}</Badge>
        {doc.reviewOverdue ? <Badge variant="warning">{t('docs.overdue')}</Badge> : null}
      </div>
      <dl className="grid gap-2 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-muted-foreground">{t('docs.columns.version')}</dt>
          <dd>v{doc.currentVersionNumber ?? '—'}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('docs.columns.effective')}</dt>
          <dd>{doc.effectiveDate ? new Date(doc.effectiveDate).toLocaleDateString() : '—'}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('docs.columns.review')}</dt>
          <dd>{doc.reviewDate ? new Date(doc.reviewDate).toLocaleDateString() : '—'}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('docs.columns.ack')}</dt>
          <dd>{doc.requiresAcknowledgement ? t('ops.yes') : t('ops.no')}</dd>
        </div>
      </dl>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      <div className="flex flex-wrap gap-2">
        {doc.status === 'Draft' && can('policy.manage') ? (
          <Button type="button" onClick={() => run.mutate(() => policiesApi.submit(id))}>
            {t('docs.actions.submit')}
          </Button>
        ) : null}
        {doc.status === 'InReview' && can('policy.approve') ? (
          <>
            <Button type="button" onClick={() => run.mutate(() => policiesApi.approve(id))}>
              {t('docs.actions.approve')}
            </Button>
            <Button type="button" variant="secondary" onClick={() => run.mutate(() => policiesApi.returnToDraft(id))}>
              {t('docs.actions.return')}
            </Button>
          </>
        ) : null}
        {doc.status === 'Approved' && can('policy.manage') ? (
          <Button type="button" onClick={() => run.mutate(() => policiesApi.publish(id))}>
            {t('docs.actions.publish')}
          </Button>
        ) : null}
      </div>
      <section className="space-y-2">
        <h2 className="text-base font-medium">{t('docs.versions')}</h2>
        <ul className="space-y-1 text-sm">
          {(versionsQuery.data ?? []).map((v) => (
            <li key={v.id}>
              v{v.versionNumber}
              {v.publishedAtUtc ? ` · published ${new Date(v.publishedAtUtc).toLocaleString()}` : ''}
              {v.changeSummary ? ` — ${v.changeSummary}` : ''}
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
