import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { complianceApi, complianceReadinessApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import { ComplianceReadinessNav } from '@/features/compliance/readiness-nav'
import {
  formatPercent,
  isReadinessDashboardProfile,
  READINESS_PROFILE_CODES,
} from '@/features/compliance/readiness-shared'

export function ComplianceHomePage() {
  const { t, i18n } = useTranslation()
  const { can } = useAuth()
  const locale = i18n.language?.startsWith('ar') ? 'ar' : 'en'

  const overviewQuery = useQuery({
    queryKey: ['compliance', 'overview'],
    queryFn: () => complianceApi.overview(),
    enabled: can('compliance.read'),
  })
  const readinessQuery = useQuery({
    queryKey: ['compliance', 'readiness', 'landing', locale],
    queryFn: () => complianceReadinessApi.landing(locale),
    enabled: can('compliance.read'),
  })

  const cov = overviewQuery.data?.coverage
  const featured =
    readinessQuery.data?.cards.filter((c) => isReadinessDashboardProfile(c.profileType)) ?? []
  const isa =
    featured.find((c) => c.frameworkCode === READINESS_PROFILE_CODES.isa315) ??
    featured.find((c) => c.profileType === 'AuditReadiness')
  const cyber =
    featured.find((c) => c.frameworkCode === READINESS_PROFILE_CODES.cyber) ??
    featured.find((c) => c.profileType === 'CybersecurityReadiness')

  return (
    <div className="space-y-6">
      <PageHeader title={t('compliance.title')} description={t('compliance.description')} />
      <ComplianceReadinessNav />

      <div className="grid gap-4 lg:grid-cols-2">
        <Card className="border-primary/40">
          <CardHeader>
            <CardTitle>{t('readiness.home.isaTitle')}</CardTitle>
            <CardDescription>{t('readiness.home.isaDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {isa ? (
              <>
                <div className="flex items-baseline justify-between gap-2">
                  <span className="text-sm text-muted-foreground">
                    {t('readiness.metrics.readyForReview')}
                  </span>
                  <span className="text-2xl font-semibold tabular-nums">
                    {formatPercent(isa.metrics.readinessCoveragePercent)}
                  </span>
                </div>
                <Progress value={isa.metrics.readinessCoveragePercent} />
                <p className="text-sm text-muted-foreground">
                  {t('readiness.openGapsCount', { count: isa.openGaps })}
                </p>
              </>
            ) : (
              <p className="text-sm text-muted-foreground">{t('readiness.home.unavailable')}</p>
            )}
            <Button asChild>
              <Link to="/it/compliance/readiness/isa-315">{t('readiness.openReadiness')}</Link>
            </Button>
          </CardContent>
        </Card>

        <Card className="border-primary/40">
          <CardHeader>
            <CardTitle>{t('readiness.home.cyberTitle')}</CardTitle>
            <CardDescription>{t('readiness.home.cyberDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {cyber ? (
              <>
                <div className="flex items-baseline justify-between gap-2">
                  <span className="text-sm text-muted-foreground">
                    {t('readiness.metrics.readyForReview')}
                  </span>
                  <span className="text-2xl font-semibold tabular-nums">
                    {formatPercent(cyber.metrics.readinessCoveragePercent)}
                  </span>
                </div>
                <Progress value={cyber.metrics.readinessCoveragePercent} />
                <p className="text-sm text-muted-foreground">
                  {t('readiness.openGapsCount', { count: cyber.openGaps })}
                </p>
              </>
            ) : (
              <p className="text-sm text-muted-foreground">{t('readiness.home.unavailable')}</p>
            )}
            <Button asChild>
              <Link to="/it/compliance/readiness/cyber">{t('readiness.openReadiness')}</Link>
            </Button>
          </CardContent>
        </Card>
      </div>

      <div className="flex flex-wrap gap-2">
        <Button asChild variant="secondary">
          <Link to="/it/compliance/readiness">{t('compliance.nav.readiness')}</Link>
        </Button>
        <Button asChild variant="secondary">
          <Link to="/it/compliance/frameworks">{t('compliance.nav.frameworks')}</Link>
        </Button>
        <Button asChild variant="secondary">
          <Link to="/it/compliance/mappings">{t('compliance.nav.mappings')}</Link>
        </Button>
        <Button asChild variant="secondary">
          <Link to="/it/compliance/assessments">{t('compliance.nav.assessments')}</Link>
        </Button>
        <Button asChild variant="secondary">
          <Link to="/it/compliance/calendar">{t('compliance.nav.calendar')}</Link>
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('compliance.overview.counts')}</CardTitle>
          <CardDescription>{overviewQuery.data?.notes ?? t('compliance.noVanity')}</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 text-sm">
          {cov ? (
            <>
              <div>
                <div className="text-muted-foreground">{t('compliance.coverage.mapped')}</div>
                <div className="text-lg font-medium">
                  {cov.mappedRequirements} / {cov.totalRequirements}
                </div>
              </div>
              <div>
                <div className="text-muted-foreground">{t('compliance.coverage.unmapped')}</div>
                <div className="text-lg font-medium">{cov.unmappedRequirements}</div>
              </div>
              <div>
                <div className="text-muted-foreground">{t('compliance.coverage.assessed')}</div>
                <div className="text-lg font-medium">
                  {cov.assessedControls} / {cov.mappedControls}
                </div>
              </div>
              <div>
                <div className="text-muted-foreground">{t('compliance.coverage.unassessed')}</div>
                <div className="text-lg font-medium">{cov.unassessedControls}</div>
              </div>
              <div className="sm:col-span-2 lg:col-span-4 text-muted-foreground">
                {cov.frameworkCode} {cov.versionCode} · {t('readiness.results.compliant')}{' '}
                {cov.resultDistribution.compliant} · {t('readiness.results.partial')}{' '}
                {cov.resultDistribution.partiallyCompliant} · {t('readiness.results.nonCompliant')}{' '}
                {cov.resultDistribution.nonCompliant} · {t('readiness.results.notApplicable')}{' '}
                {cov.resultDistribution.notApplicable} · {t('readiness.results.notTested')}{' '}
                {cov.resultDistribution.notTested}
              </div>
              <div className="sm:col-span-2 lg:col-span-4 text-muted-foreground">
                {t('compliance.coverage.evidenceAvailable')}: {cov.evidenceAvailable} ·{' '}
                {t('compliance.coverage.evidenceMissing')}: {cov.evidenceMissing} ·{' '}
                {t('compliance.coverage.evidenceExpired')}: {cov.evidenceExpired}
              </div>
            </>
          ) : (
            <p className="text-muted-foreground">{t('compliance.overview.empty')}</p>
          )}
          <div>
            <div className="text-muted-foreground">{t('compliance.calendar.upcoming')}</div>
            <div className="text-lg font-medium">{overviewQuery.data?.upcomingCount ?? 0}</div>
          </div>
          <div>
            <div className="text-muted-foreground">{t('compliance.calendar.overdue')}</div>
            <div className="text-lg font-medium">{overviewQuery.data?.overdueCount ?? 0}</div>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
