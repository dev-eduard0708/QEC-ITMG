import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { Search } from 'lucide-react'
import { ApiError, problemsApi, type Problem } from '@/api/client'
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
import { problemKeys } from '@/features/it/query-keys'

export function ProblemsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [priority, setPriority] = useState('all')
  const [createOpen, setCreateOpen] = useState(false)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [createPriority, setCreatePriority] = useState('Medium')
  const [formError, setFormError] = useState<string | null>(null)

  const filtersKey = `${search}|${status}|${priority}`
  const listQuery = useQuery({
    queryKey: problemKeys.list(filtersKey),
    queryFn: () =>
      problemsApi.list({
        pageSize: 50,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        priority: priority === 'all' ? undefined : priority,
      }),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      problemsApi.create({
        title,
        description,
        priority: createPriority,
      }),
    onSuccess: async (created) => {
      setCreateOpen(false)
      setTitle('')
      setDescription('')
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: problemKeys.all })
      navigate(`/it/problems/${created.id}`)
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('problems.error.generic'))
    },
  })

  const columns = useMemo<ColumnDef<Problem, unknown>[]>(
    () => [
      { accessorKey: 'problemNumber', header: t('problems.columns.number') },
      { accessorKey: 'title', header: t('problems.columns.title') },
      {
        accessorKey: 'status',
        header: t('problems.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      { accessorKey: 'priority', header: t('problems.columns.priority') },
      {
        id: 'owner',
        header: t('problems.columns.owner'),
        cell: ({ row }) => row.original.ownerUserId?.slice(0, 8) ?? '—',
      },
      {
        id: 'ci',
        header: t('problems.columns.ci'),
        cell: ({ row }) => row.original.configurationItemId?.slice(0, 8) ?? '—',
      },
      {
        id: 'updated',
        header: t('problems.columns.updated'),
        cell: ({ row }) => new Date(row.original.updatedAtUtc).toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('problems.title')}
        description={t('problems.description')}
        actions={
          can('problems.manage') ? (
            <Button type="button" onClick={() => setCreateOpen(true)}>
              {t('problems.new')}
            </Button>
          ) : undefined
        }
      />

      <div className="flex flex-wrap items-end gap-3">
        <div className="flex min-w-[220px] flex-1 gap-2">
          <Input
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder={t('problems.searchPlaceholder')}
            onKeyDown={(event) => {
              if (event.key === 'Enter') setSearch(searchInput.trim())
            }}
          />
          <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
            <Search className="h-4 w-4" />
          </Button>
        </div>
        <div className="space-y-1">
          <p className="text-xs text-muted-foreground">{t('problems.filters.status')}</p>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {['all', 'New', 'Investigating', 'Resolved', 'Closed'].map((option) => (
                <SelectItem key={option} value={option}>
                  {option === 'all' ? 'All' : option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <p className="text-xs text-muted-foreground">{t('problems.filters.priority')}</p>
          <Select value={priority} onValueChange={setPriority}>
            <SelectTrigger className="w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {['all', 'Low', 'Medium', 'High', 'Critical'].map((option) => (
                <SelectItem key={option} value={option}>
                  {option === 'all' ? 'All' : option}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        isLoading={listQuery.isLoading}
        emptyMessage={t('problems.empty')}
        onRowClick={(row) => navigate(`/it/problems/${row.id}`)}
      />

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('problems.new')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="problem-title">{t('problems.fields.title')}</Label>
              <Input id="problem-title" value={title} onChange={(event) => setTitle(event.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="problem-description">{t('problems.fields.description')}</Label>
              <textarea
                id="problem-description"
                className="min-h-24 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>{t('problems.fields.priority')}</Label>
              <Select value={createPriority} onValueChange={setCreatePriority}>
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
            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setCreateOpen(false)}>
              {t('problems.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!title.trim() || !description.trim() || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              {t('problems.create')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
</div>
  )
}
