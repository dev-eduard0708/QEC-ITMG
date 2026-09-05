import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  FileText,
  HardDrive,
  Laptop,
  LifeBuoy,
  Search,
  Shield,
  Ticket,
} from 'lucide-react'
import { awarenessApi, meApi, policiesApi, remoteSupportApi } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { equipmentKeys, remoteSupportKeys, ticketKeys } from '@/features/it/query-keys'
import { isOpenTicketStatus } from '@/features/employee/employee-request-helpers'
import { cn } from '@/lib/utils'

export function EmployeeHomePage() {
  const { t } = useTranslation()

  const ticketsQuery = useQuery({
    queryKey: ticketKeys.mine('home'),
    queryFn: () => meApi.listTickets({ pageSize: 50 }),
  })
  const policiesQuery = useQuery({
    queryKey: ['me', 'policies', 'summary'],
    queryFn: () => policiesApi.summary(),
  })
  const awarenessQuery = useQuery({
    queryKey: ['me', 'security', 'awareness', 'summary'],
    queryFn: () => awarenessApi.mySummary(),
  })
  const remoteQuery = useQuery({
    queryKey: remoteSupportKeys.mine('home'),
    queryFn: () => remoteSupportApi.myList({ pageSize: 20, status: 'NotifyUser' }),
  })
  const equipmentQuery = useQuery({
    queryKey: equipmentKeys.mine,
    queryFn: () => meApi.listEquipment(),
  })

  const openCount = (ticketsQuery.data?.items ?? []).filter((item) => isOpenTicketStatus(item.status)).length
  const policyCount = policiesQuery.data?.outstandingForUser ?? 0
  const policyOverdue = policiesQuery.data?.overdue ?? 0
  const awarenessOutstanding = awarenessQuery.data?.outstanding ?? 0
  const remotePending = remoteQuery.data?.totalCount ?? (remoteQuery.data?.items?.length ?? 0)
  const equipmentCount = equipmentQuery.data?.length ?? 0

  const policyMeta = policiesQuery.isLoading
    ? null
    : policyOverdue > 0
      ? t('employee.counts.policiesOverdue', { count: policyOverdue })
      : policyCount > 0
        ? t('employee.counts.policiesDue', { count: policyCount })
        : t('employee.counts.policiesOk')

  const awarenessMeta = awarenessQuery.isLoading
    ? null
    : awarenessOutstanding > 0
      ? t('employee.counts.awarenessDue', { count: awarenessOutstanding })
      : t('employee.counts.awarenessOk')

  return (
    <div className="mx-auto max-w-5xl space-y-8">
      <div className="space-y-2">
        <h1 className="text-balance text-2xl font-semibold tracking-tight sm:text-3xl">
          {t('employee.helpToday')}
        </h1>
        <p className="max-w-2xl text-sm text-muted-foreground sm:text-base">
          {t('employee.helpTodayHint')}
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <PrimaryAction
          to="/employee/requests/new"
          icon={LifeBuoy}
          title={t('employee.actions.getHelp')}
          description={t('employee.actions.getHelpHint')}
          emphasized
        />
        <PrimaryAction
          to="/employee/requests"
          icon={Ticket}
          title={t('employee.actions.myRequests')}
          description={t('employee.actions.myRequestsHint')}
          badge={
            ticketsQuery.isLoading
              ? undefined
              : openCount > 0
                ? t('employee.counts.openRequests', { count: openCount })
                : undefined
          }
        />
        <PrimaryAction
          to="/employee/knowledge"
          icon={Search}
          title={t('employee.actions.findAnswer')}
          description={t('employee.actions.findAnswerHint')}
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <SecondaryCard
          to="/employee/equipment"
          icon={HardDrive}
          title={t('nav.equipment')}
          description={t('employee.actions.equipmentHint')}
          meta={
            equipmentQuery.isLoading
              ? null
              : t('employee.counts.devices', { count: equipmentCount })
          }
        />
        <SecondaryCard
          to="/employee/policies"
          icon={FileText}
          title={t('nav.myPolicies')}
          description={t('employee.actions.policiesHint')}
          meta={policyMeta}
          actionLabel={t('employee.actions.reviewPolicies')}
        />
        <SecondaryCard
          to="/employee/remote-support"
          icon={Laptop}
          title={t('nav.remoteSupport')}
          description={t('employee.actions.remoteHint')}
          meta={
            remoteQuery.isLoading
              ? null
              : remotePending > 0
                ? t('employee.counts.remotePending', { count: remotePending })
                : t('employee.counts.remoteNone')
          }
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <SecondaryCard
          to="/employee/security/awareness"
          icon={Shield}
          title={t('employee.security.awareness.cardTitle')}
          description={t('employee.actions.awarenessHint')}
          meta={awarenessMeta}
          actionLabel={t('employee.actions.openAwareness')}
        />
        <SecondaryCard
          to="/employee/security/report"
          icon={Shield}
          title={t('employee.security.report.cardTitle')}
          description={t('employee.actions.reportSecurityHint')}
          meta={t('employee.security.report.cardMeta')}
        />
      </div>

      {(ticketsQuery.isLoading || policiesQuery.isLoading || remoteQuery.isLoading) && (
        <Skeleton className="h-8 w-48" />
      )}
    </div>
  )
}

function PrimaryAction({
  to,
  icon: Icon,
  title,
  description,
  emphasized,
  badge,
}: {
  to: string
  icon: typeof LifeBuoy
  title: string
  description: string
  emphasized?: boolean
  badge?: string
}) {
  return (
    <Link
      to={to}
      className={cn(
        'flex flex-col gap-3 rounded-2xl border p-5 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        emphasized
          ? 'border-primary/30 bg-primary text-primary-foreground shadow-sm hover:bg-primary/90'
          : 'border-border bg-card hover:bg-accent/40',
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <Icon className="h-6 w-6 shrink-0 opacity-90" aria-hidden />
        {badge ? (
          <span
            className={cn(
              'rounded-full px-2 py-0.5 text-xs font-medium',
              emphasized ? 'bg-white/15' : 'bg-muted text-muted-foreground',
            )}
          >
            {badge}
          </span>
        ) : null}
      </div>
      <div className="space-y-1">
        <div className="text-base font-semibold">{title}</div>
        <p className={cn('text-sm leading-relaxed', emphasized ? 'text-primary-foreground/85' : 'text-muted-foreground')}>
          {description}
        </p>
      </div>
    </Link>
  )
}

function SecondaryCard({
  to,
  icon: Icon,
  title,
  description,
  meta,
  actionLabel,
}: {
  to: string
  icon: typeof FileText
  title: string
  description: string
  meta: string | null
  actionLabel?: string
}) {
  return (
    <Card className="transition-colors hover:bg-muted/20">
      <CardHeader className="space-y-3 pb-2">
        <div className="flex items-center gap-2 text-muted-foreground">
          <Icon className="h-4 w-4" aria-hidden />
          <CardTitle className="text-sm font-semibold text-foreground">{title}</CardTitle>
        </div>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="flex items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground">{meta ?? '—'}</span>
        <Button asChild size="sm" variant="outline">
          <Link to={to}>{actionLabel ?? title}</Link>
        </Button>
      </CardContent>
    </Card>
  )
}
