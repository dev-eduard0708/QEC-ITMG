import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { complianceReadinessApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
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
  operationalLinkTitle,
  periodBoundsForYear,
  readinessStateBadgeVariant,
  readinessStateLabelKey,
  resolveReadinessFrameworkCode,
  safeInternalRoute,
} from '@/features/compliance/readiness-shared'

export function ReadinessRequirementPage() {
  const { frameworkCode: rawCode = '', requirementId = '' } = useParams()
  const frameworkCode = resolveReadinessFrameworkCode(rawCode)
  const { t, i18n } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [searchParams] = useSearchParams()
  const locale = i18n.language?.startsWith('ar') ? 'ar' : 'en'

  const yearParam = Number(searchParams.get('year') ?? '')
  const year = Number.isFinite(yearParam) && yearParam >= 2000 ? yearParam : currentPeriodYear()
  const { periodStart, periodEnd } = periodBoundsForYear(year)

  const [applicabilityStatus, setApplicabilityStatus] = useState('NotApplicable')
  const [applicabilityReason, setApplicabilityReason] = useState('')

  const detailQuery = useQuery({
    queryKey: [
      'compliance',
      'readiness',
      frameworkCode,
      'requirement',
      requirementId,
      locale,
      periodStart,
      periodEnd,
    ],
    queryFn: () =>
      complianceReadinessApi.getRequirement(frameworkCode, requirementId, {
        locale,
        periodStart,
        periodEnd,
      }),
    enabled: !!frameworkCode && !!requirementId,
  })

  const applicabilityMutation = useMutation({
    mutationFn: () =>
      complianceReadinessApi.setApplicability(requirementId, {
        status: applicabilityStatus,
        reason: applicabilityStatus === 'NotApplicable' ? applicabilityReason.trim() : null,
      }),
    onSuccess: async () => {
      await qc.invalidateQueries({
        queryKey: ['compliance', 'readiness', frameworkCode],
      })
    },
  })

  const detail = detailQuery.data
  const backHref = `/it/compliance/readiness/${encodeURIComponent(frameworkCode)}?year=${year}`

  if (detailQuery.isLoading) {
    return <p className="text-sm text-muted-foreground">{t('compliance.loading')}</p>
  }
  if (!detail) {
    return <p className="text-sm text-muted-foreground">{t('compliance.notFound')}</p>
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={`${detail.code} · ${detail.title}`}
        description={detail.domainTitle ?? t('readiness.requirement.description')}
        actions={
          <Button asChild variant="outline">
            <Link to={backHref}>{t('readiness.nav.backFramework')}</Link>
          </Button>
        }
      />
      <ComplianceReadinessNav />

      <Card>
        <CardContent className="pt-6 text-sm text-muted-foreground">
          {t('readiness.disclaimer')}
        </CardContent>
      </Card>

      <div className="flex flex-wrap gap-2">
        <Badge variant={readinessStateBadgeVariant(detail.readinessState)}>
          {t(readinessStateLabelKey(detail.readinessState))}
        </Badge>
        <Badge variant="outline">
          {t('readiness.requirement.applicability')}: {detail.applicabilityStatus}
        </Badge>
        {detail.domainCode ? <Badge variant="secondary">{detail.domainCode}</Badge> : null}
      </div>

      {detail.text ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('readiness.requirement.question')}</CardTitle>
          </CardHeader>
          <CardContent className="text-sm whitespace-pre-wrap">{detail.text}</CardContent>
        </Card>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('readiness.requirement.readinessSection')}</CardTitle>
            <CardDescription>{t('readiness.requirement.readinessHint')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <p>
              <span className="text-muted-foreground">{t('readiness.table.state')}: </span>
              {t(readinessStateLabelKey(detail.readinessState))}
            </p>
            {detail.applicabilityReason ? (
              <p>
                <span className="text-muted-foreground">{t('readiness.requirement.reason')}: </span>
                {detail.applicabilityReason}
              </p>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('readiness.requirement.effectivenessSection')}</CardTitle>
            <CardDescription>{t('readiness.requirement.effectivenessHint')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            {detail.mappedControls.some((c) => c.latestAssessmentResult) ? (
              detail.mappedControls
                .filter((c) => c.latestAssessmentResult)
                .map((c) => (
                  <div key={c.internalControlId} className="rounded border p-2">
                    <div className="font-medium">
                      {c.controlNumber ?? c.internalControlId} · {c.title ?? '—'}
                    </div>
                    <div className="text-muted-foreground">
                      {t('readiness.table.assessment')}: {c.latestAssessmentResult}
                      {c.latestAssessmentDateUtc
                        ? ` · ${new Date(c.latestAssessmentDateUtc).toLocaleDateString()}`
                        : ''}
                    </div>
                  </div>
                ))
            ) : (
              <p className="text-muted-foreground">{t('readiness.requirement.noAssessmentResult')}</p>
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{t('readiness.requirement.mappedControls')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {detail.mappedControls.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('readiness.requirement.noControls')}</p>
          ) : (
            detail.mappedControls.map((control) => (
              <div key={control.internalControlId} className="rounded border p-3 text-sm space-y-2">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <div className="font-medium">
                      {control.controlNumber ?? control.internalControlId}
                      {control.title ? ` · ${control.title}` : ''}
                    </div>
                    <div className="text-muted-foreground">
                      {control.status ?? '—'}
                      {control.latestAssessmentStatus
                        ? ` · ${control.latestAssessmentStatus}`
                        : ''}
                    </div>
                  </div>
                  <Button asChild size="sm" variant="secondary">
                    <Link to={`/it/controls/${control.internalControlId}`}>
                      {t('readiness.requirement.openControl')}
                    </Link>
                  </Button>
                </div>
                <div className="flex flex-wrap gap-2">
                  {control.latestAssessmentResult ? (
                    <Badge variant="outline">{control.latestAssessmentResult}</Badge>
                  ) : null}
                  {control.hasAvailableEvidence ? (
                    <Badge variant="success">{t('readiness.evidence.available')}</Badge>
                  ) : control.hasExpiredOnlyEvidence ? (
                    <Badge variant="warning">{t('readiness.evidence.expired')}</Badge>
                  ) : (
                    <Badge variant="outline">{t('readiness.evidence.missing')}</Badge>
                  )}
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button asChild size="sm" variant="outline">
                    <Link to={`/it/compliance/assessments`}>
                      {t('readiness.requirement.openAssessments')}
                    </Link>
                  </Button>
                  <Button asChild size="sm" variant="outline">
                    <Link to="/it/evidence">{t('readiness.requirement.openEvidence')}</Link>
                  </Button>
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('readiness.requirement.operationalLinks')}</CardTitle>
          <CardDescription>{t('readiness.requirement.operationalHint')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-2">
          {detail.operationalLinks.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('readiness.requirement.noLinks')}</p>
          ) : (
            detail.operationalLinks.map((link) => {
              const route = safeInternalRoute(link.internalRoute)
              return (
                <div key={link.id} className="flex flex-wrap items-center justify-between gap-2 rounded border p-3 text-sm">
                  <div>
                    <div className="font-medium">{operationalLinkTitle(link, i18n.language)}</div>
                    <div className="text-muted-foreground">
                      {link.linkType}
                      {link.notes ? ` · ${link.notes}` : ''}
                    </div>
                  </div>
                  {route ? (
                    <Button asChild size="sm" variant="secondary">
                      <Link to={route}>{t('readiness.requirement.openModule')}</Link>
                    </Button>
                  ) : null}
                </div>
              )
            })
          )}
        </CardContent>
      </Card>

      {can('compliance.manage') ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('readiness.requirement.setApplicability')}</CardTitle>
            <CardDescription>{t('readiness.requirement.applicabilityHint')}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2 items-end">
            <Select value={applicabilityStatus} onValueChange={setApplicabilityStatus}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Applicable">{t('readiness.applicability.applicable')}</SelectItem>
                <SelectItem value="NotApplicable">{t('readiness.applicability.notApplicable')}</SelectItem>
              </SelectContent>
            </Select>
            {applicabilityStatus === 'NotApplicable' ? (
              <Input
                className="max-w-md"
                value={applicabilityReason}
                onChange={(e) => setApplicabilityReason(e.target.value)}
                placeholder={t('readiness.requirement.reasonRequired')}
              />
            ) : null}
            <Button
              type="button"
              disabled={
                applicabilityMutation.isPending ||
                (applicabilityStatus === 'NotApplicable' && !applicabilityReason.trim())
              }
              onClick={() => applicabilityMutation.mutate()}
            >
              {t('readiness.requirement.saveApplicability')}
            </Button>
          </CardContent>
        </Card>
      ) : null}
    </div>
  )
}
