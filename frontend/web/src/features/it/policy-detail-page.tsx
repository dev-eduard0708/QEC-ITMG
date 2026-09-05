import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, policiesApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export function PolicyDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [dueAt, setDueAt] = useState('')

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
  const statsQuery = useQuery({
    queryKey: ['policies', id, 'ack-stats'],
    queryFn: () => policiesApi.acknowledgementStats(id),
    enabled: !!id && can('policy.read'),
  })
  const rowsQuery = useQuery({
    queryKey: ['policies', id, 'ack-rows'],
    queryFn: () => policiesApi.acknowledgementRows(id),
    enabled: !!id && can('policy.read') && docQuery.data?.status === 'Published',
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

  const stats = statsQuery.data

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

      {doc.currentContentText ? (
        <pre className="max-h-64 overflow-auto whitespace-pre-wrap rounded-xl border bg-muted/20 p-4 text-sm">
          {doc.currentContentText}
        </pre>
      ) : null}

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
            <Button
              type="button"
              variant="secondary"
              onClick={() => run.mutate(() => policiesApi.returnToDraft(id))}
            >
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

      {doc.status === 'Published' && doc.requiresAcknowledgement && can('policy.manage') ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('docs.policyAssign.title')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-2">
              <Label htmlFor="due">{t('docs.policyAssign.due')}</Label>
              <Input
                id="due"
                type="datetime-local"
                value={dueAt}
                onChange={(e) => setDueAt(e.target.value)}
              />
            </div>
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                onClick={() =>
                  run.mutate(() =>
                    policiesApi.assign(id, {
                      scope: 'AllEmployees',
                      dueAtUtc: dueAt ? new Date(dueAt).toISOString() : null,
                      isRequired: true,
                    }),
                  )
                }
              >
                {t('docs.policyAssign.allEmployees')}
              </Button>
              <Button asChild variant="outline">
                <a href={policiesApi.acknowledgementExportUrl(id)} target="_blank" rel="noreferrer">
                  {t('docs.policyAssign.exportCsv')}
                </a>
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">{t('docs.policyAssign.hint')}</p>
          </CardContent>
        </Card>
      ) : null}

      {stats ? (
        <div className="grid gap-3 sm:grid-cols-4">
          <Stat label={t('docs.policyAssign.assigned')} value={stats.assigned} />
          <Stat label={t('docs.policyAssign.acknowledged')} value={stats.acknowledged} />
          <Stat label={t('docs.policyAssign.outstanding')} value={stats.outstanding} />
          <Stat label={t('docs.policyAssign.overdue')} value={stats.overdue} />
        </div>
      ) : null}

      {(rowsQuery.data?.length ?? 0) > 0 ? (
        <section className="space-y-2 overflow-x-auto rounded-xl border">
          <table className="w-full min-w-[640px] text-sm">
            <thead className="bg-muted/40 text-start">
              <tr>
                <th className="px-3 py-2 font-medium">{t('docs.policyAssign.employee')}</th>
                <th className="px-3 py-2 font-medium">{t('docs.policyAssign.upn')}</th>
                <th className="px-3 py-2 font-medium">{t('docs.policyAssign.status')}</th>
                <th className="px-3 py-2 font-medium">{t('docs.policyAssign.ackedAt')}</th>
              </tr>
            </thead>
            <tbody>
              {(rowsQuery.data ?? []).map((row) => (
                <tr key={row.userId} className="border-t">
                  <td className="px-3 py-2">{row.displayName ?? '—'}</td>
                  <td className="px-3 py-2">{row.upn ?? '—'}</td>
                  <td className="px-3 py-2">{row.status}</td>
                  <td className="px-3 py-2">
                    {row.acknowledgedAtUtc ? new Date(row.acknowledgedAtUtc).toLocaleString() : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ) : null}

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

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-xs font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent className="text-2xl font-semibold tabular-nums">{value}</CardContent>
    </Card>
  )
}
