import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { FileText, GraduationCap, ShieldAlert } from 'lucide-react'
import { awarenessApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'

export function EmployeeSecurityPage() {
  const { t } = useTranslation()

  const summaryQuery = useQuery({
    queryKey: ['me', 'security', 'awareness', 'summary'],
    queryFn: () => awarenessApi.mySummary(),
  })

  const outstanding = summaryQuery.data?.outstanding ?? 0
  const overdue = summaryQuery.data?.overdue ?? 0
  const awarenessMeta = summaryQuery.isLoading
    ? null
    : overdue > 0
      ? t('employee.security.awareness.countOverdue', { count: overdue })
      : outstanding > 0
        ? t('employee.security.awareness.countDue', { count: outstanding })
        : t('employee.security.awareness.countNone')

  return (
    <div className="mx-auto max-w-4xl space-y-8">
      <PageHeader title={t('employee.security.title')} description={t('employee.security.description')} />

      <div className="grid gap-4 md:grid-cols-2">
        <ActionCard
          to="/employee/security/awareness"
          icon={GraduationCap}
          title={t('employee.security.awareness.cardTitle')}
          description={t('employee.security.awareness.cardHint')}
          meta={awarenessMeta}
          emphasized={outstanding > 0}
        />
        <ActionCard
          to="/employee/security/report"
          icon={ShieldAlert}
          title={t('employee.security.report.cardTitle')}
          description={t('employee.security.report.cardHint')}
          meta={t('employee.security.report.cardMeta')}
        />
      </div>

      <p className="rounded-xl border border-amber-500/40 bg-amber-500/5 px-4 py-3 text-sm">
        {t('employee.security.urgentNotice')}
      </p>

      <Card>
        <CardHeader className="space-y-3 pb-2">
          <div className="flex items-center gap-2 text-muted-foreground">
            <FileText className="h-4 w-4" aria-hidden />
            <CardTitle className="text-sm font-semibold text-foreground">{t('nav.myPolicies')}</CardTitle>
          </div>
          <CardDescription>{t('employee.security.policiesHint')}</CardDescription>
        </CardHeader>
        <CardContent>
          <Button asChild size="sm" variant="outline">
            <Link to="/employee/policies">{t('employee.security.openPolicies')}</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}

function ActionCard({
  to,
  icon: Icon,
  title,
  description,
  meta,
  emphasized,
}: {
  to: string
  icon: typeof ShieldAlert
  title: string
  description: string
  meta: string | null
  emphasized?: boolean
}) {
  return (
    <Link
      to={to}
      className={cn(
        'flex min-h-[10rem] flex-col gap-3 rounded-2xl border p-5 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        emphasized
          ? 'border-primary/40 bg-primary/5 hover:bg-primary/10'
          : 'border-border bg-card hover:bg-accent/40',
      )}
    >
      <Icon className="h-6 w-6 shrink-0 text-muted-foreground" aria-hidden />
      <div className="space-y-1">
        <div className="text-base font-semibold">{title}</div>
        <p className="text-sm leading-relaxed text-muted-foreground">{description}</p>
      </div>
      <span className="mt-auto text-xs text-muted-foreground">{meta ?? '—'}</span>
    </Link>
  )
}
