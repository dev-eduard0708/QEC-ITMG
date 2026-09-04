import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { complianceApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function ComplianceHomePage() {
  const { t } = useTranslation()
  const { can } = useAuth()
  const overviewQuery = useQuery({
    queryKey: ['compliance', 'overview'],
    queryFn: () => complianceApi.overview(),
    enabled: can('compliance.read'),
  })
  const cov = overviewQuery.data?.coverage

  return (
    <div className="space-y-6">
      <PageHeader title={t('compliance.title')} description={t('compliance.description')} />
      <div className="flex flex-wrap gap-2">
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
                {cov.frameworkCode} {cov.versionCode} · Compliant {cov.resultDistribution.compliant} · Partial{' '}
                {cov.resultDistribution.partiallyCompliant} · NonCompliant {cov.resultDistribution.nonCompliant} · N/A{' '}
                {cov.resultDistribution.notApplicable} · NotTested {cov.resultDistribution.notTested}
              </div>
              <div className="sm:col-span-2 lg:col-span-4 text-muted-foreground">
                {t('compliance.coverage.evidence')}: {cov.evidenceMissingStatus}
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
