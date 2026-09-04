import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { evidenceApi, type EvidenceItem } from '@/api/client'
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

export function EvidencePage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [expiredOnly, setExpiredOnly] = useState(false)

  const listQuery = useQuery({
    queryKey: ['evidence', search, status, expiredOnly],
    queryFn: () =>
      evidenceApi.list({
        pageSize: 50,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
        expiredOnly: expiredOnly || undefined,
      }),
  })

  const columns = useMemo<ColumnDef<EvidenceItem, unknown>[]>(
    () => [
      { accessorKey: 'evidenceNumber', header: t('evidence.columns.number') },
      { accessorKey: 'title', header: t('evidence.columns.title') },
      { accessorKey: 'evidenceType', header: t('evidence.columns.type') },
      { accessorKey: 'sourceType', header: t('evidence.columns.source') },
      { accessorKey: 'classification', header: t('evidence.columns.classification') },
      {
        accessorKey: 'status',
        header: t('evidence.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      {
        id: 'valid',
        header: t('evidence.columns.valid'),
        cell: ({ row }) =>
          row.original.validTo
            ? new Date(row.original.validTo).toLocaleDateString()
            : '—',
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('evidence.title')}
        description={t('evidence.description')}
        actions={
          can('evidence.upload') ? (
            <Button asChild>
              <Link to="/it/evidence/new">{t('evidence.new')}</Link>
            </Button>
          ) : null
        }
      />
      <div className="flex flex-wrap gap-2">
        <Input
          className="max-w-xs"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          placeholder={t('evidence.searchPlaceholder')}
          onKeyDown={(e) => {
            if (e.key === 'Enter') setSearch(searchInput.trim())
          }}
        />
        <Button type="button" variant="secondary" onClick={() => setSearch(searchInput.trim())}>
          {t('evidence.search')}
        </Button>
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('evidence.all')}</SelectItem>
            {['Draft', 'Submitted', 'Accepted', 'Expired', 'Superseded', 'Withdrawn'].map((s) => (
              <SelectItem key={s} value={s}>
                {s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Button
          type="button"
          variant={expiredOnly ? 'secondary' : 'outline'}
          onClick={() => setExpiredOnly((v) => !v)}
        >
          {t('evidence.expiredOnly')} ({listQuery.data?.expiredCount ?? 0})
        </Button>
      </div>
      <DataTable
        columns={columns}
        data={listQuery.data?.items ?? []}
        isLoading={listQuery.isLoading}
        onRowClick={(row) => navigate(`/it/evidence/${row.id}`)}
      />
    </div>
  )
}

export function EvidenceNewPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [title, setTitle] = useState('')
  const [evidenceType, setEvidenceType] = useState('Document')
  const [sourceType, setSourceType] = useState('Manual')
  const [classification, setClassification] = useState('Internal')

  const createMutation = useMutation({
    mutationFn: () =>
      evidenceApi.create({
        title,
        evidenceType,
        sourceType,
        classification,
      }),
    onSuccess: async (created) => {
      await qc.invalidateQueries({ queryKey: ['evidence'] })
      navigate(`/it/evidence/${created.id}`)
    },
  })

  return (
    <div className="space-y-6 max-w-xl">
      <PageHeader
        title={t('evidence.new')}
        description={t('evidence.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/evidence">{t('evidence.back')}</Link>
          </Button>
        }
      />
      <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('evidence.fields.title')} />
      <Select value={evidenceType} onValueChange={setEvidenceType}>
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {['Screenshot', 'Report', 'Approval', 'Configuration', 'Log', 'TestResult', 'Document', 'Export', 'Other'].map(
            (v) => (
              <SelectItem key={v} value={v}>
                {v}
              </SelectItem>
            ),
          )}
        </SelectContent>
      </Select>
      <Select value={sourceType} onValueChange={setSourceType}>
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {['Manual', 'Ticket', 'Change', 'AccessReview', 'DrTest', 'BackupRestore', 'Export', 'Other'].map((v) => (
            <SelectItem key={v} value={v}>
              {v}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={classification} onValueChange={setClassification}>
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {['Internal', 'Confidential', 'Restricted'].map((v) => (
            <SelectItem key={v} value={v}>
              {v}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Button type="button" disabled={!title.trim() || createMutation.isPending} onClick={() => createMutation.mutate()}>
        {t('evidence.create')}
      </Button>
    </div>
  )
}
