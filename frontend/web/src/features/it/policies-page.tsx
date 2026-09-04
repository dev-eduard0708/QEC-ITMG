import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { ApiError, policiesApi, type ManagedDocument } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

export function PoliciesPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [title, setTitle] = useState('')
  const [error, setError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: ['policies'],
    queryFn: () => policiesApi.list({ pageSize: 50 }),
  })

  const createMutation = useMutation({
    mutationFn: () => policiesApi.create({ title, requiresAcknowledgement: true }),
    onSuccess: async (created) => {
      setOpen(false)
      await qc.invalidateQueries({ queryKey: ['policies'] })
      navigate(`/it/policies/${created.id}`)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  const seedMutation = useMutation({
    mutationFn: () => policiesApi.seedCatalog(),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['policies'] })
    },
  })

  const columns = useMemo<ColumnDef<ManagedDocument, unknown>[]>(
    () => [
      { accessorKey: 'documentNumber', header: t('docs.columns.number') },
      { accessorKey: 'title', header: t('docs.columns.title') },
      {
        accessorKey: 'status',
        header: t('docs.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      {
        id: 'review',
        header: t('docs.columns.review'),
        cell: ({ row }) =>
          row.original.reviewOverdue ? (
            <Badge variant="warning">{t('docs.overdue')}</Badge>
          ) : row.original.reviewDate ? (
            new Date(row.original.reviewDate).toLocaleDateString()
          ) : (
            '—'
          ),
      },
      {
        accessorKey: 'requiresAcknowledgement',
        header: t('docs.columns.ack'),
        cell: ({ row }) => (row.original.requiresAcknowledgement ? t('ops.yes') : t('ops.no')),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('docs.policiesTitle')}
        description={t('docs.policiesDescription')}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="secondary">
              <Link to="/it/documents">{t('docs.nav.documents')}</Link>
            </Button>
            {can('policy.manage') ? (
              <>
                <Button type="button" variant="secondary" onClick={() => seedMutation.mutate()}>
                  {t('docs.seedCatalog')}
                </Button>
                <Button type="button" onClick={() => setOpen(true)}>
                  {t('docs.newPolicy')}
                </Button>
              </>
            ) : null}
          </div>
        }
      />
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        emptyMessage={t('docs.empty')}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => navigate(`/it/policies/${row.id}`)}
        getRowId={(row) => row.id}
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('docs.newPolicy')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-1">
            <Label>{t('docs.columns.title')}</Label>
            <Input value={title} onChange={(e) => setTitle(e.target.value)} />
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              {t('docs.cancel')}
            </Button>
            <Button type="button" disabled={!title.trim() || createMutation.isPending} onClick={() => createMutation.mutate()}>
              {t('docs.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
