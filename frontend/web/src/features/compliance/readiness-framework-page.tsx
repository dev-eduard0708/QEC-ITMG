import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { ColumnDef } from '@tanstack/react-table'
import {
  complianceReadinessApi,
  type ReadinessRequirementListItem,
} from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { DataTable } from '@/components/shared/data-table'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Progress } from '@/components/ui/progress'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { ComplianceReadinessNav } from '@/features/compliance/readiness-nav'
import {
  currentPeriodYear,
  formatPercent,
  operationalLinkTitle,
  periodBoundsForYear,
  READINESS_STATES,
  readinessStateBadgeVariant,
  readinessStateLabelKey,
  resolveReadinessFrameworkCode,
  safeInternalRoute,
} from '@/features/compliance/readiness-shared'

function yearOptions(center = currentPeriodYear()): number[] {
  return [center - 1, center, center + 1]
}

export function ReadinessFrameworkPage() {
  const { frameworkCode: rawCode = '' } = useParams()
  const frameworkCode = resolveReadinessFrameworkCode(rawCode)
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const locale = i18n.language?.startsWith('ar') ? 'ar' : 'en'

  const yearParam = Number(searchParams.get('year') ?? '')
  const year = Number.isFinite(yearParam) && yearParam >= 2000 ? yearParam : currentPeriodYear()
  const { periodStart, periodEnd } = periodBoundsForYear(year)

  const domainFilter = searchParams.get('domain') ?? ''
  const statusFilter = searchParams.get('status') ?? ''
  const searchFilter = searchParams.get('q') ?? ''
  const [searchDraft, setSearchDraft] = useState(searchFilter)

  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(searchParams)
    if (!value || value === 'all') next.delete(key)
    else next.set(key, value)
    if (key !== 'year' && !next.get('year')) next.set('year', String(year))
    setSearchParams(next, { replace: true })
  }

  const summaryQuery = useQuery({
    queryKey: ['compliance', 'readiness', frameworkCode, 'summary', locale, periodStart, periodEnd],
    queryFn: () =>
      complianceReadinessApi.framework(frameworkCode, { locale, periodStart, periodEnd }),
    enabled: !!frameworkCode,
  })

  const requirementsQuery = useQuery({
    queryKey: [
      'compliance',
      'readiness',
      frameworkCode,
      'requirements',
      locale,
      periodStart,
      periodEnd,
      domainFilter,
      statusFilter,
      searchFilter,
    ],
    queryFn: () =>
      complianceReadinessApi.listRequirements(frameworkCode, {
        locale,
        periodStart,
        periodEnd,
        domainRequirementId: domainFilter || undefined,
        status: statusFilter || undefined,
        search: searchFilter || undefined,
      }),
    enabled: !!frameworkCode,
  })

  const summary = summaryQuery.data
  const metrics = summary?.metrics
  const subtitleKey =
    summary?.profileType === 'CybersecurityReadiness'
      ? 'readiness.framework.cyberSubtitle'
      : summary?.profileType === 'AuditReadiness'
        ? 'readiness.framework.isaSubtitle'
        : 'readiness.framework.genericSubtitle'

  const columns = useMemo<ColumnDef<ReadinessRequirementListItem, unknown>[]>(
    () => [
      { accessorKey: 'code', header: t('readiness.table.code') },
      {
        accessorKey: 'title',
        header: t('readiness.table.requirement'),
        cell: ({ row }) => (
          <div className="max-w-md">
            <div className="font-medium">{row.original.title}</div>
            {row.original.domainTitle ? (
              <div className="text-xs text-muted-foreground">{row.original.domainTitle}</div>
            ) : null}
          </div>
        ),
      },
      {
        accessorKey: 'readinessState',
        header: t('readiness.table.state'),
        cell: ({ row }) => (
          <Badge variant={readinessStateBadgeVariant(row.original.readinessState)}>
            {t(readinessStateLabelKey(row.original.readinessState))}
          </Badge>
        ),
      },
      {
        id: 'controls',
        header: t('readiness.table.controls'),
        cell: ({ row }) => row.original.mappedControlCount,
      },
      {
        id: 'assessment',
        header: t('readiness.table.assessment'),
        cell: ({ row }) =>
          row.original.latestAssessmentResult ? (
            <Badge variant="outline">{row.original.latestAssessmentResult}</Badge>
          ) : row.original.hasCompletedAssessment ? (
            t('readiness.assessment.completed')
          ) : (
            '—'
          ),
      },
      {
        id: 'evidence',
        header: t('readiness.table.evidence'),
        cell: ({ row }) => {
          if (row.original.hasAvailableEvidence) return t('readiness.evidence.available')
          if (row.original.hasExpiredEvidence) return t('readiness.evidence.expired')
          return row.original.mappedControlCount > 0 ? t('readiness.evidence.missing') : '—'
        },
      },
      {
        id: 'links',
        header: t('readiness.table.links'),
        cell: ({ row }) => {
          const links = row.original.operationalLinks.slice(0, 2)
          if (!links.length) return '—'
          return (
            <div className="flex flex-col gap-1">
              {links.map((link) => {
                const route = safeInternalRoute(link.internalRoute)
                if (!route) return null
                return (
                  <Link
                    key={link.id}
                    to={route}
                    className="text-primary underline text-xs"
                    onClick={(e) => e.stopPropagation()}
                  >
                    {operationalLinkTitle(link, i18n.language)}
                  </Link>
                )
              })}
            </div>
          )
        },
      },
    ],
    [t, i18n.language],
  )

  if (!frameworkCode) {
    return <p className="text-sm text-muted-foreground">{t('compliance.notFound')}</p>
  }

  if (summaryQuery.isLoading) {
    return <p className="text-sm text-muted-foreground">{t('compliance.loading')}</p>
  }

  if (summaryQuery.isError || !summary) {
    return <p className="text-sm text-muted-foreground">{t('compliance.notFound')}</p>
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={summary.name}
        description={summary.description ?? t(subtitleKey)}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/compliance/readiness">{t('readiness.nav.backLanding')}</Link>
          </Button>
        }
      />
      <ComplianceReadinessNav />

      <Card>
        <CardContent className="pt-6 text-sm text-muted-foreground">
          {summary.disclaimer || t('readiness.disclaimer')}
        </CardContent>
      </Card>

      <div className="flex flex-wrap items-end gap-3">
        <div className="space-y-1">
          <div className="text-xs text-muted-foreground">{t('readiness.filters.periodYear')}</div>
          <Select
            value={String(year)}
            onValueChange={(v) => {
              const next = new URLSearchParams(searchParams)
              next.set('year', v)
              setSearchParams(next, { replace: true })
            }}
          >
            <SelectTrigger className="w-36">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {yearOptions().map((y) => (
                <SelectItem key={y} value={String(y)}>
                  {y}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <p className="text-xs text-muted-foreground pb-2">
          {t('readiness.filters.periodHint', { start: periodStart, end: periodEnd })}
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('readiness.metrics.readyForReview')}</CardTitle>
          <CardDescription>
            {metrics
              ? t('readiness.metrics.readyCounts', {
                  ready: metrics.readyForReview,
                  applicable: metrics.applicableRequirements,
                })
              : null}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-2">
          <div className="text-3xl font-semibold tabular-nums">
            {formatPercent(metrics?.readinessCoveragePercent)}
          </div>
          <Progress value={metrics?.readinessCoveragePercent ?? 0} className="h-3" />
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          title={t('readiness.metrics.controlCoverage')}
          percent={metrics?.mappedCoveragePercent}
          detail={
            metrics
              ? `${metrics.mappedRequirements} / ${metrics.applicableRequirements}`
              : undefined
          }
        />
        <MetricCard
          title={t('readiness.metrics.assessmentCoverage')}
          percent={metrics?.assessmentCoveragePercent}
          detail={
            metrics
              ? `${metrics.assessedRequirements} / ${metrics.mappedRequirements}`
              : undefined
          }
        />
        <MetricCard
          title={t('readiness.metrics.evidenceCoverage')}
          percent={metrics?.evidenceCoveragePercent}
          detail={
            metrics
              ? t('readiness.metrics.evidenceDetail', {
                  available: metrics.evidenceAvailable,
                  missing: metrics.evidenceMissing,
                  expired: metrics.evidenceExpired,
                })
              : undefined
          }
        />
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('readiness.gaps.title')}</CardTitle>
            <CardDescription>{t('readiness.gaps.description')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <GapButton
              label={t('readiness.gaps.unmapped')}
              count={metrics?.unmappedRequirements ?? 0}
              onClick={() => setParam('status', 'Unmapped')}
            />
            <GapButton
              label={t('readiness.gaps.needsAssessment')}
              count={metrics?.unassessedRequirements ?? 0}
              onClick={() => setParam('status', 'MappedNeedsAssessment')}
            />
            <GapButton
              label={t('readiness.gaps.evidenceMissing')}
              count={metrics?.evidenceMissing ?? 0}
              onClick={() => setParam('status', 'AssessedNeedsEvidence')}
            />
            <GapButton
              label={t('readiness.gaps.evidenceExpired')}
              count={metrics?.evidenceExpired ?? 0}
              onClick={() => setParam('status', 'AssessedNeedsEvidence')}
            />
            <div className="pt-2 text-muted-foreground">
              {t('readiness.gaps.notReady', { count: metrics?.notReadyForReview ?? 0 })}
            </div>
          </CardContent>
        </Card>
      </div>

      {metrics?.resultDistribution ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('readiness.results.title')}</CardTitle>
            <CardDescription>{t('readiness.results.description')}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-3 text-sm">
            <Badge variant="outline">
              {t('readiness.results.compliant')}: {metrics.resultDistribution.compliant}
            </Badge>
            <Badge variant="outline">
              {t('readiness.results.partial')}: {metrics.resultDistribution.partiallyCompliant}
            </Badge>
            <Badge variant="outline">
              {t('readiness.results.nonCompliant')}: {metrics.resultDistribution.nonCompliant}
            </Badge>
            <Badge variant="outline">
              {t('readiness.results.notApplicable')}: {metrics.resultDistribution.notApplicable}
            </Badge>
            <Badge variant="outline">
              {t('readiness.results.notTested')}: {metrics.resultDistribution.notTested}
            </Badge>
          </CardContent>
        </Card>
      ) : null}

      <div className="space-y-3">
        <h2 className="text-lg font-semibold">{t('readiness.domains.title')}</h2>
        <div className="grid gap-3 md:grid-cols-2">
          {summary.domains.map((domain) => {
            const active = domainFilter === domain.domainRequirementId
            return (
              <Card key={domain.domainRequirementId} className={active ? 'border-primary' : undefined}>
                <CardHeader className="pb-2">
                  <CardTitle className="text-base">{domain.title}</CardTitle>
                  <CardDescription>{domain.code}</CardDescription>
                </CardHeader>
                <CardContent className="space-y-3">
                  <div className="flex items-center justify-between text-sm">
                    <span>
                      {t('readiness.domains.readyOf', {
                        ready: domain.readyForReviewCount,
                        total: domain.applicableCount,
                      })}
                    </span>
                    <span className="tabular-nums font-medium">
                      {formatPercent(domain.readinessPercent)}
                    </span>
                  </div>
                  <Progress value={domain.readinessPercent} />
                  <div className="grid grid-cols-2 gap-2 text-xs text-muted-foreground sm:grid-cols-4">
                    <span>
                      {t('readiness.domains.mapped')}: {domain.mappedCount}
                    </span>
                    <span>
                      {t('readiness.domains.assessed')}: {domain.assessedCount}
                    </span>
                    <span>
                      {t('readiness.domains.evidenceMissing')}: {domain.evidenceMissingCount}
                    </span>
                    <span>
                      {t('readiness.domains.unmapped')}: {domain.unmappedCount}
                    </span>
                  </div>
                  <Button
                    type="button"
                    size="sm"
                    variant={active ? 'default' : 'secondary'}
                    onClick={() =>
                      setParam('domain', active ? '' : domain.domainRequirementId)
                    }
                  >
                    {active ? t('readiness.domains.clearFilter') : t('readiness.domains.filter')}
                  </Button>
                </CardContent>
              </Card>
            )
          })}
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('readiness.requirements.title')}</CardTitle>
          <CardDescription>{t('readiness.requirements.description')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <Input
              className="max-w-xs"
              value={searchDraft}
              onChange={(e) => setSearchDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') setParam('q', searchDraft.trim())
              }}
              placeholder={t('readiness.filters.searchPlaceholder')}
            />
            <Button type="button" variant="secondary" onClick={() => setParam('q', searchDraft.trim())}>
              {t('readiness.filters.search')}
            </Button>
            <Select
              value={statusFilter || 'all'}
              onValueChange={(v) => setParam('status', v === 'all' ? '' : v)}
            >
              <SelectTrigger className="w-56">
                <SelectValue placeholder={t('readiness.filters.status')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t('readiness.filters.allStatuses')}</SelectItem>
                {READINESS_STATES.map((state) => (
                  <SelectItem key={state} value={state}>
                    {t(readinessStateLabelKey(state))}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={domainFilter || 'all'}
              onValueChange={(v) => setParam('domain', v === 'all' ? '' : v)}
            >
              <SelectTrigger className="w-64">
                <SelectValue placeholder={t('readiness.filters.domain')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t('readiness.filters.allDomains')}</SelectItem>
                {summary.domains.map((d) => (
                  <SelectItem key={d.domainRequirementId} value={d.domainRequirementId}>
                    {d.code} · {d.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <DataTable
            columns={columns}
            data={requirementsQuery.data ?? []}
            isLoading={requirementsQuery.isLoading}
            onRowClick={(row) =>
              navigate(
                `/it/compliance/readiness/${encodeURIComponent(frameworkCode)}/requirements/${row.id}?year=${year}`,
              )
            }
          />
        </CardContent>
      </Card>
    </div>
  )
}

function MetricCard({
  title,
  percent,
  detail,
}: {
  title: string
  percent?: number
  detail?: string
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{title}</CardTitle>
        {detail ? <CardDescription>{detail}</CardDescription> : null}
      </CardHeader>
      <CardContent className="space-y-2">
        <div className="text-2xl font-semibold tabular-nums">{formatPercent(percent)}</div>
        <Progress value={percent ?? 0} />
      </CardContent>
    </Card>
  )
}

function GapButton({
  label,
  count,
  onClick,
}: {
  label: string
  count: number
  onClick: () => void
}) {
  return (
    <button
      type="button"
      className="flex w-full items-center justify-between rounded-md border px-3 py-2 text-start hover:bg-accent"
      onClick={onClick}
    >
      <span>{label}</span>
      <span className="font-medium tabular-nums">{count}</span>
    </button>
  )
}
