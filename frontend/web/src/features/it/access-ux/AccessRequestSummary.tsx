import { useTranslation } from 'react-i18next'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { cn } from '@/lib/utils'

export type RoutingPreview = {
  approver?: string | null
  fulfiller?: string | null
  verifier?: string | null
  closer?: string | null
}

export function AccessRequestSummary({
  category,
  typeLabel,
  employee,
  accessRequestedCount,
  standardCount,
  additionalCount,
  routing,
  className,
  sticky,
}: {
  category?: string | null
  typeLabel?: string | null
  employee?: string | null
  accessRequestedCount: number
  standardCount: number
  additionalCount: number
  routing?: RoutingPreview | null
  className?: string
  sticky?: boolean
}) {
  const { t } = useTranslation()

  return (
    <Card className={cn(sticky && 'lg:sticky lg:top-4', className)}>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">{t('access.summaryTitle')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <SummaryRow label={t('access.fields.category')} value={category || '—'} />
        <SummaryRow label={t('access.columns.type')} value={typeLabel || '—'} />
        <SummaryRow label={t('access.caseHero.employee')} value={employee || '—'} />
        <SummaryRow
          label={t('access.requestSummary.accessRequested')}
          value={String(accessRequestedCount)}
        />
        <Separator />
        <div className="space-y-1.5">
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            {t('access.requestSummary.selectedAccess')}
          </p>
          <p className="text-muted-foreground">
            {t('access.requestSummary.selectedBreakdown', {
              standard: standardCount,
              additional: additionalCount,
            })}
          </p>
        </div>
        {routing ? (
          <>
            <Separator />
            <div className="space-y-2">
              <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                {t('access.requestSummary.expectedWorkflow')}
              </p>
              <p className="text-[11px] text-muted-foreground">
                {t('access.requestSummary.routingPreviewNote')}
              </p>
              <SummaryRow label={t('access.workflow.approver')} value={routing.approver || '—'} />
              <SummaryRow label={t('access.workflow.fulfiller')} value={routing.fulfiller || '—'} />
              <SummaryRow
                label={t('access.requestSummary.verificationEmployee')}
                value={routing.verifier || '—'}
              />
              <SummaryRow label={t('access.workflow.closer')} value={routing.closer || '—'} />
            </div>
          </>
        ) : null}
      </CardContent>
    </Card>
  )
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-3">
      <span className="shrink-0 text-muted-foreground">{label}</span>
      <span className="text-end font-medium">{value}</span>
    </div>
  )
}
