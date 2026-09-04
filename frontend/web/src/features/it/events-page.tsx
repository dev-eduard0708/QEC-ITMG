import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { Search } from 'lucide-react'
import { ApiError, eventsApi, type OperationalEvent } from '@/api/client'
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
import { eventKeys } from '@/features/it/query-keys'

const statuses = ['New', 'Acknowledged', 'Promoted', 'Closed'] as const
const severities = ['Info', 'Warning', 'Critical', 'Emergency'] as const

export function EventsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [severity, setSeverity] = useState('all')
  const [ingestOpen, setIngestOpen] = useState(false)
  const [source, setSource] = useState('Manual')
  const [sourceEventKey, setSourceEventKey] = useState('')
  const [ingestSeverity, setIngestSeverity] = useState('Warning')
  const [title, setTitle] = useState('')
  const [summary, setSummary] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const filtersKey = `${search}|${status}|${severity}`
  const listQuery = useQuery({
    queryKey: eventKeys.list(filtersKey),
    queryFn: () =>
      eventsApi.list({
        pageSize: 50,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        severity: severity === 'all' ? undefined : severity,
      }),
  })

  const ingestMutation = useMutation({
    mutationFn: () =>
      eventsApi.ingest({
        source,
        sourceEventKey,
        severity: ingestSeverity,
        title,
        summary,
      }),
    onSuccess: async (result) => {
      setIngestOpen(false)
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: eventKeys.all })
      navigate(`/it/events/${result.event.id}`)
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('events.error.generic'))
    },
  })

  const columns = useMemo<ColumnDef<OperationalEvent, unknown>[]>(
    () => [
      { accessorKey: 'eventNumber', header: t('events.columns.number') },
      { accessorKey: 'title', header: t('events.columns.title') },
      {
        accessorKey: 'severity',
        header: t('events.columns.severity'),
        cell: ({ row }) => <Badge variant="outline">{row.original.severity}</Badge>,
      },
      {
        accessorKey: 'status',
        header: t('events.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      { accessorKey: 'source', header: t('events.columns.source') },
      { accessorKey: 'occurrenceCount', header: t('events.columns.count') },
      {
        id: 'lastSeen',
        header: t('events.columns.lastSeen'),
        cell: ({ row }) => new Date(row.original.lastSeenAtUtc).toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('events.title')}
        description={t('events.description')}
        actions={
          can('event.admin') ? (
            <Button type="button" onClick={() => setIngestOpen(true)}>
              {t('events.ingest')}
            </Button>
          ) : undefined
        }
      />

      <div className="flex flex-wrap gap-2">
        <div className="relative min-w-[12rem] flex-1">
          <Search className="pointer-events-none absolute top-2.5 start-2 size-4 text-muted-foreground" />
          <Input
            className="ps-8"
            value={searchInput}
            placeholder={t('events.searchPlaceholder')}
            onChange={(e) => setSearchInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') setSearch(searchInput.trim())
            }}
          />
        </div>
        <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
          <Search className="h-4 w-4" />
        </Button>
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="w-[10rem]">
            <SelectValue placeholder={t('events.filters.status')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('events.filters.all')}</SelectItem>
            {statuses.map((item) => (
              <SelectItem key={item} value={item}>
                {item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={severity} onValueChange={setSeverity}>
          <SelectTrigger className="w-[10rem]">
            <SelectValue placeholder={t('events.filters.severity')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('events.filters.all')}</SelectItem>
            {severities.map((item) => (
              <SelectItem key={item} value={item}>
                {item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {listQuery.isError ? (
        <p className="text-sm text-destructive">{t('events.error.generic')}</p>
      ) : null}

      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        isLoading={listQuery.isLoading}
        emptyMessage={t('events.empty')}
        onRowClick={(row) => navigate(`/it/events/${row.id}`)}
      />

      <Dialog open={ingestOpen} onOpenChange={setIngestOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('events.ingest')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>{t('events.fields.source')}</Label>
              <Input value={source} onChange={(e) => setSource(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>{t('events.fields.sourceKey')}</Label>
              <Input value={sourceEventKey} onChange={(e) => setSourceEventKey(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>{t('events.fields.severity')}</Label>
              <Select value={ingestSeverity} onValueChange={setIngestSeverity}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {severities.map((item) => (
                    <SelectItem key={item} value={item}>
                      {item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>{t('events.fields.title')}</Label>
              <Input value={title} onChange={(e) => setTitle(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>{t('events.fields.summary')}</Label>
              <textarea
                className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={summary}
                onChange={(e) => setSummary(e.target.value)}
              />
            </div>
            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setIngestOpen(false)}>
              {t('events.cancel')}
            </Button>
            <Button type="button" onClick={() => ingestMutation.mutate()} disabled={ingestMutation.isPending}>
              {t('events.submitIngest')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
