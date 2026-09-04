import { useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, documentsApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

export function DocumentDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string | null>(null)

  const docQuery = useQuery({
    queryKey: ['documents', id],
    queryFn: () => documentsApi.get(id),
    enabled: !!id,
  })
  const versionsQuery = useQuery({
    queryKey: ['documents', id, 'versions'],
    queryFn: () => documentsApi.listVersions(id),
    enabled: !!id,
  })

  const run = useMutation({
    mutationFn: async (fn: () => Promise<unknown>) => fn(),
    onSuccess: async () => {
      setError(null)
      await qc.invalidateQueries({ queryKey: ['documents', id] })
      await qc.invalidateQueries({ queryKey: ['documents'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  const doc = docQuery.data
  if (docQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('docs.loading')}</p>
  if (!doc) return <p className="text-sm text-destructive">{t('docs.notFound')}</p>

  const policyPath = doc.documentType === 'Policy'
  const approvePerm = policyPath ? can('policy.approve') || can('doc.approve') : can('doc.approve')
  const managePerm = policyPath ? can('policy.manage') || can('doc.manage') : can('doc.manage')

  return (
    <div className="space-y-6">
      <PageHeader
        title={doc.documentNumber}
        description={doc.title}
        actions={
          <Button asChild variant="secondary">
            <Link to={policyPath ? '/it/policies' : '/it/documents'}>{t('docs.back')}</Link>
          </Button>
        }
      />
      <div className="flex flex-wrap gap-2">
        <Badge variant="outline">{doc.documentType}</Badge>
        <Badge variant="secondary">{doc.status}</Badge>
        <Badge variant="outline">{doc.classification}</Badge>
        {doc.reviewOverdue ? <Badge variant="warning">{t('docs.overdue')}</Badge> : null}
        {doc.requiresAcknowledgement ? <Badge variant="outline">{t('docs.requiresAck')}</Badge> : null}
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
          <dd>
            {doc.reviewDate ? new Date(doc.reviewDate).toLocaleDateString() : '—'}
            {doc.daysToReview != null ? ` (${doc.daysToReview}d)` : ''}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('docs.columns.approver')}</dt>
          <dd>{doc.currentApprovedByUserId ?? doc.designatedApproverUserId ?? '—'}</dd>
        </div>
      </dl>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      <div className="flex flex-wrap gap-2">
        {doc.status === 'Draft' && managePerm ? (
          <Button type="button" onClick={() => run.mutate(() => documentsApi.submit(id))}>
            {t('docs.actions.submit')}
          </Button>
        ) : null}
        {doc.status === 'InReview' && approvePerm ? (
          <>
            <Button type="button" onClick={() => run.mutate(() => documentsApi.approve(id))}>
              {t('docs.actions.approve')}
            </Button>
            <Button type="button" variant="secondary" onClick={() => run.mutate(() => documentsApi.returnToDraft(id))}>
              {t('docs.actions.return')}
            </Button>
          </>
        ) : null}
        {doc.status === 'Approved' && managePerm ? (
          <Button type="button" onClick={() => run.mutate(() => documentsApi.publish(id))}>
            {t('docs.actions.publish')}
          </Button>
        ) : null}
        {managePerm && !['Retired', 'Superseded'].includes(doc.status) ? (
          <Button
            type="button"
            variant="secondary"
            onClick={() => run.mutate(() => documentsApi.createRevision(id, 'Revision'))}
          >
            {t('docs.actions.revise')}
          </Button>
        ) : null}
        {managePerm && doc.status === 'Draft' ? (
          <>
            <Input
              ref={fileRef}
              type="file"
              className="max-w-xs"
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (!file) return
                run.mutate(async () => {
                  await documentsApi.uploadAttachment(id, file)
                  await qc.invalidateQueries({ queryKey: ['documents', id] })
                })
              }}
            />
          </>
        ) : null}
      </div>
      <section className="space-y-2">
        <h2 className="text-base font-medium">{t('docs.versions')}</h2>
        <ul className="space-y-1 text-sm">
          {(versionsQuery.data ?? []).map((v) => (
            <li key={v.id}>
              v{v.versionNumber}
              {v.publishedAtUtc ? ` · published ${new Date(v.publishedAtUtc).toLocaleString()}` : ''}
              {v.approvedAtUtc ? ` · approved` : ''}
              {v.attachmentId ? ` · attachment` : ''}
              {v.changeSummary ? ` — ${v.changeSummary}` : ''}
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
