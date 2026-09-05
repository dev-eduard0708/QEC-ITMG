import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { reportsApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'

type Section =
  | 'executive'
  | 'servicedesk'
  | 'incidents'
  | 'changes'
  | 'cmdb'
  | 'security'
  | 'compliance'
  | 'audit'
  | 'bcm'
  | 'vendors'

const sectionPerm: Record<Section, string> = {
  executive: 'report.executive',
  servicedesk: 'report.servicedesk',
  incidents: 'report.incident',
  changes: 'report.change',
  cmdb: 'report.cmdb',
  security: 'report.security',
  compliance: 'report.compliance',
  audit: 'report.audit',
  bcm: 'report.bcm',
  vendors: 'report.vendor',
}

function MetricGrid({ data }: { data: Record<string, unknown> | null | undefined }) {
  if (!data) return null
  const entries = Object.entries(data).filter(
    ([k, v]) =>
      !['note', 'source', 'generatedAtUtc', 'asOfUtc', 'tiles', 'dashboard', 'serviceReadiness', 'assessmentResults', 'controlsByStatus', 'controlsByDomain', 'openByPriority', 'openByStatus', 'workloadByAssignee'].includes(k) &&
      (typeof v === 'number' || typeof v === 'string' || typeof v === 'boolean' || v === null),
  )
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {entries.map(([key, value]) => (
        <Card key={key}>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium">{key}</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold tabular-nums">
            {value === null || value === undefined ? 'N/A' : String(value)}
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

export function ReportsPage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const [section, setSection] = useState<Section>('executive')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  const range = {
    from: from ? new Date(from).toISOString() : undefined,
    to: to ? new Date(to).toISOString() : undefined,
  }

  const sections = (Object.keys(sectionPerm) as Section[]).filter((s) => can(sectionPerm[s]))
  const current = sections.includes(section) ? section : (sections[0] ?? 'executive')

  const execQuery = useQuery({
    queryKey: ['reports', 'executive'],
    queryFn: () => reportsApi.executive(),
    enabled: current === 'executive' && can('report.executive'),
  })
  const snapQuery = useQuery({
    queryKey: ['reports', 'snapshots'],
    queryFn: () => reportsApi.executiveSnapshots(14),
    enabled: current === 'executive' && can('report.executive'),
  })
  const sdQuery = useQuery({
    queryKey: ['reports', 'servicedesk', range],
    queryFn: () => reportsApi.serviceDesk(range),
    enabled: current === 'servicedesk',
  })
  const incQuery = useQuery({
    queryKey: ['reports', 'incidents', range],
    queryFn: () => reportsApi.incidents(range),
    enabled: current === 'incidents',
  })
  const chgQuery = useQuery({
    queryKey: ['reports', 'changes', range],
    queryFn: () => reportsApi.changes(range),
    enabled: current === 'changes',
  })
  const cmdbQuery = useQuery({
    queryKey: ['reports', 'cmdb'],
    queryFn: () => reportsApi.cmdb(),
    enabled: current === 'cmdb',
  })
  const secQuery = useQuery({
    queryKey: ['reports', 'security'],
    queryFn: () => reportsApi.security(),
    enabled: current === 'security',
  })
  const compQuery = useQuery({
    queryKey: ['reports', 'compliance'],
    queryFn: () => reportsApi.compliance(),
    enabled: current === 'compliance',
  })
  const auditQuery = useQuery({
    queryKey: ['reports', 'audit'],
    queryFn: () => reportsApi.audit(),
    enabled: current === 'audit',
  })
  const bcmQuery = useQuery({
    queryKey: ['reports', 'bcm'],
    queryFn: () => reportsApi.bcm(),
    enabled: current === 'bcm',
  })
  const vendorQuery = useQuery({
    queryKey: ['reports', 'vendors'],
    queryFn: () => reportsApi.vendors(),
    enabled: current === 'vendors',
  })

  const activeData =
    current === 'servicedesk'
      ? sdQuery.data
      : current === 'incidents'
        ? incQuery.data
        : current === 'changes'
          ? chgQuery.data
          : current === 'cmdb'
            ? cmdbQuery.data
            : current === 'security'
              ? secQuery.data
              : current === 'compliance'
                ? compQuery.data
                : current === 'audit'
                  ? auditQuery.data
                  : current === 'bcm'
                    ? (bcmQuery.data?.dashboard as Record<string, unknown> | undefined)
                    : current === 'vendors'
                      ? (vendorQuery.data?.dashboard as Record<string, unknown> | undefined)
                      : null

  const generatedAt =
    (activeData as { generatedAtUtc?: string } | null)?.generatedAtUtc ??
    execQuery.data?.generatedAtUtc ??
    null
  const source =
    (activeData as { source?: string } | null)?.source ??
    execQuery.data?.source ??
    'live'
  const note =
    (activeData as { note?: string } | null)?.note ??
    execQuery.data?.note ??
    null

  if (sections.length === 0) {
    return <p className="text-sm text-muted-foreground">{t('reports.noAccess')}</p>
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t('reports.title')} description={t('reports.description')} />
      <div className="flex flex-wrap gap-2">
        {sections.map((key) => (
          <Button
            key={key}
            type="button"
            size="sm"
            variant={current === key ? 'default' : 'outline'}
            onClick={() => setSection(key)}
          >
            {t(`reports.sections.${key}`)}
          </Button>
        ))}
      </div>

      <div className="flex flex-wrap items-end gap-2">
        <div>
          <label className="text-xs text-muted-foreground">{t('reports.from')}</label>
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-[160px]" />
        </div>
        <div>
          <label className="text-xs text-muted-foreground">{t('reports.to')}</label>
          <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-[160px]" />
        </div>
        {can('report.export') ? (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => {
              window.location.href = reportsApi.exportUrl(current === 'executive' ? 'executive' : current, range)
            }}
          >
            {t('reports.exportCsv')}
          </Button>
        ) : null}
      </div>

      <div className="flex flex-wrap gap-2 text-sm text-muted-foreground">
        {generatedAt ? (
          <span>
            {t('reports.generatedAt')}: {new Date(generatedAt).toLocaleString()}
          </span>
        ) : null}
        <Badge variant="secondary">{source === 'live' ? t('reports.live') : t('reports.snapshot')}</Badge>
      </div>
      {note ? <p className="text-sm text-muted-foreground">{note}</p> : null}

      {current === 'executive' && execQuery.data ? (
        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {Object.entries(execQuery.data.tiles).map(([key, tile]) => (
              <Card key={key}>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm font-medium">{key}</CardTitle>
                </CardHeader>
                <CardContent className="space-y-1 text-sm">
                  {Object.entries(tile as Record<string, unknown>)
                    .filter(([k]) => !['generatedAtUtc', 'source'].includes(k))
                    .map(([k, v]) => (
                      <div key={k} className="flex justify-between gap-2">
                        <span className="text-muted-foreground">{k}</span>
                        <span className="font-semibold tabular-nums">{v == null ? 'N/A' : String(v)}</span>
                      </div>
                    ))}
                </CardContent>
              </Card>
            ))}
          </div>
          {(snapQuery.data?.length ?? 0) > 0 ? (
            <div>
              <h3 className="mb-2 text-sm font-medium">{t('reports.snapshotTrend')}</h3>
              <ul className="space-y-1 text-sm text-muted-foreground">
                {snapQuery.data!.map((s) => (
                  <li key={s.id}>
                    {new Date(s.snapshotDateUtc).toLocaleDateString()} · {s.snapshotKey}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      ) : null}

      {current !== 'executive' ? <MetricGrid data={activeData as Record<string, unknown>} /> : null}

      {current === 'compliance' && compQuery.data ? (
        <div className="space-y-2 text-sm">
          <p>{t('reports.complianceHonest')}</p>
          {compQuery.data.assessmentResults ? (
            <pre className="overflow-auto rounded-md bg-muted p-3 text-xs">
              {JSON.stringify(compQuery.data.assessmentResults, null, 2)}
            </pre>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}

export function ReportsExecutivePage() {
  return <ReportsPage />
}
