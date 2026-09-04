import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { governanceApi, type RegisterCiRow, type RegisterServiceRow } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

type RegisterKind = 'applications' | 'infrastructure' | 'interfaces' | 'business-services'

export function RegistersPage() {
  const { t } = useTranslation()
  const [kind, setKind] = useState<RegisterKind>('applications')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')

  const isServices = kind === 'business-services'
  const ciQuery = useQuery({
    queryKey: ['governance', 'registers', kind, search],
    queryFn: () => governanceApi.ciRegister(kind as 'applications' | 'infrastructure' | 'interfaces', search || undefined),
    enabled: !isServices,
  })
  const serviceQuery = useQuery({
    queryKey: ['governance', 'registers', 'business-services', search],
    queryFn: () => governanceApi.businessServicesRegister(search || undefined),
    enabled: isServices,
  })

  const ciColumns = useMemo<ColumnDef<RegisterCiRow, unknown>[]>(
    () => [
      { accessorKey: 'ciNumber', header: t('governance.registers.columns.number') },
      { accessorKey: 'name', header: t('governance.registers.columns.name') },
      { accessorKey: 'ciTypeName', header: t('governance.registers.columns.type') },
      { accessorKey: 'status', header: t('governance.registers.columns.status') },
      { accessorKey: 'criticality', header: t('governance.registers.columns.criticality') },
      {
        id: 'updated',
        header: t('governance.registers.columns.updated'),
        cell: ({ row }) => new Date(row.original.updatedAtUtc).toLocaleString(),
      },
      {
        id: 'rels',
        header: t('governance.registers.columns.relationships'),
        cell: ({ row }) => row.original.relationships.length,
      },
    ],
    [t],
  )

  const serviceColumns = useMemo<ColumnDef<RegisterServiceRow, unknown>[]>(
    () => [
      { accessorKey: 'name', header: t('governance.registers.columns.name') },
      { accessorKey: 'criticality', header: t('governance.registers.columns.criticality') },
      { accessorKey: 'isActive', header: t('governance.registers.columns.status') },
      {
        id: 'cis',
        header: t('governance.registers.columns.linkedCis'),
        cell: ({ row }) => row.original.linkedConfigurationItemIds.length,
      },
      {
        id: 'updated',
        header: t('governance.registers.columns.updated'),
        cell: ({ row }) => new Date(row.original.updatedAtUtc).toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('governance.registers.title')}
        description={t('governance.registers.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/governance">{t('governance.nav.back')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Select value={kind} onValueChange={(v) => setKind(v as RegisterKind)}>
          <SelectTrigger className="w-56">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="applications">{t('governance.registers.applications')}</SelectItem>
            <SelectItem value="infrastructure">{t('governance.registers.infrastructure')}</SelectItem>
            <SelectItem value="interfaces">{t('governance.registers.interfaces')}</SelectItem>
            <SelectItem value="business-services">{t('governance.registers.services')}</SelectItem>
          </SelectContent>
        </Select>
        <Input
          className="max-w-xs"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          placeholder={t('governance.searchPlaceholder')}
          onKeyDown={(e) => {
            if (e.key === 'Enter') setSearch(searchInput.trim())
          }}
        />
        <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
          {t('governance.search')}
        </Button>
      </div>

      {isServices ? (
        <DataTable
          columns={serviceColumns}
          data={serviceQuery.data ?? []}
          isLoading={serviceQuery.isLoading}
        />
      ) : (
        <DataTable
          columns={ciColumns}
          data={ciQuery.data ?? []}
          isLoading={ciQuery.isLoading}
        />
      )}
    </div>
  )
}
