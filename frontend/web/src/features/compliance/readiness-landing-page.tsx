import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { complianceReadinessApi, type ReadinessLandingCard } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import { ComplianceReadinessNav } from '@/features/compliance/readiness-nav'
import {
  formatPercent,
  isReadinessDashboardProfile,
  READINESS_PROFILE_CODES,
} from '@/features/compliance/readiness-shared'

function profileSortRank(profileType: string): number {
  if (profileType === 'AuditReadiness') return 0
  if (profileType === 'CybersecurityReadiness') return 1
  if (profileType === 'Governance') return 2
  return 3
}

function shortcutPath(card: ReadinessLandingCard): string {
  if (card.frameworkCode === READINESS_PROFILE_CODES.isa315) return '/it/compliance/readiness/isa-315'
  if (card.frameworkCode === READINESS_PROFILE_CODES.cyber) return '/it/compliance/readiness/cyber'
  return `/it/compliance/readiness/${encodeURIComponent(card.frameworkCode)}`
}

function ReadinessCard({ card }: { card: ReadinessLandingCard }) {
  const { t } = useTranslation()
  const m = card.metrics
  const featured = isReadinessDashboardProfile(card.profileType)

  return (
    <Card className={featured ? 'border-primary/40' : undefined}>
      <CardHeader>
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="space-y-1">
            <CardTitle>{card.name}</CardTitle>
            <CardDescription>
              {card.frameworkCode}
              {card.versionCode ? ` · ${card.versionCode}` : ''}
            </CardDescription>
          </div>
          {featured ? (
            <Badge variant="secondary">
              {card.profileType === 'AuditReadiness'
                ? t('readiness.profiles.audit')
                : t('readiness.profiles.cyber')}
            </Badge>
          ) : (
            <Badge variant="outline">{t('readiness.profiles.other')}</Badge>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div>
          <div className="mb-1 flex items-baseline justify-between gap-2 text-sm">
            <span className="font-medium">{t('readiness.metrics.readyForReview')}</span>
            <span className="text-2xl font-semibold tabular-nums">
              {formatPercent(m.readinessCoveragePercent)}
            </span>
          </div>
          <Progress value={m.readinessCoveragePercent} />
        </div>

        <div className="grid gap-3 sm:grid-cols-3 text-sm">
          <div>
            <div className="text-muted-foreground">{t('readiness.metrics.controlCoverage')}</div>
            <div className="font-medium tabular-nums">{formatPercent(m.mappedCoveragePercent)}</div>
          </div>
          <div>
            <div className="text-muted-foreground">{t('readiness.metrics.assessmentCoverage')}</div>
            <div className="font-medium tabular-nums">{formatPercent(m.assessmentCoveragePercent)}</div>
          </div>
          <div>
            <div className="text-muted-foreground">{t('readiness.metrics.evidenceCoverage')}</div>
            <div className="font-medium tabular-nums">{formatPercent(m.evidenceCoveragePercent)}</div>
          </div>
        </div>

        <p className="text-sm text-muted-foreground">
          {t('readiness.openGapsCount', { count: card.openGaps })}
        </p>

        {card.description ? <p className="text-sm text-muted-foreground">{card.description}</p> : null}

        <Button asChild>
          <Link to={shortcutPath(card)}>{t('readiness.openReadiness')}</Link>
        </Button>
      </CardContent>
    </Card>
  )
}

export function ReadinessLandingPage() {
  const { t, i18n } = useTranslation()
  const locale = i18n.language?.startsWith('ar') ? 'ar' : 'en'
  const landingQuery = useQuery({
    queryKey: ['compliance', 'readiness', 'landing', locale],
    queryFn: () => complianceReadinessApi.landing(locale),
  })

  const cards = [...(landingQuery.data?.cards ?? [])].sort(
    (a, b) => profileSortRank(a.profileType) - profileSortRank(b.profileType) || a.name.localeCompare(b.name),
  )
  const featured = cards.filter((c) => isReadinessDashboardProfile(c.profileType))
  const others = cards.filter((c) => !isReadinessDashboardProfile(c.profileType))

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('readiness.landing.title')}
        description={t('readiness.landing.description')}
      />
      <ComplianceReadinessNav />

      <Card>
        <CardContent className="pt-6 text-sm text-muted-foreground">
          {landingQuery.data?.disclaimer ?? t('readiness.disclaimer')}
        </CardContent>
      </Card>

      {landingQuery.isLoading ? (
        <p className="text-sm text-muted-foreground">{t('compliance.loading')}</p>
      ) : null}

      {featured.length > 0 ? (
        <div className="grid gap-4 lg:grid-cols-2">
          {featured.map((card) => (
            <ReadinessCard key={card.frameworkId} card={card} />
          ))}
        </div>
      ) : null}

      {others.length > 0 ? (
        <div className="space-y-3">
          <h2 className="text-lg font-semibold">{t('readiness.landing.otherFrameworks')}</h2>
          <div className="grid gap-4 lg:grid-cols-2">
            {others.map((card) => (
              <ReadinessCard key={card.frameworkId} card={card} />
            ))}
          </div>
        </div>
      ) : null}

      {!landingQuery.isLoading && cards.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('readiness.landing.empty')}</p>
      ) : null}
    </div>
  )
}
