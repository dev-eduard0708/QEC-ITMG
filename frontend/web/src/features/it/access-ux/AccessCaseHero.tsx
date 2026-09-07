import { useTranslation } from 'react-i18next'
import type { AccessCase } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { AccessStatusBadge } from '@/features/it/access-ux/AccessStatusBadge'
import { cn } from '@/lib/utils'

function formatWhen(value: string | null | undefined): string {
  if (!value) return '—'
  return new Date(value).toLocaleString()
}

export function AccessCaseHero({
  accessCase,
  employeeLabel,
  categoryLabel,
  requestedBy,
  currentOwner,
  className,
}: {
  accessCase: AccessCase
  employeeLabel: string
  categoryLabel?: string | null
  requestedBy?: string | null
  currentOwner?: string | null
  className?: string
}) {
  const { t } = useTranslation()
  const typeLabel = t(`access.types.${accessCase.type}`, { defaultValue: accessCase.type })

  return (
    <Card className={cn('overflow-hidden', className)}>
      <CardContent className="space-y-4 p-5 sm:p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0 space-y-1">
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              {t('access.caseHero.case')}
            </p>
            <h1 className="text-xl font-semibold tracking-tight sm:text-2xl">
              {accessCase.caseNumber}
            </h1>
          </div>
          <AccessStatusBadge accessCase={accessCase} />
        </div>

        <p className="text-sm leading-relaxed text-foreground/90">{accessCase.reason}</p>

        <div className="flex flex-wrap gap-1.5">
          {categoryLabel ? <Badge variant="outline">{categoryLabel}</Badge> : null}
          <Badge variant="outline">{typeLabel}</Badge>
        </div>

        <dl className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
          <div>
            <dt className="text-xs text-muted-foreground">{t('access.caseHero.employee')}</dt>
            <dd className="mt-0.5 font-medium">{employeeLabel}</dd>
          </div>
          {requestedBy ? (
            <div>
              <dt className="text-xs text-muted-foreground">{t('access.caseHero.requestedBy')}</dt>
              <dd className="mt-0.5 font-medium">{requestedBy}</dd>
            </div>
          ) : null}
          <div>
            <dt className="text-xs text-muted-foreground">{t('access.caseHero.submitted')}</dt>
            <dd className="mt-0.5 font-medium">{formatWhen(accessCase.createdAtUtc)}</dd>
          </div>
          {currentOwner ? (
            <div>
              <dt className="text-xs text-muted-foreground">{t('access.caseHero.currentOwner')}</dt>
              <dd className="mt-0.5 font-medium">{currentOwner}</dd>
            </div>
          ) : null}
        </dl>
      </CardContent>
    </Card>
  )
}
