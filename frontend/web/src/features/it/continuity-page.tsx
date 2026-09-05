import { useMemo, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import {
  cmdbApi,
  continuityApi,
  type BiaItem,
  type ContinuityPlanItem,
  type DrTestItem,
  type RecoveryProcedureItem,
  type ServiceReadinessRow,
  type ConfigurationItem,
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

type Section = 'dashboard' | 'bia' | 'plans' | 'procedures' | 'tests' | 'spof' | 'readiness'

function sectionFromPath(pathname: string): Section {
  if (pathname.includes('/bia')) return 'bia'
  if (pathname.includes('/plans')) return 'plans'
  if (pathname.includes('/procedures')) return 'procedures'
  if (pathname.includes('/tests')) return 'tests'
  return 'dashboard'
}

export function ContinuityHomePage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [section, setSection] = useState<Section>(() => sectionFromPath(location.pathname))
  const [biaSummary, setBiaSummary] = useState('')
  const [biaServiceId, setBiaServiceId] = useState('')
  const [planTitle, setPlanTitle] = useState('')
  const [planType, setPlanType] = useState('BusinessContinuity')
  const [procTitle, setProcTitle] = useState('')
  const [procPlanId, setProcPlanId] = useState('')
  const [testTitle, setTestTitle] = useState('')
  const [testServiceId, setTestServiceId] = useState('')
  const [spofCiId, setSpofCiId] = useState('')
  const [spofReason, setSpofReason] = useState('')

  const servicesQuery = useQuery({
    queryKey: ['cmdb', 'services'],
    queryFn: () => cmdbApi.listServices(),
  })
  const dashQuery = useQuery({
    queryKey: ['continuity', 'dashboard'],
    queryFn: () => continuityApi.dashboard(),
    enabled: section === 'dashboard',
  })
  const readinessQuery = useQuery({
    queryKey: ['continuity', 'readiness'],
    queryFn: () => continuityApi.readiness(),
    enabled: section === 'readiness' || section === 'dashboard',
  })
  const biaQuery = useQuery({
    queryKey: ['continuity', 'bia'],
    queryFn: () => continuityApi.listBia(),
    enabled: section === 'bia',
  })
  const plansQuery = useQuery({
    queryKey: ['continuity', 'plans'],
    queryFn: () => continuityApi.listPlans(),
    enabled: section === 'plans' || section === 'procedures',
  })
  const procQuery = useQuery({
    queryKey: ['continuity', 'procedures'],
    queryFn: () => continuityApi.listProcedures(),
    enabled: section === 'procedures',
  })
  const testsQuery = useQuery({
    queryKey: ['continuity', 'tests'],
    queryFn: () => continuityApi.listTests(),
    enabled: section === 'tests',
  })
  const spofQuery = useQuery({
    queryKey: ['continuity', 'spofs'],
    queryFn: () => continuityApi.listSpofs(),
    enabled: section === 'spof',
  })

  const qc = useQueryClient()
  const refresh = async (...keys: string[]) => {
    for (const key of keys) await qc.invalidateQueries({ queryKey: ['continuity', key] })
  }

  const sections: [Section, string][] = [
    ['dashboard', 'continuity.sections.dashboard'],
    ['bia', 'continuity.sections.bia'],
    ['plans', 'continuity.sections.plans'],
    ['procedures', 'continuity.sections.procedures'],
    ['tests', 'continuity.sections.tests'],
    ['spof', 'continuity.sections.spof'],
    ['readiness', 'continuity.sections.readiness'],
  ]

  const biaColumns = useMemo<ColumnDef<BiaItem, unknown>[]>(
    () => [
      { accessorKey: 'biaNumber', header: t('continuity.columns.number') },
      { accessorKey: 'criticality', header: t('continuity.columns.criticality') },
      { accessorKey: 'status', header: t('continuity.columns.status') },
      { accessorKey: 'businessImpactSummary', header: t('continuity.columns.summary') },
    ],
    [t],
  )
  const planColumns = useMemo<ColumnDef<ContinuityPlanItem, unknown>[]>(
    () => [
      { accessorKey: 'planNumber', header: t('continuity.columns.number') },
      { accessorKey: 'title', header: t('continuity.columns.title') },
      { accessorKey: 'planType', header: t('continuity.columns.type') },
      {
        accessorKey: 'status',
        header: t('continuity.columns.status'),
        cell: ({ row }) => (
          <Badge variant={row.original.isReviewOverdue ? 'warning' : 'secondary'}>
            {row.original.isReviewOverdue ? t('continuity.reviewOverdue') : row.original.status}
          </Badge>
        ),
      },
    ],
    [t],
  )
  const procColumns = useMemo<ColumnDef<RecoveryProcedureItem, unknown>[]>(
    () => [
      { accessorKey: 'procedureNumber', header: t('continuity.columns.number') },
      { accessorKey: 'title', header: t('continuity.columns.title') },
      { accessorKey: 'recoveryStage', header: t('continuity.columns.stage') },
      { accessorKey: 'sequence', header: t('continuity.columns.sequence') },
    ],
    [t],
  )
  const testColumns = useMemo<ColumnDef<DrTestItem, unknown>[]>(
    () => [
      { accessorKey: 'drTestNumber', header: t('continuity.columns.number') },
      { accessorKey: 'title', header: t('continuity.columns.title') },
      { accessorKey: 'testType', header: t('continuity.columns.type') },
      { accessorKey: 'status', header: t('continuity.columns.status') },
      { accessorKey: 'result', header: t('continuity.columns.result') },
    ],
    [t],
  )
  const readinessColumns = useMemo<ColumnDef<ServiceReadinessRow, unknown>[]>(
    () => [
      { accessorKey: 'serviceName', header: t('continuity.columns.service') },
      { accessorKey: 'rtoMinutes', header: t('continuity.columns.rto') },
      { accessorKey: 'rpoMinutes', header: t('continuity.columns.rpo') },
      { accessorKey: 'biaStatus', header: t('continuity.columns.biaStatus') },
      { accessorKey: 'planStatus', header: t('continuity.columns.planStatus') },
      { accessorKey: 'latestDrTestNumber', header: t('continuity.columns.latestTest') },
      { accessorKey: 'latestDrTestResult', header: t('continuity.columns.result') },
      { accessorKey: 'spofCount', header: t('continuity.columns.spofCount') },
    ],
    [t],
  )
  const spofColumns = useMemo<ColumnDef<ConfigurationItem, unknown>[]>(
    () => [
      { accessorKey: 'ciNumber', header: t('continuity.columns.number') },
      { accessorKey: 'name', header: t('continuity.columns.title') },
      { accessorKey: 'spofReason', header: t('continuity.columns.reason') },
      { accessorKey: 'spofMitigationNotes', header: t('continuity.columns.mitigation') },
    ],
    [t],
  )

  const serviceOptions = servicesQuery.data ?? []

  return (
    <div className="space-y-6">
      <PageHeader title={t('continuity.title')} description={t('continuity.description')} />
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
                [t('continuity.dash.criticalServices'), dashQuery.data.criticalServices],
                [t('continuity.dash.withoutBia'), dashQuery.data.servicesWithoutApprovedBia],
                [t('continuity.dash.withoutPlan'), dashQuery.data.servicesWithoutActivePlan],
                [t('continuity.dash.missingDrTest'), dashQuery.data.servicesMissingRecentDrTest],
                [t('continuity.dash.upcomingTests'), dashQuery.data.upcomingDrTests],
                [t('continuity.dash.overdueTests'), dashQuery.data.overdueDrTests],
                [t('continuity.dash.passed'), dashQuery.data.drPassed],
                [t('continuity.dash.passedGaps'), dashQuery.data.drPassedWithGaps],
                [t('continuity.dash.failed'), dashQuery.data.drFailed],
                [t('continuity.dash.rtoMisses'), dashQuery.data.rtoMisses],
                [t('continuity.dash.rpoMisses'), dashQuery.data.rpoMisses],
                [t('continuity.dash.spofs'), dashQuery.data.confirmedSpofs],
                [t('continuity.dash.plansOverdue'), dashQuery.data.plansOverdueReview],
                [t('continuity.dash.openRisks'), dashQuery.data.openBcmLinkedRisks],
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

      {section === 'bia' ? (
        <div className="space-y-4">
          {can('bcm.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Select value={biaServiceId || undefined} onValueChange={setBiaServiceId}>
                <SelectTrigger className="w-[220px]">
                  <SelectValue placeholder={t('continuity.fields.service')} />
                </SelectTrigger>
                <SelectContent>
                  {serviceOptions.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input
                className="max-w-md"
                value={biaSummary}
                onChange={(e) => setBiaSummary(e.target.value)}
                placeholder={t('continuity.fields.impactSummary')}
              />
              <Button
                type="button"
                disabled={!biaServiceId || !biaSummary.trim()}
                onClick={async () => {
                  const created = await continuityApi.createBia({
                    businessServiceId: biaServiceId,
                    businessImpactSummary: biaSummary.trim(),
                  })
                  setBiaSummary('')
                  await refresh('bia', 'dashboard', 'readiness')
                  navigate(`/it/continuity/bia/${created.id}`)
                }}
              >
                {t('continuity.createBia')}
              </Button>
            </div>
          ) : null}
          <DataTable
            columns={biaColumns}
            data={biaQuery.data ?? []}
            onRowClick={(row) => navigate(`/it/continuity/bia/${row.id}`)}
          />
        </div>
      ) : null}

      {section === 'plans' ? (
        <div className="space-y-4">
          {can('bcm.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Input
                className="max-w-xs"
                value={planTitle}
                onChange={(e) => setPlanTitle(e.target.value)}
                placeholder={t('continuity.fields.title')}
              />
              <Select value={planType} onValueChange={setPlanType}>
                <SelectTrigger className="w-[200px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="BusinessContinuity">BCP</SelectItem>
                  <SelectItem value="ITDisasterRecovery">DRP</SelectItem>
                </SelectContent>
              </Select>
              <Button
                type="button"
                disabled={!planTitle.trim()}
                onClick={async () => {
                  const created = await continuityApi.createPlan({ title: planTitle.trim(), planType })
                  setPlanTitle('')
                  await refresh('plans', 'dashboard')
                  navigate(`/it/continuity/plans/${created.id}`)
                }}
              >
                {t('continuity.createPlan')}
              </Button>
            </div>
          ) : null}
          <DataTable
            columns={planColumns}
            data={plansQuery.data ?? []}
            onRowClick={(row) => navigate(`/it/continuity/plans/${row.id}`)}
          />
        </div>
      ) : null}

      {section === 'procedures' ? (
        <div className="space-y-4">
          {can('bcm.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Select value={procPlanId || undefined} onValueChange={setProcPlanId}>
                <SelectTrigger className="w-[220px]">
                  <SelectValue placeholder={t('continuity.fields.plan')} />
                </SelectTrigger>
                <SelectContent>
                  {(plansQuery.data ?? []).map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.planNumber}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input
                className="max-w-xs"
                value={procTitle}
                onChange={(e) => setProcTitle(e.target.value)}
                placeholder={t('continuity.fields.title')}
              />
              <Button
                type="button"
                disabled={!procPlanId || !procTitle.trim()}
                onClick={async () => {
                  await continuityApi.createProcedure({
                    continuityPlanId: procPlanId,
                    title: procTitle.trim(),
                  })
                  setProcTitle('')
                  await refresh('procedures')
                }}
              >
                {t('continuity.createProcedure')}
              </Button>
            </div>
          ) : null}
          <DataTable columns={procColumns} data={procQuery.data ?? []} />
        </div>
      ) : null}

      {section === 'tests' ? (
        <div className="space-y-4">
          {can('dr.test.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Input
                className="max-w-xs"
                value={testTitle}
                onChange={(e) => setTestTitle(e.target.value)}
                placeholder={t('continuity.fields.title')}
              />
              <Select value={testServiceId || undefined} onValueChange={setTestServiceId}>
                <SelectTrigger className="w-[220px]">
                  <SelectValue placeholder={t('continuity.fields.service')} />
                </SelectTrigger>
                <SelectContent>
                  {serviceOptions.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button
                type="button"
                disabled={!testTitle.trim() || !testServiceId}
                onClick={async () => {
                  const created = await continuityApi.createTest({
                    title: testTitle.trim(),
                    businessServiceId: testServiceId,
                    plannedAtUtc: new Date().toISOString(),
                  })
                  setTestTitle('')
                  await refresh('tests', 'dashboard')
                  navigate(`/it/continuity/tests/${created.id}`)
                }}
              >
                {t('continuity.createTest')}
              </Button>
            </div>
          ) : null}
          <DataTable
            columns={testColumns}
            data={testsQuery.data ?? []}
            onRowClick={(row) => navigate(`/it/continuity/tests/${row.id}`)}
          />
        </div>
      ) : null}

      {section === 'spof' ? (
        <div className="space-y-4">
          <p className="text-sm text-muted-foreground">{t('continuity.spofHint')}</p>
          {can('bcm.manage') ? (
            <div className="flex flex-wrap gap-2">
              <Input
                className="max-w-xs"
                value={spofCiId}
                onChange={(e) => setSpofCiId(e.target.value)}
                placeholder={t('continuity.fields.ciId')}
              />
              <Input
                className="max-w-md"
                value={spofReason}
                onChange={(e) => setSpofReason(e.target.value)}
                placeholder={t('continuity.fields.reason')}
              />
              <Button
                type="button"
                disabled={!spofCiId || !spofReason.trim()}
                onClick={async () => {
                  const ci = await cmdbApi.getCi(spofCiId.trim())
                  await continuityApi.setSpof(ci.id, {
                    isSinglePointOfFailure: true,
                    reason: spofReason.trim(),
                    confirmed: true,
                    rowVersion: ci.rowVersion,
                  })
                  setSpofCiId('')
                  setSpofReason('')
                  await refresh('spofs', 'dashboard', 'readiness')
                }}
              >
                {t('continuity.confirmSpof')}
              </Button>
            </div>
          ) : null}
          <DataTable columns={spofColumns} data={spofQuery.data?.items ?? []} />
        </div>
      ) : null}

      {section === 'readiness' ? (
        <DataTable columns={readinessColumns} data={readinessQuery.data ?? []} />
      ) : null}
    </div>
  )
}

export function BiaDetailPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [linkType, setLinkType] = useState('Risk')
  const [linkId, setLinkId] = useState('')

  const query = useQuery({
    queryKey: ['continuity', 'bia', id],
    queryFn: () => continuityApi.getBia(id),
    enabled: !!id,
  })

  if (query.isLoading) return <p>{t('continuity.loading')}</p>
  if (!query.data) return <p>{t('continuity.notFound')}</p>
  const { bia, businessService, linkedConfigurationItemIds, links } = query.data

  return (
    <div className="space-y-6">
      <PageHeader
        title={bia.biaNumber}
        description={bia.businessImpactSummary}
        actions={
          <Link to="/it/continuity" className="text-sm text-primary underline">
            {t('continuity.back')}
          </Link>
        }
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">{t('continuity.biaMeta')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p>
              {t('continuity.columns.status')}: {bia.status}
            </p>
            <p>
              {t('continuity.columns.criticality')}: {bia.criticality}
            </p>
            {bia.maximumTolerableDowntimeMinutes != null ? (
              <p>
                MTD: {bia.maximumTolerableDowntimeMinutes} min
              </p>
            ) : null}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">{t('continuity.serviceTargets')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p>{businessService?.name ?? bia.businessServiceId}</p>
            <p>
              RTO: {businessService?.rtoMinutes ?? '—'} / RPO: {businessService?.rpoMinutes ?? '—'}
            </p>
            <p>
              {t('continuity.linkedCis')}: {linkedConfigurationItemIds.length}
            </p>
          </CardContent>
        </Card>
      </div>
      {can('bcm.manage') ? (
        <div className="flex flex-wrap gap-2">
          {['InReview', 'Approved', 'Retired'].map((status) => (
            <Button
              key={status}
              type="button"
              size="sm"
              variant="outline"
              onClick={async () => {
                await continuityApi.transitionBia(id, status)
                await qc.invalidateQueries({ queryKey: ['continuity', 'bia', id] })
              }}
            >
              {status}
            </Button>
          ))}
          <Input
            className="max-w-xs"
            value={linkId}
            onChange={(e) => setLinkId(e.target.value)}
            placeholder={t('continuity.fields.targetId')}
          />
          <Select value={linkType} onValueChange={setLinkType}>
            <SelectTrigger className="w-[180px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {['ConfigurationItem', 'Risk', 'InternalControl', 'ManagedDocument'].map((x) => (
                <SelectItem key={x} value={x}>
                  {x}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            type="button"
            size="sm"
            disabled={!linkId}
            onClick={async () => {
              await continuityApi.addBiaLink(id, linkType, linkId.trim())
              setLinkId('')
              await qc.invalidateQueries({ queryKey: ['continuity', 'bia', id] })
            }}
          >
            {t('continuity.addLink')}
          </Button>
        </div>
      ) : null}
      <ul className="text-sm text-muted-foreground">
        {links.map((l) => (
          <li key={l.id}>
            {l.targetType}: {l.targetId}
          </li>
        ))}
      </ul>
    </div>
  )
}

export function ContinuityPlanDetailPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [procTitle, setProcTitle] = useState('')
  const [linkType, setLinkType] = useState('BusinessService')
  const [linkId, setLinkId] = useState('')

  const query = useQuery({
    queryKey: ['continuity', 'plans', id],
    queryFn: () => continuityApi.getPlan(id),
    enabled: !!id,
  })

  if (query.isLoading) return <p>{t('continuity.loading')}</p>
  if (!query.data) return <p>{t('continuity.notFound')}</p>
  const { plan, links, procedures } = query.data

  return (
    <div className="space-y-6">
      <PageHeader
        title={plan.planNumber}
        description={plan.title}
        actions={
          <Link to="/it/continuity" className="text-sm text-primary underline">
            {t('continuity.back')}
          </Link>
        }
      />
      <p className="text-sm">
        {plan.planType} · {plan.status}
        {plan.isReviewOverdue ? (
          <Badge className="ms-2" variant="warning">
            {t('continuity.reviewOverdue')}
          </Badge>
        ) : null}
      </p>
      {plan.managedDocumentId ? (
        <p className="text-sm">
          <Link to={`/it/documents/${plan.managedDocumentId}`} className="underline">
            {t('continuity.managedDocument')}
          </Link>
        </p>
      ) : null}
      {can('bcm.manage') ? (
        <div className="flex flex-wrap gap-2">
          {['Active', 'Retired'].map((status) => (
            <Button
              key={status}
              type="button"
              size="sm"
              variant="outline"
              onClick={async () => {
                await continuityApi.transitionPlan(id, status)
                await qc.invalidateQueries({ queryKey: ['continuity', 'plans', id] })
              }}
            >
              {status}
            </Button>
          ))}
          <Input
            className="max-w-xs"
            value={linkId}
            onChange={(e) => setLinkId(e.target.value)}
            placeholder={t('continuity.fields.targetId')}
          />
          <Select value={linkType} onValueChange={setLinkType}>
            <SelectTrigger className="w-[180px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {['BusinessService', 'ConfigurationItem', 'BiaRecord'].map((x) => (
                <SelectItem key={x} value={x}>
                  {x}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            type="button"
            size="sm"
            disabled={!linkId}
            onClick={async () => {
              await continuityApi.addPlanLink(id, linkType, linkId.trim())
              setLinkId('')
              await qc.invalidateQueries({ queryKey: ['continuity', 'plans', id] })
            }}
          >
            {t('continuity.addLink')}
          </Button>
          <Input
            className="max-w-xs"
            value={procTitle}
            onChange={(e) => setProcTitle(e.target.value)}
            placeholder={t('continuity.fields.procedureTitle')}
          />
          <Button
            type="button"
            size="sm"
            disabled={!procTitle.trim()}
            onClick={async () => {
              await continuityApi.createProcedure({ continuityPlanId: id, title: procTitle.trim() })
              setProcTitle('')
              await qc.invalidateQueries({ queryKey: ['continuity', 'plans', id] })
            }}
          >
            {t('continuity.createProcedure')}
          </Button>
        </div>
      ) : null}
      <div>
        <h3 className="mb-2 text-sm font-medium">{t('continuity.sections.procedures')}</h3>
        <ul className="space-y-1 text-sm">
          {procedures.map((p) => (
            <li key={p.id}>
              {p.procedureNumber} — {p.title}
              {p.recoveryStage ? ` (${p.recoveryStage})` : ''}
            </li>
          ))}
        </ul>
      </div>
      <ul className="text-sm text-muted-foreground">
        {links.map((l) => (
          <li key={l.id}>
            {l.targetType}: {l.targetId}
          </li>
        ))}
      </ul>
    </div>
  )
}

export function DrTestDetailPage() {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const { can } = useAuth()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [result, setResult] = useState('Passed')
  const [observedRto, setObservedRto] = useState('')
  const [observedRpo, setObservedRpo] = useState('')
  const [summary, setSummary] = useState('')
  const [gaps, setGaps] = useState('')
  const [evidenceId, setEvidenceId] = useState('')

  const query = useQuery({
    queryKey: ['continuity', 'tests', id],
    queryFn: () => continuityApi.getTest(id),
    enabled: !!id,
  })

  if (query.isLoading) return <p>{t('continuity.loading')}</p>
  if (!query.data) return <p>{t('continuity.notFound')}</p>
  const { test, businessService, links } = query.data

  return (
    <div className="space-y-6">
      <PageHeader
        title={test.drTestNumber}
        description={test.title}
        actions={
          <Link to="/it/continuity" className="text-sm text-primary underline">
            {t('continuity.back')}
          </Link>
        }
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">{t('continuity.testMeta')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p>
              {test.testType} · {test.status}
              {test.result ? ` · ${test.result}` : ''}
            </p>
            <p>
              RTO met:{' '}
              {test.rtoMet == null ? '—' : test.rtoMet ? t('continuity.met') : t('continuity.notMet')}
            </p>
            <p>
              RPO met:{' '}
              {test.rpoMet == null ? '—' : test.rpoMet ? t('continuity.met') : t('continuity.notMet')}
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">{t('continuity.serviceTargets')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p>{businessService?.name ?? test.businessServiceId}</p>
            <p>
              RTO: {businessService?.rtoMinutes ?? '—'} / RPO: {businessService?.rpoMinutes ?? '—'}
            </p>
          </CardContent>
        </Card>
      </div>
      {can('dr.test.manage') ? (
        <div className="space-y-3">
          <div className="flex flex-wrap gap-2">
            {test.status === 'Planned' ? (
              <Button
                type="button"
                size="sm"
                onClick={async () => {
                  await continuityApi.startTest(id)
                  await qc.invalidateQueries({ queryKey: ['continuity', 'tests', id] })
                }}
              >
                {t('continuity.startTest')}
              </Button>
            ) : null}
            {test.status === 'InProgress' || test.status === 'Planned' ? (
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={async () => {
                  await continuityApi.cancelTest(id)
                  await qc.invalidateQueries({ queryKey: ['continuity', 'tests', id] })
                }}
              >
                {t('continuity.cancelTest')}
              </Button>
            ) : null}
          </div>
          {test.status === 'InProgress' || test.status === 'Planned' ? (
            <div className="flex flex-wrap gap-2">
              <Select value={result} onValueChange={setResult}>
                <SelectTrigger className="w-[180px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {['Passed', 'PassedWithGaps', 'Failed', 'NotCompleted'].map((x) => (
                    <SelectItem key={x} value={x}>
                      {x}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input
                className="w-28"
                value={observedRto}
                onChange={(e) => setObservedRto(e.target.value)}
                placeholder="Obs RTO"
              />
              <Input
                className="w-28"
                value={observedRpo}
                onChange={(e) => setObservedRpo(e.target.value)}
                placeholder="Obs RPO"
              />
              <Input
                className="max-w-xs"
                value={summary}
                onChange={(e) => setSummary(e.target.value)}
                placeholder={t('continuity.fields.summary')}
              />
              <Input
                className="max-w-xs"
                value={gaps}
                onChange={(e) => setGaps(e.target.value)}
                placeholder={t('continuity.fields.gaps')}
              />
              <Button
                type="button"
                size="sm"
                onClick={async () => {
                  await continuityApi.completeTest(id, {
                    result,
                    observedRtoMinutes: observedRto ? Number(observedRto) : null,
                    observedRpoMinutes: observedRpo ? Number(observedRpo) : null,
                    summary: summary || null,
                    gaps: gaps || null,
                  })
                  await qc.invalidateQueries({ queryKey: ['continuity', 'tests', id] })
                }}
              >
                {t('continuity.completeTest')}
              </Button>
            </div>
          ) : null}
          {test.status === 'Completed' ? (
            <div className="flex flex-wrap gap-2">
              <Button type="button" size="sm" variant="outline" onClick={() => navigate('/it/evidence/new')}>
                {t('continuity.promoteEvidence')}
              </Button>
              <Input
                className="max-w-xs"
                value={evidenceId}
                onChange={(e) => setEvidenceId(e.target.value)}
                placeholder={t('continuity.fields.evidenceId')}
              />
              <Button
                type="button"
                size="sm"
                disabled={!evidenceId}
                onClick={async () => {
                  await continuityApi.addTestLink(id, 'Evidence', evidenceId.trim())
                  setEvidenceId('')
                  await qc.invalidateQueries({ queryKey: ['continuity', 'tests', id] })
                }}
              >
                {t('continuity.linkEvidence')}
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}
      <ul className="text-sm text-muted-foreground">
        {links.map((l) => (
          <li key={l.id}>
            {l.targetType}: {l.targetId}
          </li>
        ))}
      </ul>
    </div>
  )
}
