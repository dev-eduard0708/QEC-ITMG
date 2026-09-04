import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { ApiError, accessApi, type AccessReviewCampaign } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

export function AccessReviewsPage() {
  const { t } = useTranslation()
  const { can, user } = useAuth()
  const qc = useQueryClient()
  const [name, setName] = useState('')
  const [type, setType] = useState('UserAccess')
  const [due, setDue] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [summary, setSummary] = useState('')

  const listQuery = useQuery({
    queryKey: ['access', 'reviews'],
    queryFn: () => accessApi.listReviews({ pageSize: 50 }),
  })
  const itemsQuery = useQuery({
    queryKey: ['access', 'reviews', selectedId, 'items'],
    queryFn: () => accessApi.listReviewItems(selectedId!),
    enabled: !!selectedId,
  })

  const createMutation = useMutation({
    mutationFn: () =>
      accessApi.createReview({
        name,
        type,
        reviewerUserId: user!.id,
        startsAtUtc: new Date().toISOString(),
        dueAtUtc: new Date(due).toISOString(),
      }),
    onSuccess: async () => {
      setName('')
      setDue('')
      setError(null)
      await qc.invalidateQueries({ queryKey: ['access', 'reviews'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  const columns = useMemo<ColumnDef<AccessReviewCampaign, unknown>[]>(
    () => [
      { accessorKey: 'name', header: t('access.columns.name') },
      { accessorKey: 'type', header: t('access.columns.type') },
      {
        accessorKey: 'status',
        header: t('access.columns.status'),
        cell: ({ row }) => (
          <span className="inline-flex items-center gap-2">
            <Badge variant="secondary">{row.original.status}</Badge>
            {row.original.isOverdue ? <Badge variant="warning">{t('access.overdue')}</Badge> : null}
          </span>
        ),
      },
      {
        accessorKey: 'pendingCount',
        header: t('access.columns.pending'),
      },
      {
        id: 'due',
        header: t('access.columns.due'),
        cell: ({ row }) => new Date(row.original.dueAtUtc).toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('access.reviewsTitle')}
        description={t('access.reviewsDescription')}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access">{t('access.back')}</Link>
          </Button>
        }
      />
      <p className="text-sm text-muted-foreground">
        {t('access.reviewsCounts', {
          overdue: listQuery.data?.overdueCount ?? 0,
          pending: listQuery.data?.pendingDecisionCount ?? 0,
        })}
      </p>
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        emptyMessage={t('access.empty')}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => setSelectedId(row.id)}
        getRowId={(row) => row.id}
      />

      {can('access.review') ? (
        <div className="grid max-w-xl gap-3">
          <h2 className="text-base font-medium">{t('access.reviewsCreate')}</h2>
          <div className="space-y-1">
            <Label>{t('access.columns.name')}</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label>{t('access.columns.type')}</Label>
            <Select value={type} onValueChange={setType}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {['UserAccess', 'Privileged', 'ServiceAccount'].map((item) => (
                  <SelectItem key={item} value={item}>
                    {item}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label>{t('access.columns.due')}</Label>
            <Input type="datetime-local" value={due} onChange={(e) => setDue(e.target.value)} />
          </div>
          {error ? <p className="text-sm text-destructive">{error}</p> : null}
          <Button
            type="button"
            disabled={!name.trim() || !due || !user?.id || createMutation.isPending}
            onClick={() => createMutation.mutate()}
          >
            {t('access.save')}
          </Button>
        </div>
      ) : null}

      {selectedId ? (
        <section className="space-y-3">
          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              size="sm"
              onClick={async () => {
                await accessApi.openReview(selectedId)
                await qc.invalidateQueries({ queryKey: ['access', 'reviews'] })
              }}
            >
              {t('access.actions.openReview')}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="secondary"
              onClick={async () => {
                await accessApi.completeReview(selectedId)
                await qc.invalidateQueries({ queryKey: ['access', 'reviews'] })
              }}
            >
              {t('access.actions.completeReview')}
            </Button>
            <Button
              type="button"
              size="sm"
              variant="secondary"
              onClick={async () => {
                await accessApi.prepareReviewEvidence(selectedId)
              }}
            >
              {t('access.actions.prepareEvidence')}
            </Button>
          </div>
          <ul className="space-y-2 text-sm">
            {(itemsQuery.data ?? []).map((item) => (
              <li key={item.id} className="flex flex-wrap items-center gap-2">
                <span>{item.accessSummary}</span>
                <Badge variant="outline">{item.decision}</Badge>
                {item.decision === 'Pending' ? (
                  <>
                    <Button
                      type="button"
                      size="sm"
                      onClick={async () => {
                        await accessApi.decideReviewItem(selectedId, item.id, 'Keep')
                        await qc.invalidateQueries({ queryKey: ['access', 'reviews', selectedId, 'items'] })
                      }}
                    >
                      Keep
                    </Button>
                    <Button
                      type="button"
                      size="sm"
                      variant="secondary"
                      onClick={async () => {
                        await accessApi.decideReviewItem(selectedId, item.id, 'Remove')
                        await qc.invalidateQueries({ queryKey: ['access', 'reviews', selectedId, 'items'] })
                      }}
                    >
                      Remove
                    </Button>
                  </>
                ) : null}
              </li>
            ))}
          </ul>
          <div className="flex gap-2">
            <Input value={summary} onChange={(e) => setSummary(e.target.value)} placeholder={t('access.fields.accessSummary')} />
            <Button
              type="button"
              disabled={!summary.trim()}
              onClick={async () => {
                await accessApi.addReviewItem(selectedId, { accessSummary: summary })
                setSummary('')
                await qc.invalidateQueries({ queryKey: ['access', 'reviews', selectedId, 'items'] })
              }}
            >
              {t('access.actions.addItem')}
            </Button>
          </div>
        </section>
      ) : null}
    </div>
  )
}
