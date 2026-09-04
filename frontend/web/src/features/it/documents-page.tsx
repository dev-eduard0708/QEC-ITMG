import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { Search } from 'lucide-react'
import { ApiError, documentsApi, type ManagedDocument } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

const types = ['Policy', 'Procedure', 'Standard', 'Guideline', 'Template', 'Diagram'] as const

export function DocumentsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [type, setType] = useState('all')
  const [overdueOnly, setOverdueOnly] = useState(false)
  const [open, setOpen] = useState(false)
  const [title, setTitle] = useState('')
  const [docType, setDocType] = useState('Procedure')
  const [error, setError] = useState<string | null>(null)

  const listQuery = useQuery({
    queryKey: ['documents', search, type, overdueOnly],
    queryFn: () =>
      documentsApi.list({
        pageSize: 50,
        search: search || undefined,
        type: type === 'all' ? undefined : type,
        reviewOverdueOnly: overdueOnly || undefined,
      }),
  })

  const createMutation = useMutation({
    mutationFn: () => documentsApi.create({ title, documentType: docType }),
    onSuccess: async (created) => {
      setOpen(false)
      await qc.invalidateQueries({ queryKey: ['documents'] })
      navigate(`/it/documents/${created.id}`)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  const columns = useMemo<ColumnDef<ManagedDocument, unknown>[]>(
    () => [
      { accessorKey: 'documentNumber', header: t('docs.columns.number') },
      { accessorKey: 'title', header: t('docs.columns.title') },
      {
        accessorKey: 'documentType',
        header: t('docs.columns.type'),
        cell: ({ row }) => <Badge variant="outline">{row.original.documentType}</Badge>,
      },
      {
        accessorKey: 'status',
        header: t('docs.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      {
        id: 'review',
        header: t('docs.columns.review'),
        cell: ({ row }) =>
          row.original.reviewOverdue ? (
            <Badge variant="warning">{t('docs.overdue')}</Badge>
          ) : row.original.reviewDueSoon ? (
            <Badge variant="outline">{t('docs.dueSoon')}</Badge>
          ) : row.original.reviewDate ? (
            new Date(row.original.reviewDate).toLocaleDateString()
          ) : (
            '—'
          ),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('docs.title')}
        description={t('docs.description')}
        actions={
          <div className="flex flex-wrap gap-2">
            {can('policy.read') ? (
              <Button asChild variant="secondary">
                <Link to="/it/policies">{t('docs.nav.policies')}</Link>
              </Button>
            ) : null}
            <Button asChild variant="secondary">
              <Link to="/employee/policies">{t('docs.nav.myAcks')}</Link>
            </Button>
            {can('doc.manage') ? (
              <Button type="button" onClick={() => setOpen(true)}>
                {t('docs.new')}
              </Button>
            ) : null}
          </div>
        }
      />
      <p className="text-sm text-muted-foreground">
        {t('docs.reviewCounts', {
          overdue: listQuery.data?.reviewOverdueCount ?? 0,
          dueSoon: listQuery.data?.reviewDueSoonCount ?? 0,
        })}
      </p>
      <div className="flex flex-wrap gap-2">
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="ps-9"
            value={searchInput}
            placeholder={t('docs.searchPlaceholder')}
            onChange={(e) => setSearchInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') setSearch(searchInput.trim())
            }}
          />
        </div>
        <Select value={type} onValueChange={setType}>
          <SelectTrigger className="w-[160px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('docs.filters.all')}</SelectItem>
            {types.map((item) => (
              <SelectItem key={item} value={item}>
                {item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Button
          type="button"
          variant={overdueOnly ? 'default' : 'secondary'}
          onClick={() => setOverdueOnly((v) => !v)}
        >
          {t('docs.overdueOnly')}
        </Button>
      </div>
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        emptyMessage={t('docs.empty')}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => navigate(`/it/documents/${row.id}`)}
        getRowId={(row) => row.id}
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('docs.new')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>{t('docs.columns.title')}</Label>
              <Input value={title} onChange={(e) => setTitle(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>{t('docs.columns.type')}</Label>
              <Select value={docType} onValueChange={setDocType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {types.map((item) => (
                    <SelectItem key={item} value={item}>
                      {item}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {error ? <p className="text-sm text-destructive">{error}</p> : null}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              {t('docs.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!title.trim() || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              {t('docs.save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
