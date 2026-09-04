import { useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { Search } from 'lucide-react'
import { ticketsApi, type Ticket } from '@/api/client'
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
import { ticketKeys } from '@/features/it/query-keys'

function slaLabel(ticket: Ticket, t: (key: string) => string): string {
  if (ticket.resolutionBreached || ticket.responseBreached) {
    return t('tickets.sla.breached')
  }
  const due = ticket.resolutionDueAtUtc ?? ticket.responseDueAtUtc
  if (!due) return t('tickets.sla.none')
  return new Date(due).toLocaleString()
}

export function TicketsPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState(searchParams.get('status') || 'all')
  const [type, setType] = useState(searchParams.get('type') || 'all')
  const [priority, setPriority] = useState(searchParams.get('priority') || 'all')

  const filtersKey = `${search}|${status}|${type}|${priority}`
  const listQuery = useQuery({
    queryKey: ticketKeys.list(filtersKey),
    queryFn: () =>
      ticketsApi.list({
        pageSize: 50,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        type: type === 'all' ? undefined : type,
        priority: priority === 'all' ? undefined : priority,
      }),
  })

  const queueQuery = useQuery({
    queryKey: ticketKeys.queues(),
    queryFn: () => ticketsApi.listQueues(),
  })

  const columns = useMemo<ColumnDef<Ticket, unknown>[]>(
    () => [
      {
        accessorKey: 'ticketNumber',
        header: t('tickets.columns.number'),
        cell: ({ row }) => (
          <span className="inline-flex items-center gap-2">
            {row.original.ticketNumber}
            {row.original.isMajorIncident ? (
              <Badge variant="warning">{t('tickets.incident.majorBadge')}</Badge>
            ) : null}
          </span>
        ),
      },
      { accessorKey: 'title', header: t('tickets.columns.title') },
      {
        id: 'requester',
        header: t('tickets.columns.requester'),
        cell: ({ row }) => row.original.requesterUserId.slice(0, 8),
      },
      {
        accessorKey: 'status',
        header: t('tickets.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      { accessorKey: 'priority', header: t('tickets.columns.priority') },
      {
        id: 'queue',
        header: t('tickets.columns.queue'),
        cell: ({ row }) => {
          const queueId = row.original.queueId
          if (!queueId) return '—'
          return queueQuery.data?.find((queue) => queue.id === queueId)?.name ?? queueId.slice(0, 8)
        },
      },
      {
        id: 'assignee',
        header: t('tickets.columns.assignee'),
        cell: ({ row }) => row.original.assignedUserId?.slice(0, 8) ?? '—',
      },
      {
        id: 'sla',
        header: t('tickets.columns.sla'),
        cell: ({ row }) => {
          const breached = row.original.responseBreached || row.original.resolutionBreached
          return (
            <span className={breached ? 'font-medium text-destructive' : undefined}>
              {slaLabel(row.original, t)}
            </span>
          )
        },
      },
    ],
    [queueQuery.data, t],
  )

  return (
    <div className="space-y-6">
      <PageHeader title={t('tickets.title')} description={t('tickets.description')} />

      <div className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-[220px] flex-1 gap-2">
          <Input
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder={t('tickets.searchPlaceholder')}
            onKeyDown={(event) => {
              if (event.key === 'Enter') setSearch(searchInput.trim())
            }}
          />
          <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
            <Search className="h-4 w-4" />
          </Button>
        </div>
        <FilterSelect
          label={t('tickets.filters.type')}
          value={type}
          onChange={setType}
          options={['all', 'Incident', 'ServiceRequest']}
        />
        <FilterSelect
          label={t('tickets.filters.status')}
          value={status}
          onChange={setStatus}
          options={['all', 'New', 'Open', 'InProgress', 'PendingRequester', 'Resolved', 'Closed', 'Cancelled']}
        />
        <FilterSelect
          label={t('tickets.filters.priority')}
          value={priority}
          onChange={setPriority}
          options={['all', 'Low', 'Medium', 'High', 'Critical']}
        />
      </div>

      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        isLoading={listQuery.isLoading}
        emptyMessage={t('tickets.empty')}
        onRowClick={(row) => navigate(`/it/tickets/${row.id}`)}
      />
    </div>
  )
}

function FilterSelect({
  label,
  value,
  onChange,
  options,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: string[]
}) {
  return (
    <div className="space-y-1">
      <p className="text-xs text-muted-foreground">{label}</p>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger className="w-[160px]">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {options.map((option) => (
            <SelectItem key={option} value={option}>
              {option === 'all' ? 'All' : option}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}
