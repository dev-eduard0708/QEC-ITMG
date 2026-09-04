import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { ApiError, changesApi, type ChangeCatalogItem } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { changeKeys } from '@/features/it/query-keys'

export function ChangeCatalogPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [createOpen, setCreateOpen] = useState(false)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [riskRating, setRiskRating] = useState('Low')
  const [implementationPlan, setImplementationPlan] = useState('')
  const [testPlan, setTestPlan] = useState('')
  const [rollbackPlan, setRollbackPlan] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const catalogQuery = useQuery({
    queryKey: changeKeys.catalog(),
    queryFn: () => changesApi.listCatalog(),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      changesApi.createCatalog({
        code,
        name,
        description: description || null,
        riskRating,
        implementationPlan,
        testPlan,
        rollbackPlan,
      }),
    onSuccess: async () => {
      setCreateOpen(false)
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: changeKeys.catalog() })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('changes.error.generic'))
    },
  })

  const createChangeMutation = useMutation({
    mutationFn: (catalogItemId: string) => changesApi.createFromCatalog(catalogItemId),
    onSuccess: (created) => navigate(`/it/changes/${created.id}`),
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('changes.error.generic'))
    },
  })

  const deactivateMutation = useMutation({
    mutationFn: (item: ChangeCatalogItem) =>
      changesApi.updateCatalog(item.id, {
        name: item.name,
        description: item.description,
        riskRating: item.riskRating,
        implementationPlan: item.implementationPlan,
        testPlan: item.testPlan,
        rollbackPlan: item.rollbackPlan,
        isActive: false,
        rowVersion: item.rowVersion,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: changeKeys.catalog() })
    },
  })

  const columns = useMemo<ColumnDef<ChangeCatalogItem, unknown>[]>(
    () => [
      { accessorKey: 'code', header: t('changes.catalog.columns.code') },
      { accessorKey: 'name', header: t('changes.catalog.columns.name') },
      { accessorKey: 'riskRating', header: t('changes.catalog.columns.risk') },
      {
        id: 'active',
        header: t('changes.catalog.columns.active'),
        cell: ({ row }) =>
          row.original.isActive ? (
            <Badge>{t('changes.catalog.active')}</Badge>
          ) : (
            <Badge variant="secondary">{t('changes.catalog.inactive')}</Badge>
          ),
      },
      {
        id: 'actions',
        header: t('changes.catalog.columns.actions'),
        cell: ({ row }) => (
          <div className="flex flex-wrap gap-2">
            {row.original.isActive && can('change.create') ? (
              <Button
                type="button"
                size="sm"
                onClick={(e) => {
                  e.stopPropagation()
                  createChangeMutation.mutate(row.original.id)
                }}
              >
                {t('changes.catalog.createChange')}
              </Button>
            ) : null}
            {row.original.isActive && can('change.catalog.manage') ? (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={(e) => {
                  e.stopPropagation()
                  deactivateMutation.mutate(row.original)
                }}
              >
                {t('changes.catalog.deactivate')}
              </Button>
            ) : null}
          </div>
        ),
      },
    ],
    [can, createChangeMutation, deactivateMutation, t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('changes.catalog.title')}
        description={t('changes.catalog.description')}
        actions={
          <div className="flex gap-2">
            <Button type="button" variant="outline" onClick={() => navigate('/it/changes')}>
              {t('changes.back')}
            </Button>
            {can('change.catalog.manage') ? (
              <Button type="button" onClick={() => setCreateOpen(true)}>
                {t('changes.catalog.new')}
              </Button>
            ) : null}
          </div>
        }
      />

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}

      <DataTable
        columns={columns}
        data={catalogQuery.data ?? []}
        isLoading={catalogQuery.isLoading}
        emptyMessage={t('changes.catalog.empty')}
      />

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('changes.catalog.new')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>{t('changes.catalog.fields.code')}</Label>
              <Input value={code} onChange={(e) => setCode(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>{t('changes.catalog.fields.name')}</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>{t('changes.catalog.fields.description')}</Label>
              <Input value={description} onChange={(e) => setDescription(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>{t('changes.fields.risk')}</Label>
              <Select value={riskRating} onValueChange={setRiskRating}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {['Low', 'Medium', 'High', 'Critical'].map((item) => (
                    <SelectItem key={item} value={item}>
                      {item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>{t('changes.fields.implementationPlan')}</Label>
              <textarea
                className="min-h-16 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={implementationPlan}
                onChange={(e) => setImplementationPlan(e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>{t('changes.fields.testPlan')}</Label>
              <textarea
                className="min-h-16 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={testPlan}
                onChange={(e) => setTestPlan(e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>{t('changes.fields.rollbackPlan')}</Label>
              <textarea
                className="min-h-16 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={rollbackPlan}
                onChange={(e) => setRollbackPlan(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setCreateOpen(false)}>
              {t('changes.catalog.cancel')}
            </Button>
            <Button type="button" onClick={() => createMutation.mutate()} disabled={createMutation.isPending}>
              {t('changes.catalog.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
