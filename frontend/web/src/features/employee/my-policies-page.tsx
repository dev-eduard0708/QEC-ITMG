import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { policiesApi, type EmployeePolicyItem } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'

type FilterKey = 'outstanding' | 'acknowledged' | 'all'

export function MyPoliciesPage() {
  const { t } = useTranslation()
  const [filter, setFilter] = useState<FilterKey>('outstanding')

  const summaryQuery = useQuery({
    queryKey: ['me', 'policies', 'summary'],
    queryFn: () => policiesApi.summary(),
  })
  const listQuery = useQuery({
    queryKey: ['me', 'policies', filter],
    queryFn: () => policiesApi.mine(filter),
  })

  const required = summaryQuery.data?.required ?? summaryQuery.data?.totalOutstandingVersions ?? 0
  const acknowledged = summaryQuery.data?.acknowledged ?? 0
  const outstanding = summaryQuery.data?.outstandingForUser ?? 0
  const overdue = summaryQuery.data?.overdue ?? 0

  const filters: { key: FilterKey; label: string }[] = [
    { key: 'outstanding', label: t('employee.policies.filter.needs') },
    { key: 'acknowledged', label: t('employee.policies.filter.acknowledged') },
    { key: 'all', label: t('employee.policies.filter.all') },
  ]

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader
        title={t('employee.policies.title')}
        description={t('employee.policies.description')}
      />

      <div className="grid gap-3 sm:grid-cols-4">
        <StatCard label={t('employee.policies.stats.required')} value={required} />
        <StatCard label={t('employee.policies.stats.acknowledged')} value={acknowledged} />
        <StatCard
          label={t('employee.policies.stats.outstanding')}
          value={outstanding}
          emphasize={outstanding > 0}
        />
        <StatCard label={t('employee.policies.stats.overdue')} value={overdue} warn={overdue > 0} />
      </div>

      <div className="flex flex-wrap gap-2">
        {filters.map((item) => (
          <button
            key={item.key}
            type="button"
            onClick={() => setFilter(item.key)}
            className={cn(
              'rounded-full border px-3 py-1.5 text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              filter === item.key
                ? 'border-primary bg-primary text-primary-foreground'
                : 'border-border bg-card text-muted-foreground hover:bg-muted/40',
            )}
          >
            {item.label}
          </button>
        ))}
      </div>

      <ul className="space-y-3">
        {(listQuery.data ?? []).map((item) => (
          <PolicyListCard key={`${item.managedDocumentId}-${item.documentVersionId}`} item={item} />
        ))}
        {!listQuery.isLoading && (listQuery.data?.length ?? 0) === 0 ? (
          <li className="rounded-2xl border border-dashed px-6 py-10 text-center text-sm text-muted-foreground">
            {filter === 'outstanding' ? t('employee.policies.upToDate') : t('employee.policies.none')}
          </li>
        ) : null}
      </ul>
    </div>
  )
}

function StatCard({
  label,
  value,
  emphasize,
  warn,
}: {
  label: string
  value: number
  emphasize?: boolean
  warn?: boolean
}) {
  return (
    <Card className={cn(warn && 'border-amber-500/40', emphasize && 'border-primary/30')}>
      <CardHeader className="pb-2">
        <CardTitle className="text-xs font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-semibold tabular-nums">{value}</div>
      </CardContent>
    </Card>
  )
}

function PolicyListCard({ item }: { item: EmployeePolicyItem }) {
  const { t } = useTranslation()
  const needsAction = item.status === 'NeedsAcknowledgement' || item.status === 'Overdue'
  const statusLabel =
    item.status === 'Overdue'
      ? t('employee.policies.badge.overdue')
      : item.status === 'Acknowledged'
        ? t('employee.policies.badge.acknowledged')
        : t('employee.policies.badge.needs')

  return (
    <li className="rounded-2xl border p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <div className="font-medium">{item.title}</div>
          <div className="text-sm text-muted-foreground">
            {item.documentNumber} · v{item.versionNumber}
          </div>
          {item.summary ? <p className="max-w-2xl text-sm text-muted-foreground">{item.summary}</p> : null}
          {item.dueAtUtc ? (
            <p className="text-xs text-muted-foreground">
              {t('employee.policies.due', { date: new Date(item.dueAtUtc).toLocaleString() })}
            </p>
          ) : null}
          {item.acknowledgedAtUtc ? (
            <p className="text-xs text-muted-foreground">
              {t('employee.policies.ackedOn', {
                date: new Date(item.acknowledgedAtUtc).toLocaleString(),
              })}
            </p>
          ) : null}
        </div>
        <div className="flex flex-col items-end gap-2">
          <Badge variant={item.status === 'Overdue' ? 'warning' : 'secondary'}>{statusLabel}</Badge>
          <Button asChild size="sm" variant={needsAction ? 'default' : 'outline'}>
            <Link to={`/employee/policies/${item.managedDocumentId}`}>
              {needsAction ? t('employee.policies.readAndAck') : t('employee.policies.read')}
            </Link>
          </Button>
        </div>
      </div>
    </li>
  )
}
