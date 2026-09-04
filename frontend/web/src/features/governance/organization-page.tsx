import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { governanceApi, type OrganizationalUnit } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export function OrganizationPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [parentId, setParentId] = useState('')

  const unitsQuery = useQuery({
    queryKey: ['governance', 'units'],
    queryFn: () => governanceApi.listUnits(),
  })
  const profileQuery = useQuery({
    queryKey: ['governance', 'profile'],
    queryFn: () => governanceApi.getProfile(),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      governanceApi.createUnit({
        name,
        code: code || null,
        parentId: parentId || null,
      }),
    onSuccess: async () => {
      setName('')
      setCode('')
      setParentId('')
      await qc.invalidateQueries({ queryKey: ['governance', 'units'] })
    },
  })

  const columns = useMemo<ColumnDef<OrganizationalUnit, unknown>[]>(
    () => [
      { accessorKey: 'name', header: t('governance.organization.columns.name') },
      { accessorKey: 'code', header: t('governance.organization.columns.code') },
      {
        id: 'parent',
        header: t('governance.organization.columns.parent'),
        cell: ({ row }) => {
          const parent = unitsQuery.data?.find((u) => u.id === row.original.parentId)
          return parent?.name ?? '—'
        },
      },
      {
        id: 'members',
        header: t('governance.organization.columns.members'),
        cell: ({ row }) => row.original.memberUserIds.length,
      },
      {
        accessorKey: 'isActive',
        header: t('governance.organization.columns.active'),
        cell: ({ row }) => (row.original.isActive ? 'Yes' : 'No'),
      },
    ],
    [t, unitsQuery.data],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('governance.organization.title')}
        description={t('governance.organization.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/governance">{t('governance.nav.back')}</Link>
          </Button>
        }
      />

      {profileQuery.data ? (
        <p className="text-sm text-muted-foreground">
          {profileQuery.data.legalName} · {profileQuery.data.timezone}
        </p>
      ) : null}

      {can('gov.manage') ? (
        <div className="grid gap-3 md:grid-cols-4 items-end">
          <div className="space-y-1">
            <Label>{t('governance.organization.columns.name')}</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label>{t('governance.organization.columns.code')}</Label>
            <Input value={code} onChange={(e) => setCode(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label>{t('governance.organization.columns.parent')}</Label>
            <Input
              value={parentId}
              onChange={(e) => setParentId(e.target.value)}
              placeholder="parent unit id"
            />
          </div>
          <Button
            type="button"
            disabled={!name.trim() || createMutation.isPending}
            onClick={() => createMutation.mutate()}
          >
            {t('governance.organization.addUnit')}
          </Button>
        </div>
      ) : null}

      <DataTable columns={columns} data={unitsQuery.data ?? []} isLoading={unitsQuery.isLoading} />
    </div>
  )
}
