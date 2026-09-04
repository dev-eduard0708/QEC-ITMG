import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import { auditsApi, type AuditEngagement, type AuditReadiness } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

export function AuditsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('all')
  const [section, setSection] = useState<'engagements' | 'findings' | 'capa' | 'requests' | 'readiness'>(
    'engagements',
  )

  const listQuery = useQuery({
    queryKey: ['audits', search, status],
    queryFn: () =>
      auditsApi.list({
        pageSize: 50,
        search: search || undefined,
        status: status === 'all' ? undefined : status,
      }),
  })
  const readinessQuery = useQuery({
    queryKey: ['audits', 'readiness'],
    queryFn: () => auditsApi.readiness(),
    enabled: section === 'readiness',
  })
  const findingsQuery = useQuery({
    queryKey: ['audits', 'findings'],
    queryFn: () => auditsApi.listFindings(),
    enabled: section === 'findings',
  })
  const capaQuery = useQuery({
    queryKey: ['audits', 'capa'],
    queryFn: () => auditsApi.listCapa(),
    enabled: section === 'capa',
  })
  const capaSummaryQuery = useQuery({
    queryKey: ['audits', 'capa-summary'],
    queryFn: () => auditsApi.capaSummary(),
    enabled: section === 'capa',
  })
  const requestsQuery = useQuery({
    queryKey: ['audits', 'evidence-requests'],
    queryFn: () => auditsApi.listEvidenceRequests(),
    enabled: section === 'requests',
  })

  const columns = useMemo<ColumnDef<AuditEngagement, unknown>[]>(
    () => [
      { accessorKey: 'auditNumber', header: t('audits.columns.number') },
      { accessorKey: 'title', header: t('audits.columns.title') },
      { accessorKey: 'auditType', header: t('audits.columns.type') },
      {
        accessorKey: 'status',
        header: t('audits.columns.status'),
        cell: ({ row }) => <Badge variant="secondary">{row.original.status}</Badge>,
      },
      {
        id: 'dates',
        header: t('audits.columns.dates'),
        cell: ({ row }) =>
          `${row.original.startDate ?? '—'} → ${row.original.endDate ?? '—'}`,
      },
    ],
    [t],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('audits.title')}
        description={t('audits.description')}
        actions={
          can('audit.manage') ? (
            <Button asChild>
              <Link to="/it/audits/new">{t('audits.new')}</Link>
            </Button>
          ) : null
        }
      />

      <div className="flex flex-wrap gap-2">
        {(
          [
            ['engagements', 'audits.sections.engagements'],
            ['findings', 'audits.sections.findings'],
            ['capa', 'audits.sections.capa'],
            ['requests', 'audits.sections.requests'],
            ['readiness', 'audits.sections.readiness'],
          ] as const
        ).map(([key, label]) => (
          <Button
            key={key}
            type="button"
            size="sm"
            variant={section === key ? 'default' : 'outline'}
            onClick={() => setSection(key)}
          >
            {t(label)}
          </Button>
        ))}
      </div>

      {section === 'engagements' ? (
        <>
          <div className="flex flex-wrap gap-2">
            <Input
              className="max-w-xs"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder={t('audits.searchPlaceholder')}
            />
            <Button type="button" variant="secondary" onClick={() => setSearch(searchInput)}>
              {t('audits.search')}
            </Button>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger className="w-40">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t('audits.all')}</SelectItem>
                {['Draft', 'Planned', 'InProgress', 'Fieldwork', 'Reporting', 'Closed', 'Cancelled'].map(
                  (s) => (
                    <SelectItem key={s} value={s}>
                      {s}
                    </SelectItem>
                  ),
                )}
              </SelectContent>
            </Select>
          </div>
          <DataTable
            columns={columns}
            data={listQuery.data?.items ?? []}
            emptyMessage={t('audits.empty')}
            isLoading={listQuery.isLoading}
            onRowClick={(row) => navigate(`/it/audits/${row.id}`)}
          />
        </>
      ) : null}

      {section === 'findings' ? (
        <div className="space-y-2">
          {(findingsQuery.data ?? []).map((f) => (
            <Card key={f.id}>
              <CardContent className="flex flex-wrap items-center justify-between gap-2 py-4">
                <div>
                  <p className="font-medium">
                    {f.findingNumber} · {f.title}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {f.severity} · {f.status}
                  </p>
                </div>
                <Button asChild size="sm" variant="outline">
                  <Link to={`/it/audits/${f.auditEngagementId}`}>{t('audits.openEngagement')}</Link>
                </Button>
              </CardContent>
            </Card>
          ))}
          {!findingsQuery.isLoading && (findingsQuery.data?.length ?? 0) === 0 ? (
            <p className="text-sm text-muted-foreground">{t('audits.empty')}</p>
          ) : null}
        </div>
      ) : null}

      {section === 'capa' ? (
        <div className="space-y-4">
          {capaSummaryQuery.data ? (
            <p className="text-sm text-muted-foreground">
              {t('audits.capa.open')}: {capaSummaryQuery.data.open} · {t('audits.capa.overdue')}:{' '}
              {capaSummaryQuery.data.overdue} · {t('audits.capa.awaiting')}:{' '}
              {capaSummaryQuery.data.completedAwaitingVerification} · {t('audits.capa.verified')}:{' '}
              {capaSummaryQuery.data.verified}
            </p>
          ) : null}
          {(capaQuery.data ?? []).map((c) => (
            <Card key={c.id}>
              <CardContent className="py-4">
                <p className="font-medium">
                  {c.actionNumber ?? c.id.slice(0, 8)} · {c.title}
                </p>
                <p className="text-sm text-muted-foreground">
                  {c.status}
                  {c.isOverdue ? ` · ${t('audits.capa.overdue')}` : ''}
                </p>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : null}

      {section === 'requests' ? (
        <div className="space-y-2">
          {(requestsQuery.data ?? []).map((r) => (
            <Card key={r.id}>
              <CardContent className="flex flex-wrap items-center justify-between gap-2 py-4">
                <div>
                  <p className="font-medium">{r.title}</p>
                  <p className="text-sm text-muted-foreground">
                    {r.status}
                    {r.isOverdue ? ` · ${t('audits.capa.overdue')}` : ''}
                  </p>
                </div>
                <Button asChild size="sm" variant="outline">
                  <Link to={`/it/audits/${r.auditEngagementId}`}>{t('audits.openEngagement')}</Link>
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : null}

      {section === 'readiness' && readinessQuery.data ? (
        <ReadinessCards data={readinessQuery.data} />
      ) : null}
    </div>
  )
}

function ReadinessCards({ data }: { data: AuditReadiness }) {
  const { t } = useTranslation()
  const items = [
    [t('audits.readiness.controlsMissingEvidence'), data.controlsWithoutAcceptedEvidence],
    [t('audits.readiness.expiredEvidence'), data.expiredEvidence],
    [t('audits.readiness.openFindings'), data.openFindings],
    [t('audits.readiness.overdueCapa'), data.overdueCapa],
    [t('audits.readiness.policiesOverdue'), data.policiesOverdueReview],
  ] as const
  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">{data.note}</p>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {items.map(([label, value]) => (
          <Card key={label}>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium">{label}</CardTitle>
            </CardHeader>
            <CardContent className="text-2xl font-semibold tabular-nums">{value}</CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}

export function AuditNewPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [title, setTitle] = useState('')
  const [auditType, setAuditType] = useState('Internal')
  const [objective, setObjective] = useState('')

  const createMutation = useMutation({
    mutationFn: () =>
      auditsApi.create({
        title,
        auditType,
        objective: objective || null,
      }),
    onSuccess: (created) => navigate(`/it/audits/${created.id}`),
  })

  return (
    <div className="mx-auto max-w-lg space-y-4">
      <PageHeader title={t('audits.new')} description={t('audits.description')} />
      <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('audits.fields.title')} />
      <Select value={auditType} onValueChange={setAuditType}>
        <SelectTrigger>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {['Internal', 'External', 'ISA315Profile', 'Other'].map((x) => (
            <SelectItem key={x} value={x}>
              {x}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Input
        value={objective}
        onChange={(e) => setObjective(e.target.value)}
        placeholder={t('audits.fields.objective')}
      />
      <div className="flex gap-2">
        <Button asChild variant="outline">
          <Link to="/it/audits">{t('audits.back')}</Link>
        </Button>
        <Button
          type="button"
          disabled={!title.trim() || createMutation.isPending}
          onClick={() => createMutation.mutate()}
        >
          {t('audits.create')}
        </Button>
      </div>
    </div>
  )
}
