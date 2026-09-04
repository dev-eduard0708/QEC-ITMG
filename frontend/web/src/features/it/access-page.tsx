import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { Search } from 'lucide-react'
import { accessApi, type AccessCase } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

const types = ['Joiner', 'Mover', 'Leaver', 'AccessRequest'] as const
const statuses = [
  'Draft',
  'Submitted',
  'Approval',
  'Fulfillment',
  'Verification',
  'Closed',
  'Rejected',
  'Cancelled',
] as const

export function AccessPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [type, setType] = useState('all')
  const [status, setStatus] = useState('all')

  const listQuery = useQuery({
    queryKey: ['access', 'cases', search, type, status],
    queryFn: () =>
      accessApi.listCases({
        pageSize: 50,
        search: search || undefined,
        type: type === 'all' ? undefined : type,
        status: status === 'all' ? undefined : status,
      }),
  })

  const columns = useMemo<ColumnDef<AccessCase, unknown>[]>(
    () => [
      { accessorKey: 'caseNumber', header: t('access.columns.number') },
      {
        accessorKey: 'type',
        header: t('access.columns.type'),
        cell: ({ row }) => <Badge variant="outline">{row.original.type}</Badge>,
      },
      {
        accessorKey: 'status',
        header: t('access.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      { accessorKey: 'reason', header: t('access.columns.reason') },
      {
        id: 'updated',
        header: t('access.columns.updated'),
        cell: ({ row }) => new Date(row.original.updatedAtUtc).toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('access.title')}
        description={t('access.description')}
        actions={
          <div className="flex flex-wrap gap-2">
            {can('access.review') ? (
              <Button asChild variant="secondary">
                <Link to="/it/access/reviews">{t('access.nav.reviews')}</Link>
              </Button>
            ) : null}
            {can('access.privileged.manage') ? (
              <Button asChild variant="secondary">
                <Link to="/it/access/accounts">{t('access.nav.accounts')}</Link>
              </Button>
            ) : null}
            {can('sod.manage') ? (
              <Button asChild variant="secondary">
                <Link to="/it/access/sod">{t('access.nav.sod')}</Link>
              </Button>
            ) : null}
            {can('access.request') ? (
              <Button asChild>
                <Link to="/it/access/new">{t('access.new')}</Link>
              </Button>
            ) : null}
          </div>
        }
      />
      <div className="flex flex-wrap gap-2">
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="ps-9"
            value={searchInput}
            placeholder={t('access.searchPlaceholder')}
            onChange={(e) => setSearchInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') setSearch(searchInput.trim())
            }}
          />
        </div>
        <Select value={type} onValueChange={setType}>
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder={t('access.columns.type')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('access.filters.all')}</SelectItem>
            {types.map((item) => (
              <SelectItem key={item} value={item}>
                {item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder={t('access.columns.status')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('access.filters.all')}</SelectItem>
            {statuses.map((item) => (
              <SelectItem key={item} value={item}>
                {item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
          {t('access.search')}
        </Button>
      </div>
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        emptyMessage={t('access.empty')}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => navigate(`/it/access/${row.id}`)}
        getRowId={(row) => row.id}
      />
    </div>
  )
}
