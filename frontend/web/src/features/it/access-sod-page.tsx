import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { ApiError, accessApi, type SodRule } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export function AccessSodPage() {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [name, setName] = useState('')
  const [left, setLeft] = useState('')
  const [right, setRight] = useState('')
  const [severity, setSeverity] = useState('High')
  const [error, setError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: ['access', 'sod'],
    queryFn: () => accessApi.listSod({ pageSize: 50 }),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      accessApi.createSod({
        name,
        leftEntitlementKey: left,
        rightEntitlementKey: right,
        severity,
      }),
    onSuccess: async () => {
      setName('')
      setLeft('')
      setRight('')
      setError(null)
      await qc.invalidateQueries({ queryKey: ['access', 'sod'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  const columns = useMemo<ColumnDef<SodRule, unknown>[]>(
    () => [
      { accessorKey: 'name', header: t('access.columns.name') },
      { accessorKey: 'leftEntitlementKey', header: t('access.columns.left') },
      { accessorKey: 'rightEntitlementKey', header: t('access.columns.right') },
      {
        accessorKey: 'severity',
        header: t('access.columns.severity'),
        cell: ({ row }) => <Badge variant="warning">{row.original.severity}</Badge>,
      },
      {
        accessorKey: 'isActive',
        header: t('access.columns.active'),
        cell: ({ row }) => (row.original.isActive ? t('ops.yes') : t('ops.no')),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('access.sodTitle')}
        description={t('access.sodDescription')}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access">{t('access.back')}</Link>
          </Button>
        }
      />
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        emptyMessage={t('access.empty')}
        isLoading={listQuery.isLoading}
      />
      <div className="grid max-w-xl gap-3">
        <h2 className="text-base font-medium">{t('access.sodCreate')}</h2>
        <div className="space-y-1">
          <Label>{t('access.columns.name')}</Label>
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="space-y-1">
          <Label>{t('access.columns.left')}</Label>
          <Input value={left} onChange={(e) => setLeft(e.target.value)} />
        </div>
        <div className="space-y-1">
          <Label>{t('access.columns.right')}</Label>
          <Input value={right} onChange={(e) => setRight(e.target.value)} />
        </div>
        <div className="space-y-1">
          <Label>{t('access.columns.severity')}</Label>
          <Input value={severity} onChange={(e) => setSeverity(e.target.value)} />
        </div>
        {error ? <p className="text-sm text-destructive">{error}</p> : null}
        <Button
          type="button"
          disabled={!name.trim() || !left.trim() || !right.trim() || createMutation.isPending}
          onClick={() => createMutation.mutate()}
        >
          {t('access.save')}
        </Button>
      </div>
    </div>
  )
}
