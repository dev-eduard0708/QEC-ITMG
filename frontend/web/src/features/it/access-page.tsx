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
import { Skeleton } from '@/components/ui/skeleton'
import { AccessNavTabs } from '@/features/it/access-nav'
import { AccessStatusBadge } from '@/features/it/access-ux'
import { useAccessUsers } from '@/features/it/access-users'
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

const queueSubheadingKeys: Partial<Record<AccessWorkQueue, string>> = {
  'for-approval': 'access.queueSub.forApproval',
  'for-fulfillment': 'access.queueSub.forFulfillment',
  'for-verification': 'access.queueSub.forVerification',
  'for-closure': 'access.queueSub.forClosure',
  'my-requests': 'access.queueSub.myRequests',
  all: 'access.queueSub.all',
}

const queueEmptyKeys: Partial<Record<AccessWorkQueue, string>> = {
  'for-approval': 'access.emptyQueue.forApproval',
  'for-fulfillment': 'access.emptyQueue.forFulfillment',
  'for-verification': 'access.emptyQueue.forVerification',
  'for-closure': 'access.emptyQueue.forClosure',
  'my-requests': 'access.emptyQueue.myRequests',
  all: 'access.empty',
}

function categoryLabel(row: AccessCase): string {
  return row.accessCategoryDisplayName ?? row.accessCategoryNameSnapshot ?? '—'
}

function employeeLabel(row: AccessCase): string {
  if (row.subjectName?.trim()) return row.subjectName.trim()
  if (row.subjectEmail?.trim()) return row.subjectEmail.trim()
  return '—'
}

export function AccessPage() {
  const { t } = useTranslation()
  const { can, user } = useAuth()
  const { nameFor } = useAccessUsers()
  const navigate = useNavigate()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [type, setType] = useState('all')
  const [status, setStatus] = useState('all')
  const [queue, setQueue] = useState<AccessWorkQueue>('my-requests')

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
      {
        id: 'case',
        header: t('access.columns.case'),
        cell: ({ row }) => (
          <div className="min-w-0 max-w-[220px]">
            <p className="truncate font-medium">{row.original.caseNumber}</p>
            <p className="truncate text-xs text-muted-foreground">{row.original.reason}</p>
          </div>
        ),
      },
      {
        id: 'employee',
        header: t('access.columns.employee'),
        cell: ({ row }) => (
          <div className="min-w-0 max-w-[160px]">
            <p className="truncate text-sm">{employeeLabel(row.original)}</p>
            {row.original.subjectEmail && row.original.subjectName ? (
              <p className="truncate text-xs text-muted-foreground">{row.original.subjectEmail}</p>
            ) : null}
          </div>
        ),
      },
      {
        id: 'category',
        header: t('access.columns.category'),
        cell: ({ row }) => (
          <span className="text-sm">{categoryLabel(row.original)}</span>
        ),
      },
      {
        accessorKey: 'type',
        header: t('access.columns.type'),
        cell: ({ row }) => (
          <Badge variant="outline" className="font-normal">
            {t(`access.types.${row.original.type}`, { defaultValue: row.original.type })}
          </Badge>
        ),
      },
      {
        id: 'currentStep',
        header: t('access.columns.currentStep'),
        cell: ({ row }) => <AccessStatusBadge accessCase={row.original} />,
      },
      {
        id: 'requestedBy',
        header: t('access.columns.requestedBy'),
        cell: ({ row }) => (
          <span className="text-sm text-muted-foreground">
            {nameFor(row.original.requesterUserId)}
          </span>
        ),
      },
      {
        id: 'updated',
        header: t('access.columns.updated'),
        cell: ({ row }) => (
          <span className="whitespace-nowrap text-xs text-muted-foreground">
            {new Date(row.original.updatedAtUtc).toLocaleString()}
          </span>
        ),
      },
    ],
    [nameFor, t],
  )

  const subheadingKey = queueSubheadingKeys[queue]
  const emptyKey = queueEmptyKeys[queue] ?? 'access.empty'
  const countsLoading = countQueries.some((q) => q.isLoading)

  return (
    <div className="space-y-5">
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

      <div className="space-y-2">
        <div className="flex flex-wrap gap-1.5 rounded-lg border bg-muted/20 p-1.5">
          {visibleQueues.map((tab) => {
            const count = queueCounts.get(tab.id)
            const active = queue === tab.id
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => setQueue(tab.id)}
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-sm font-medium transition-colors',
                  active
                    ? 'bg-background text-foreground shadow-sm'
                    : 'text-muted-foreground hover:bg-background/60 hover:text-foreground',
                )}
              >
                {t(tab.labelKey)}
                {countsLoading && typeof count !== 'number' ? (
                  <Skeleton className="h-4 w-5 rounded-sm" />
                ) : typeof count === 'number' ? (
                  <span
                    className={cn(
                      'inline-flex h-5 min-w-5 items-center justify-center rounded-sm px-1 text-[10px] tabular-nums',
                      active ? 'bg-muted text-foreground' : 'bg-muted/80 text-muted-foreground',
                    )}
                  >
                    {count}
                  </span>
                ) : null}
              </button>
            )
          })}
        </div>
        {subheadingKey ? (
          <p className="text-sm text-muted-foreground">{t(subheadingKey)}</p>
        ) : null}
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
                {t(`access.types.${item}`)}
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
        emptyMessage={t(emptyKey)}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => navigate(`/it/access/${row.id}`)}
        getRowId={(row) => row.id}
      />
    </div>
  )
}
