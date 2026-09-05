import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import {
  securityApi,
  type VulnerabilityItem,
  type RiskItem,
  type PolicyExceptionItem,
  type PentestItem,
  type AwarenessCampaignItem,
} from '@/api/client'
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

type Section = 'dashboard' | 'vulnerabilities' | 'risks' | 'exceptions' | 'pentests' | 'awareness'

export function SecurityHomePage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const [section, setSection] = useState<Section>('dashboard')
  const [search, setSearch] = useState('')
  const [vulnTitle, setVulnTitle] = useState('')
  const [ciId, setCiId] = useState('')
  const [riskTitle, setRiskTitle] = useState('')
  const [riskDesc, setRiskDesc] = useState('')
  const [excTitle, setExcTitle] = useState('')
  const [excReason, setExcReason] = useState('')
  const [pentestTitle, setPentestTitle] = useState('')
  const [pentestScope, setPentestScope] = useState('')
  const [campaignTitle, setCampaignTitle] = useState('')

  const dashQuery = useQuery({
    queryKey: ['security', 'dashboard'],
    queryFn: () => securityApi.dashboard(),
    enabled: section === 'dashboard',
  })
  const vulnQuery = useQuery({
    queryKey: ['security', 'vulnerabilities', search],
    queryFn: () => securityApi.listVulnerabilities({ pageSize: 50, search: search || undefined }),
    enabled: section === 'vulnerabilities',
  })
  const riskQuery = useQuery({
    queryKey: ['security', 'risks'],
    queryFn: () => securityApi.listRisks(),
    enabled: section === 'risks',
  })
  const excQuery = useQuery({
    queryKey: ['security', 'exceptions'],
    queryFn: () => securityApi.listExceptions(),
    enabled: section === 'exceptions',
  })
  const pentestQuery = useQuery({
    queryKey: ['security', 'pentests'],
    queryFn: () => securityApi.listPentests(),
    enabled: section === 'pentests',
  })
  const awarenessQuery = useQuery({
    queryKey: ['security', 'awareness'],
    queryFn: () => securityApi.listAwareness(),
    enabled: section === 'awareness',
  })

  const qc = useQueryClient()
  const refresh = async (key: string) => {
    await qc.invalidateQueries({ queryKey: ['security', key] })
  }

  const vulnColumns = useMemo<ColumnDef<VulnerabilityItem, unknown>[]>(
    () => [
      { accessorKey: 'vulnerabilityNumber', header: t('security.columns.number') },
      { accessorKey: 'title', header: t('security.columns.title') },
      { accessorKey: 'severity', header: t('security.columns.severity') },
      {
        accessorKey: 'status',
        header: t('security.columns.status'),
        cell: ({ row }) => (
          <Badge variant={row.original.isOverdue ? 'warning' : 'secondary'}>{row.original.status}</Badge>
        ),
      },
      { accessorKey: 'source', header: t('security.columns.source') },
    ],
    [t],
  )

  const sections: [Section, string][] = [
    ['dashboard', 'security.sections.dashboard'],
    ['vulnerabilities', 'security.sections.vulnerabilities'],
    ['risks', 'security.sections.risks'],
    ['exceptions', 'security.sections.exceptions'],
    ['pentests', 'security.sections.pentests'],
    ['awareness', 'security.sections.awareness'],
  ]

  return (
    <div className="space-y-6">
      <PageHeader title={t('security.title')} description={t('security.description')} />
      <div className="flex flex-wrap gap-2">
        {sections.map(([key, label]) => (
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

      {section === 'dashboard' && dashQuery.data ? (
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">{dashQuery.data.note}</p>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {(
              [
                [t('security.dash.openVulns'), dashQuery.data.openVulnerabilities],
                [t('security.dash.criticalHigh'), dashQuery.data.criticalHighVulnerabilities],
                [t('security.dash.overdue'), dashQuery.data.overdueRemediation],
                [t('security.dash.securityIncidents'), dashQuery.data.openSecurityIncidents],
                [t('security.dash.openExceptions'), dashQuery.data.openExceptions],
                [t('security.dash.expiringExceptions'), dashQuery.data.expiringExceptions],
                [t('security.dash.openRisks'), dashQuery.data.openRisks],
                [t('security.dash.highResidual'), dashQuery.data.highResidualRisks],
                [t('security.dash.pentestFindings'), dashQuery.data.pentestOpenFindings],
                [t('security.dash.awarenessOutstanding'), dashQuery.data.awarenessOutstanding],
              ] as const
            ).map(([label, value]) => (
              <Card key={label}>
                <CardHeader className="pb-2">
                  <CardTitle className="text-sm font-medium">{label}</CardTitle>
                </CardHeader>
                <CardContent className="text-2xl font-semibold tabular-nums">{value}</CardContent>
              </Card>
            ))}
          </div>
        </div>
      ) : null}

      {section === 'vulnerabilities' ? (
        <div className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <Input
              className="max-w-xs"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('security.searchPlaceholder')}
            />
            {can('vuln.manage') ? (
              <>
                <Input
                  className="max-w-xs"
                  value={vulnTitle}
                  onChange={(e) => setVulnTitle(e.target.value)}
                  placeholder={t('security.fields.title')}
                />
                <Input
                  className="max-w-xs"
                  value={ciId}
                  onChange={(e) => setCiId(e.target.value)}
                  placeholder={t('security.fields.ciId')}
                />
                <Button
                  type="button"
                  disabled={!vulnTitle.trim() || !ciId}
                  onClick={async () => {
                    const created = await securityApi.createVulnerability({
                      title: vulnTitle,
                      configurationItemId: ciId,
                      severity: 'Medium',
                      source: 'Manual',
                    })
                    setVulnTitle('')
                    await refresh('vulnerabilities')
                    navigate(`/it/security/vulnerabilities/${created.id}`)
                  }}
                >
                  {t('security.createVuln')}
                </Button>
              </>
            ) : null}
          </div>
          <DataTable
            columns={vulnColumns}
            data={vulnQuery.data?.items ?? []}
            emptyMessage={t('security.empty')}
            isLoading={vulnQuery.isLoading}
            onRowClick={(row) => navigate(`/it/security/vulnerabilities/${row.id}`)}
          />
        </div>
      ) : null}

      {section === 'risks' ? (
        <RisksPanel
          items={riskQuery.data?.items ?? []}
          canManage={can('risk.manage')}
          title={riskTitle}
          setTitle={setRiskTitle}
          desc={riskDesc}
          setDesc={setRiskDesc}
          onCreate={async () => {
            await securityApi.createRisk({
              title: riskTitle,
              description: riskDesc,
              category: 'Security',
              likelihood: 3,
              impact: 3,
            })
            setRiskTitle('')
            setRiskDesc('')
            await refresh('risks')
          }}
          onTransition={async (id, status) => {
            await securityApi.transitionRisk(id, status)
            await refresh('risks')
          }}
        />
      ) : null}

      {section === 'exceptions' ? (
        <ExceptionsPanel
          items={excQuery.data ?? []}
          canManage={can('risk.manage')}
          canApprove={can('exception.approve')}
          title={excTitle}
          setTitle={setExcTitle}
          reason={excReason}
          setReason={setExcReason}
          onCreate={async () => {
            const start = new Date()
            const end = new Date(Date.now() + 90 * 24 * 60 * 60 * 1000)
            await securityApi.createException({
              title: excTitle,
              reason: excReason,
              startAtUtc: start.toISOString(),
              expiresAtUtc: end.toISOString(),
            })
            setExcTitle('')
            setExcReason('')
            await refresh('exceptions')
          }}
          onSubmit={async (id) => {
            await securityApi.submitException(id)
            await refresh('exceptions')
          }}
          onApprove={async (id) => {
            await securityApi.approveException(id)
            await refresh('exceptions')
          }}
          onReject={async (id) => {
            await securityApi.rejectException(id, 'Rejected')
            await refresh('exceptions')
          }}
        />
      ) : null}

      {section === 'pentests' ? (
        <PentestsPanel
          items={pentestQuery.data ?? []}
          canManage={can('vuln.manage')}
          title={pentestTitle}
          setTitle={setPentestTitle}
          scope={pentestScope}
          setScope={setPentestScope}
          onCreate={async () => {
            await securityApi.createPentest({ title: pentestTitle, scopeSummary: pentestScope })
            setPentestTitle('')
            setPentestScope('')
            await refresh('pentests')
          }}
        />
      ) : null}

      {section === 'awareness' ? (
        <AwarenessPanel
          items={awarenessQuery.data ?? []}
          canManage={can('risk.manage')}
          title={campaignTitle}
          setTitle={setCampaignTitle}
          onCreate={async () => {
            await securityApi.createAwareness({ title: campaignTitle })
            setCampaignTitle('')
            await refresh('awareness')
          }}
          onOpen={async (id) => {
            await securityApi.openAwareness(id)
            await refresh('awareness')
          }}
        />
      ) : null}
    </div>
  )
}

function RisksPanel({
  items,
  canManage,
  title,
  setTitle,
  desc,
  setDesc,
  onCreate,
  onTransition,
}: {
  items: RiskItem[]
  canManage: boolean
  title: string
  setTitle: (v: string) => void
  desc: string
  setDesc: (v: string) => void
  onCreate: () => Promise<void>
  onTransition: (id: string, status: string) => Promise<void>
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-4">
      {items.map((r) => (
        <Card key={r.id}>
          <CardContent className="flex flex-wrap items-center justify-between gap-2 py-4">
            <div>
              <p className="font-medium">
                {r.riskNumber} · {r.title}
              </p>
              <p className="text-sm text-muted-foreground">
                {r.status} · score {r.inherentScore}
                {r.residualScore != null ? ` / residual ${r.residualScore}` : ''}
              </p>
            </div>
            {canManage && r.status === 'Identified' ? (
              <Button type="button" size="sm" onClick={() => onTransition(r.id, 'Analyzed')}>
                Analyzed
              </Button>
            ) : null}
          </CardContent>
        </Card>
      ))}
      {canManage ? (
        <div className="flex flex-wrap gap-2">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('security.fields.title')} />
          <Input value={desc} onChange={(e) => setDesc(e.target.value)} placeholder={t('security.fields.description')} />
          <Button type="button" disabled={!title.trim() || !desc.trim()} onClick={() => onCreate()}>
            {t('security.createRisk')}
          </Button>
        </div>
      ) : null}
    </div>
  )
}

function ExceptionsPanel({
  items,
  canManage,
  canApprove,
  title,
  setTitle,
  reason,
  setReason,
  onCreate,
  onSubmit,
  onApprove,
  onReject,
}: {
  items: PolicyExceptionItem[]
  canManage: boolean
  canApprove: boolean
  title: string
  setTitle: (v: string) => void
  reason: string
  setReason: (v: string) => void
  onCreate: () => Promise<void>
  onSubmit: (id: string) => Promise<void>
  onApprove: (id: string) => Promise<void>
  onReject: (id: string) => Promise<void>
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-4">
      {items.map((e) => (
        <Card key={e.id}>
          <CardContent className="space-y-2 py-4">
            <p className="font-medium">
              {e.exceptionNumber} · {e.title}
            </p>
            <p className="text-sm text-muted-foreground">
              {e.status}
              {e.daysToExpiry != null ? ` · ${e.daysToExpiry}d` : ''}
              {e.isExpired ? ` · ${t('security.expired')}` : ''}
            </p>
            <div className="flex flex-wrap gap-2">
              {canManage && e.status === 'Draft' ? (
                <Button type="button" size="sm" onClick={() => onSubmit(e.id)}>
                  {t('security.submit')}
                </Button>
              ) : null}
              {canApprove && e.status === 'PendingApproval' ? (
                <>
                  <Button type="button" size="sm" onClick={() => onApprove(e.id)}>
                    {t('security.approve')}
                  </Button>
                  <Button type="button" size="sm" variant="outline" onClick={() => onReject(e.id)}>
                    {t('security.reject')}
                  </Button>
                </>
              ) : null}
            </div>
          </CardContent>
        </Card>
      ))}
      {canManage ? (
        <div className="flex flex-wrap gap-2">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('security.fields.title')} />
          <Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder={t('security.fields.reason')} />
          <Button type="button" disabled={!title.trim() || !reason.trim()} onClick={() => onCreate()}>
            {t('security.createException')}
          </Button>
        </div>
      ) : null}
    </div>
  )
}

function PentestsPanel({
  items,
  canManage,
  title,
  setTitle,
  scope,
  setScope,
  onCreate,
}: {
  items: PentestItem[]
  canManage: boolean
  title: string
  setTitle: (v: string) => void
  scope: string
  setScope: (v: string) => void
  onCreate: () => Promise<void>
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-4">
      {items.map((p) => (
        <Card key={p.id}>
          <CardContent className="py-4">
            <p className="font-medium">
              {p.pentestNumber} · {p.title}
            </p>
            <p className="text-sm text-muted-foreground">
              {p.status} · {p.scopeSummary}
            </p>
          </CardContent>
        </Card>
      ))}
      {canManage ? (
        <div className="flex flex-wrap gap-2">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('security.fields.title')} />
          <Input value={scope} onChange={(e) => setScope(e.target.value)} placeholder={t('security.fields.scope')} />
          <Button type="button" disabled={!title.trim() || !scope.trim()} onClick={() => onCreate()}>
            {t('security.createPentest')}
          </Button>
        </div>
      ) : null}
    </div>
  )
}

function AwarenessPanel({
  items,
  canManage,
  title,
  setTitle,
  onCreate,
  onOpen,
}: {
  items: AwarenessCampaignItem[]
  canManage: boolean
  title: string
  setTitle: (v: string) => void
  onCreate: () => Promise<void>
  onOpen: (id: string) => Promise<void>
}) {
  const { t } = useTranslation()
  return (
    <div className="space-y-4">
      {items.map((c) => (
        <Card key={c.id}>
          <CardContent className="flex flex-wrap items-center justify-between gap-2 py-4">
            <div>
              <p className="font-medium">{c.title}</p>
              <p className="text-sm text-muted-foreground">
                {c.status} · {t('security.awareness.assigned')}: {c.assignedCount} ·{' '}
                {t('security.awareness.completed')}: {c.completedCount} ·{' '}
                {t('security.awareness.outstanding')}: {c.outstandingCount}
                {c.overdueCount ? ` · ${t('security.awareness.overdue')}: ${c.overdueCount}` : ''}
              </p>
            </div>
            {canManage && c.status === 'Draft' ? (
              <Button type="button" size="sm" onClick={() => onOpen(c.id)}>
                {t('security.openCampaign')}
              </Button>
            ) : null}
          </CardContent>
        </Card>
      ))}
      {canManage ? (
        <div className="flex flex-wrap gap-2">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t('security.fields.title')} />
          <Button type="button" disabled={!title.trim()} onClick={() => onCreate()}>
            {t('security.createCampaign')}
          </Button>
        </div>
      ) : null}
    </div>
  )
}

export function VulnerabilityDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [linkType, setLinkType] = useState('Ticket')
  const [targetId, setTargetId] = useState('')
  const [resolution, setResolution] = useState('')
  const [exceptionId, setExceptionId] = useState('')

  const detailQuery = useQuery({
    queryKey: ['security', 'vulnerabilities', id],
    queryFn: () => securityApi.getVulnerability(id),
    enabled: Boolean(id),
  })
  const linksQuery = useQuery({
    queryKey: ['security', 'vulnerabilities', id, 'links'],
    queryFn: () => securityApi.listRemediationLinks(id),
    enabled: Boolean(id),
  })

  const refresh = async () => {
    await qc.invalidateQueries({ queryKey: ['security', 'vulnerabilities', id] })
  }

  const transitionMutation = useMutation({
    mutationFn: (payload: {
      status: string
      resolutionSummary?: string
      acceptedRiskReason?: string
      exceptionId?: string
    }) => securityApi.transitionVulnerability(id, payload),
    onSuccess: refresh,
  })

  const item = detailQuery.data
  if (detailQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('security.loading')}</p>
  if (!item) return <p className="text-sm text-muted-foreground">{t('security.notFound')}</p>

  return (
    <div className="space-y-6">
      <PageHeader
        title={item.vulnerabilityNumber}
        description={item.title}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/security">{t('security.back')}</Link>
          </Button>
        }
      />
      <div className="flex flex-wrap gap-2">
        <Badge>{item.status}</Badge>
        <Badge variant="outline">{item.severity}</Badge>
        {item.isOverdue ? <Badge variant="warning">{t('security.overdue')}</Badge> : null}
      </div>
      <Card>
        <CardContent className="space-y-2 py-4 text-sm">
          <p>{item.description ?? '—'}</p>
          <p className="text-muted-foreground">
            CI {item.configurationItemId} · {item.source}
          </p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>{t('security.remediation')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {(linksQuery.data ?? []).map((l) => (
            <p key={l.id} className="text-sm">
              {l.linkType}: {l.targetId}
            </p>
          ))}
          {can('vuln.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Select value={linkType} onValueChange={setLinkType}>
                <SelectTrigger className="w-40">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {['ChangeRequest', 'Ticket', 'Finding', 'CorrectiveAction'].map((x) => (
                    <SelectItem key={x} value={x}>
                      {x}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input
                className="max-w-sm"
                value={targetId}
                onChange={(e) => setTargetId(e.target.value)}
                placeholder={t('security.fields.targetId')}
              />
              <Button
                type="button"
                disabled={!targetId}
                onClick={async () => {
                  await securityApi.addRemediationLink(id, linkType, targetId)
                  setTargetId('')
                  await refresh()
                }}
              >
                {t('security.addLink')}
              </Button>
            </div>
          ) : null}
        </CardContent>
      </Card>
      {can('vuln.manage') && item.status !== 'Resolved' && item.status !== 'AcceptedRisk' && item.status !== 'FalsePositive' ? (
        <div className="flex flex-wrap gap-2">
          <Input
            className="max-w-sm"
            value={resolution}
            onChange={(e) => setResolution(e.target.value)}
            placeholder={t('security.fields.resolution')}
          />
          <Button
            type="button"
            onClick={() =>
              transitionMutation.mutate({ status: 'Resolved', resolutionSummary: resolution })
            }
          >
            Resolve
          </Button>
          <Input
            className="max-w-sm"
            value={exceptionId}
            onChange={(e) => setExceptionId(e.target.value)}
            placeholder={t('security.fields.exceptionId')}
          />
          <Button
            type="button"
            variant="secondary"
            onClick={() =>
              transitionMutation.mutate({
                status: 'AcceptedRisk',
                acceptedRiskReason: resolution || 'Accepted with exception',
                exceptionId,
              })
            }
          >
            AcceptedRisk
          </Button>
          <Button
            type="button"
            variant="outline"
            onClick={() =>
              transitionMutation.mutate({
                status: 'FalsePositive',
                acceptedRiskReason: resolution || 'False positive',
              })
            }
          >
            FalsePositive
          </Button>
        </div>
      ) : null}
    </div>
  )
}
