import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQueries, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { Search } from 'lucide-react'
import { accessApi, type AccessCase, type AccessWorkQueue } from '@/api/client'
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
import { AccessNavTabs } from '@/features/it/access-nav'
import { cn } from '@/lib/utils'

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

type QueueTab = {
  id: AccessWorkQueue
  labelKey: string
  configureOnly?: boolean
}

const queueTabs: QueueTab[] = [
  { id: 'my-requests', labelKey: 'access.queue.myRequests' },
  { id: 'for-approval', labelKey: 'access.queue.forApproval' },
  { id: 'for-fulfillment', labelKey: 'access.queue.forFulfillment' },
  { id: 'for-verification', labelKey: 'access.queue.forVerification' },
  { id: 'for-closure', labelKey: 'access.queue.forClosure' },
  { id: 'all', labelKey: 'access.queue.all', configureOnly: true },
]

function categoryLabel(row: AccessCase): string {
  return row.accessCategoryDisplayName ?? row.accessCategoryNameSnapshot ?? '—'
}

export function AccessPage() {
  const { t } = useTranslation()
  const { can, user } = useAuth()
  const navigate = useNavigate()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [type, setType] = useState('all')
  const [status, setStatus] = useState('all')
  const [queue, setQueue] = useState<AccessWorkQueue>('my-requests')

  // Anyone who can open the access list sees workflow queues; All stays configure-only.
  const visibleQueues = queueTabs.filter((tab) => !tab.configureOnly || can('access.configure'))

  const listQuery = useQuery({
    queryKey: ['access', 'cases', user?.id, search, type, status, queue],
    queryFn: () =>
      accessApi.listCases({
        pageSize: 50,
        search: search || undefined,
        type: type === 'all' ? undefined : type,
        status: status === 'all' ? undefined : status,
        queue,
      }),
    refetchInterval: 12_000,
    refetchOnWindowFocus: true,
  })

  const countQueries = useQueries({
    queries: visibleQueues.map((tab) => ({
      queryKey: ['access', 'cases', 'count', user?.id, tab.id] as const,
      queryFn: () => accessApi.listCases({ pageSize: 1, queue: tab.id }),
      refetchInterval: 12_000,
      refetchOnWindowFocus: true,
    })),
  })

  const queueCounts = useMemo(() => {
    const map = new Map<AccessWorkQueue, number>()
    visibleQueues.forEach((tab, index) => {
      const count = countQueries[index]?.data?.totalCount
      if (typeof count === 'number') map.set(tab.id, count)
    })
    return map
  }, [countQueries, visibleQueues])

  const columns = useMemo<ColumnDef<AccessCase, unknown>[]>(
    () => [
      { accessorKey: 'caseNumber', header: t('access.columns.number') },
      {
        id: 'category',
        header: t('access.columns.category'),
        cell: ({ row }) => categoryLabel(row.original),
      },
      {
        accessorKey: 'type',
        header: t('access.columns.type'),
        cell: ({ row }) => <Badge variant="outline">{row.original.type}</Badge>,
      },
      {
        accessorKey: 'status',
        header: t('access.columns.status'),
        cell: ({ row }) => (
          <span className="inline-flex flex-wrap items-center gap-1">
            <Badge variant="secondary">{row.original.status}</Badge>
            {row.original.isReadyToClose ? (
              <Badge variant="success">{t('access.status.readyToClose')}</Badge>
            ) : null}
          </span>
        ),
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
          can('access.request') ? (
            <Button asChild>
              <Link to="/it/access/new">{t('access.new')}</Link>
            </Button>
          ) : null
        }
      />
      <AccessNavTabs />

      <div className="flex flex-wrap gap-2">
        {visibleQueues.map((tab) => {
          const count = queueCounts.get(tab.id)
          return (
            <Button
              key={tab.id}
              type="button"
              size="sm"
              variant={queue === tab.id ? 'default' : 'outline'}
              className={cn(queue === tab.id && 'shadow-sm')}
              onClick={() => setQueue(tab.id)}
            >
              {t(tab.labelKey)}
              {typeof count === 'number' ? (
                <Badge
                  variant={queue === tab.id ? 'secondary' : 'outline'}
                  className="ms-1.5 h-5 min-w-5 justify-center px-1.5 text-[10px]"
                >
                  {count}
                </Badge>
              ) : null}
            </Button>
          )
        })}
      </div>

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
