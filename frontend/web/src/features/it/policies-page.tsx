import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { Search } from 'lucide-react'
import { ApiError, policiesApi, type ManagedDocument } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
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
import { usePolicyUsers } from '@/features/it/policy-users'

const CLASSIFICATIONS = ['Internal', 'Confidential', 'Restricted'] as const

type FilterTab = 'all' | 'Draft' | 'InReview' | 'Approved' | 'Published' | 'needsAttention'

const TABS: { id: FilterTab; labelKey: string }[] = [
  { id: 'all', labelKey: 'policyMgmt.tabs.all' },
  { id: 'Draft', labelKey: 'policyMgmt.status.draft' },
  { id: 'InReview', labelKey: 'policyMgmt.status.inReview' },
  { id: 'Approved', labelKey: 'policyMgmt.status.approved' },
  { id: 'Published', labelKey: 'policyMgmt.status.published' },
  { id: 'needsAttention', labelKey: 'policyMgmt.tabs.needsAttention' },
]

function statusVariant(status: string): 'default' | 'secondary' | 'outline' | 'success' | 'warning' {
  switch (status) {
    case 'Published':
      return 'success'
    case 'Approved':
      return 'default'
    case 'InReview':
      return 'warning'
    default:
      return 'secondary'
  }
}

function needsAttention(doc: ManagedDocument): boolean {
  if (doc.status === 'InReview' || doc.status === 'Approved') return true
  return doc.status === 'Published' && (doc.outstandingAcknowledgementCount ?? 0) > 0
}

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString() : '—'
}

export function PoliciesPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { nameFor } = usePolicyUsers()

  const [tab, setTab] = useState<FilterTab>('all')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [title, setTitle] = useState('')
  const [classification, setClassification] = useState<string>('Internal')
  const [contentText, setContentText] = useState('')
  const [requiresAck, setRequiresAck] = useState(true)
  const [effectiveDate, setEffectiveDate] = useState('')
  const [reviewDate, setReviewDate] = useState('')

  const statusParam = tab === 'all' || tab === 'needsAttention' ? undefined : tab

  const listQuery = useQuery({
    queryKey: ['policies', { status: statusParam ?? 'all', search }],
    queryFn: () =>
      policiesApi.list({ pageSize: 200, status: statusParam, search: search || undefined }),
  })

  const summaryQuery = useQuery({
    queryKey: ['policies', 'workspace-summary'],
    queryFn: () => policiesApi.workspaceSummary(),
  })

  function resetForm() {
    setTitle('')
    setClassification('Internal')
    setContentText('')
    setRequiresAck(true)
    setEffectiveDate('')
    setReviewDate('')
    setError(null)
  }

  const createMutation = useMutation({
    mutationFn: () =>
      policiesApi.create({
        title: title.trim(),
        classification,
        contentText: contentText.trim() ? contentText : null,
        requiresAcknowledgement: requiresAck,
        effectiveDate: effectiveDate ? new Date(effectiveDate).toISOString() : null,
        reviewDate: reviewDate ? new Date(reviewDate).toISOString() : null,
      }),
    onSuccess: async (created) => {
      setOpen(false)
      resetForm()
      await qc.invalidateQueries({ queryKey: ['policies'] })
      navigate(`/it/policies/${created.id}`)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  const seedMutation = useMutation({
    mutationFn: () => policiesApi.seedCatalog(),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['policies'] })
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  const rows = useMemo(() => {
    const items = listQuery.data?.items ?? []
    return tab === 'needsAttention' ? items.filter(needsAttention) : items
  }, [listQuery.data, tab])

  const summary = summaryQuery.data
  const list = listQuery.data

  const columns = useMemo<ColumnDef<ManagedDocument, unknown>[]>(
    () => [
      { accessorKey: 'documentNumber', header: t('docs.columns.number') },
      {
        accessorKey: 'title',
        header: t('docs.columns.title'),
        cell: ({ row }) => <span className="font-medium">{row.original.title}</span>,
      },
      {
        id: 'version',
        header: t('docs.columns.version'),
        cell: ({ row }) =>
          row.original.currentVersionNumber ? `v${row.original.currentVersionNumber}` : '—',
      },
      {
        accessorKey: 'status',
        header: t('docs.columns.status'),
        cell: ({ row }) => (
          <Badge variant={statusVariant(row.original.status)}>
            {t(`policyMgmt.statusValue.${row.original.status}`, {
              defaultValue: row.original.status,
            })}
          </Badge>
        ),
      },
      {
        id: 'owner',
        header: t('policyMgmt.roles.owner'),
        cell: ({ row }) => nameFor(row.original.ownerUserId),
      },
      {
        id: 'reviewer',
        header: t('policyMgmt.roles.reviewer'),
        cell: ({ row }) => nameFor(row.original.reviewerUserId),
      },
      {
        id: 'approver',
        header: t('policyMgmt.roles.approver'),
        cell: ({ row }) => nameFor(row.original.designatedApproverUserId),
      },
      {
        id: 'effective',
        header: t('docs.columns.effective'),
        cell: ({ row }) => formatDate(row.original.effectiveDate),
      },
      {
        id: 'review',
        header: t('docs.columns.review'),
        cell: ({ row }) =>
          row.original.reviewOverdue ? (
            <Badge variant="warning">{t('docs.overdue')}</Badge>
          ) : row.original.reviewDueSoon ? (
            <Badge variant="outline">{t('docs.dueSoon')}</Badge>
          ) : (
            formatDate(row.original.reviewDate)
          ),
      },
      {
        id: 'ack',
        header: t('docs.requiresAck'),
        cell: ({ row }) => (row.original.requiresAcknowledgement ? t('ops.yes') : t('ops.no')),
      },
      {
        id: 'assigned',
        header: t('policyMgmt.columns.assigned'),
        cell: ({ row }) => row.original.assignedEmployeeCount ?? 0,
      },
      {
        id: 'outstanding',
        header: t('policyMgmt.columns.outstanding'),
        cell: ({ row }) => {
          const outstanding = row.original.outstandingAcknowledgementCount ?? 0
          return outstanding > 0 ? (
            <Badge variant="warning">{outstanding}</Badge>
          ) : (
            <span className="text-muted-foreground">0</span>
          )
        },
      },
    ],
    [nameFor, t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('policyMgmt.title')}
        description={t('policyMgmt.description')}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="secondary">
              <Link to="/it/documents">{t('docs.nav.documents')}</Link>
            </Button>
            {can('policy.manage') ? (
              <>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={seedMutation.isPending}
                  onClick={() => seedMutation.mutate()}
                >
                  {t('docs.seedCatalog')}
                </Button>
                <Button type="button" onClick={() => setOpen(true)}>
                  {t('docs.newPolicy')}
                </Button>
              </>
            ) : null}
          </div>
        }
      />

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <SummaryCard
          label={t('policyMgmt.status.draft')}
          value={summary?.draft ?? list?.draftCount ?? 0}
        />
        <SummaryCard
          label={t('policyMgmt.status.inReview')}
          value={summary?.inReview ?? list?.inReviewCount ?? 0}
        />
        <SummaryCard
          label={t('policyMgmt.status.approved')}
          value={summary?.approved ?? list?.approvedCount ?? 0}
        />
        <SummaryCard
          label={t('policyMgmt.status.published')}
          value={summary?.published ?? list?.publishedCount ?? 0}
        />
        <SummaryCard
          label={t('policyMgmt.summary.ackOutstanding')}
          value={summary?.acknowledgementOutstanding ?? list?.acknowledgementOutstandingCount ?? 0}
          emphasis
        />
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <div
          className="flex flex-wrap gap-1 rounded-lg border border-border bg-card p-1"
          role="tablist"
          aria-label={t('policyMgmt.tabs.label')}
        >
          {TABS.map((item) => (
            <Button
              key={item.id}
              type="button"
              role="tab"
              aria-selected={tab === item.id}
              size="sm"
              variant={tab === item.id ? 'default' : 'ghost'}
              onClick={() => setTab(item.id)}
            >
              {t(item.labelKey)}
            </Button>
          ))}
        </div>
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="ps-9"
            value={searchInput}
            placeholder={t('docs.searchPlaceholder')}
            onChange={(event) => setSearchInput(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') setSearch(searchInput.trim())
            }}
          />
        </div>
      </div>

      {error && !open ? <p className="text-sm text-destructive">{error}</p> : null}

      <DataTable
        columns={columns}
        data={rows}
        emptyMessage={t('policyMgmt.empty')}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => navigate(`/it/policies/${row.id}`)}
        getRowId={(row) => row.id}
      />

      <Dialog
        open={open}
        onOpenChange={(next) => {
          setOpen(next)
          if (!next) resetForm()
        }}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{t('docs.newPolicy')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="policy-title">{t('docs.columns.title')}</Label>
              <Input
                id="policy-title"
                value={title}
                onChange={(event) => setTitle(event.target.value)}
              />
            </div>
            <div className="grid gap-3 sm:grid-cols-3">
              <div className="space-y-1">
                <Label>{t('policyMgmt.fields.classification')}</Label>
                <Select value={classification} onValueChange={setClassification}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CLASSIFICATIONS.map((item) => (
                      <SelectItem key={item} value={item}>
                        {t(`policyMgmt.classification.${item}`, { defaultValue: item })}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label htmlFor="policy-effective">{t('docs.columns.effective')}</Label>
                <Input
                  id="policy-effective"
                  type="date"
                  value={effectiveDate}
                  onChange={(event) => setEffectiveDate(event.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="policy-review">{t('docs.columns.review')}</Label>
                <Input
                  id="policy-review"
                  type="date"
                  value={reviewDate}
                  onChange={(event) => setReviewDate(event.target.value)}
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label htmlFor="policy-content">{t('policyMgmt.fields.content')}</Label>
              <Textarea
                id="policy-content"
                className="min-h-40"
                value={contentText}
                onChange={(event) => setContentText(event.target.value)}
                placeholder={t('policyMgmt.fields.contentPlaceholder')}
              />
            </div>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={requiresAck}
                onCheckedChange={(checked) => setRequiresAck(checked === true)}
              />
              {t('policyMgmt.fields.requiresAck')}
            </label>
            <p className="text-xs text-muted-foreground">{t('policyMgmt.create.hint')}</p>
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

function SummaryCard({
  label,
  value,
  emphasis,
}: {
  label: string
  value: number
  emphasis?: boolean
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-xs font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent
        className={
          emphasis && value > 0
            ? 'text-2xl font-semibold tabular-nums text-amber-600 dark:text-amber-400'
            : 'text-2xl font-semibold tabular-nums'
        }
      >
        {value}
      </CardContent>
    </Card>
  )
}
