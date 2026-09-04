import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { Search } from 'lucide-react'
import { changesApi, type ChangeRequest } from '@/api/client'
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
import { changeKeys } from '@/features/it/query-keys'

const types = ['Standard', 'Normal', 'Emergency'] as const
const statuses = [
  'Draft',
  'Assessment',
  'Approval',
  'Scheduled',
  'Implementation',
  'Validation',
  'PostImplementationReview',
  'Closed',
  'Rejected',
  'Failed',
  'RolledBack',
  'RequiresFollowUp',
  'Cancelled',
] as const
const risks = ['Low', 'Medium', 'High', 'Critical'] as const

function formatWindow(start: string | null, end: string | null) {
  if (!start && !end) return '—'
  const a = start ? new Date(start).toLocaleString() : '?'
  const b = end ? new Date(end).toLocaleString() : '?'
  return `${a} → ${b}`
}

export function ChangesPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [type, setType] = useState('all')
  const [status, setStatus] = useState('all')
  const [risk, setRisk] = useState('all')

  const filtersKey = `${search}|${type}|${status}|${risk}`
  const listQuery = useQuery({
    queryKey: changeKeys.list(filtersKey),
    queryFn: () =>
      changesApi.list({
        pageSize: 50,
        search: search || undefined,
        type: type === 'all' ? undefined : type,
        status: status === 'all' ? undefined : status,
        risk: risk === 'all' ? undefined : risk,
      }),
  })

  const columns = useMemo<ColumnDef<ChangeRequest, unknown>[]>(
    () => [
      { accessorKey: 'changeNumber', header: t('changes.columns.number') },
      {
        accessorKey: 'title',
        header: t('changes.columns.title'),
        cell: ({ row }) => (
          <span className="inline-flex items-center gap-2">
            {row.original.title}
            {row.original.isRetrospective ? (
              <Badge variant="warning">{t('changes.retrospective')}</Badge>
            ) : null}
          </span>
        ),
      },
      { accessorKey: 'type', header: t('changes.columns.type') },
      {
        accessorKey: 'status',
        header: t('changes.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      { accessorKey: 'riskRating', header: t('changes.columns.risk') },
      {
        id: 'owner',
        header: t('changes.columns.owner'),
        cell: ({ row }) => row.original.ownerUserId?.slice(0, 8) ?? '—',
      },
      {
        id: 'schedule',
        header: t('changes.columns.schedule'),
        cell: ({ row }) =>
          formatWindow(row.original.scheduledStartUtc, row.original.scheduledEndUtc),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('changes.title')}
        description={t('changes.description')}
        actions={
          <div className="flex gap-2">
            {can('change.read') ? (
              <Button type="button" variant="outline" onClick={() => navigate('/it/changes/catalog')}>
                {t('changes.catalog.title')}
              </Button>
            ) : null}
            {can('change.create') ? (
              <Button type="button" onClick={() => navigate('/it/changes/new')}>
                {t('changes.new')}
              </Button>
            ) : null}
          </div>
        }
      />

      <div className="flex flex-wrap gap-2">
        <div className="relative min-w-[12rem] flex-1">
          <Search className="pointer-events-none absolute top-2.5 start-2 size-4 text-muted-foreground" />
          <Input
            className="ps-8"
            value={searchInput}
            placeholder={t('changes.searchPlaceholder')}
            onChange={(e) => setSearchInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') setSearch(searchInput.trim())
            }}
          />
        </div>
        <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
          <Search className="h-4 w-4" />
        </Button>
        <Select value={type} onValueChange={setType}>
          <SelectTrigger className="w-[9rem]">
            <SelectValue placeholder={t('changes.filters.type')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('changes.filters.all')}</SelectItem>
            {types.map((item) => (
              <SelectItem key={item} value={item}>
                {item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="w-[12rem]">
            <SelectValue placeholder={t('changes.filters.status')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('changes.filters.all')}</SelectItem>
            {statuses.map((item) => (
              <SelectItem key={item} value={item}>
                {item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={risk} onValueChange={setRisk}>
          <SelectTrigger className="w-[9rem]">
            <SelectValue placeholder={t('changes.filters.risk')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('changes.filters.all')}</SelectItem>
            {risks.map((item) => (
              <SelectItem key={item} value={item}>
                {item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {listQuery.isError ? (
        <p className="text-sm text-destructive">{t('changes.error.generic')}</p>
      ) : null}

      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        isLoading={listQuery.isLoading}
        emptyMessage={t('changes.empty')}
        onRowClick={(row) => navigate(`/it/changes/${row.id}`)}
      />
    </div>
  )
}
