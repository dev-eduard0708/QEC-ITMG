import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { awarenessApi, type EmployeeAwarenessItem } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type FilterKey = 'outstanding' | 'completed' | 'all'

export function EmployeeAwarenessPage() {
  const { t } = useTranslation()
  const [filter, setFilter] = useState<FilterKey>('outstanding')

  const summaryQuery = useQuery({
    queryKey: ['me', 'security', 'awareness', 'summary'],
    queryFn: () => awarenessApi.mySummary(),
  })
  const listQuery = useQuery({
    queryKey: ['me', 'security', 'awareness', filter],
    queryFn: () => awarenessApi.mine(filter),
  })

  const assigned = summaryQuery.data?.assigned ?? 0
  const completed = summaryQuery.data?.completed ?? 0
  const outstanding = summaryQuery.data?.outstanding ?? 0
  const overdue = summaryQuery.data?.overdue ?? 0

  const filters: { key: FilterKey; label: string }[] = [
    { key: 'outstanding', label: t('employee.security.awareness.filter.todo') },
    { key: 'completed', label: t('employee.security.awareness.filter.completed') },
    { key: 'all', label: t('employee.security.awareness.filter.all') },
  ]

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader
        title={t('employee.security.awareness.title')}
        description={t('employee.security.awareness.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/security">{t('employee.security.back')}</Link>
          </Button>
        }
      />

      <div className="grid gap-3 sm:grid-cols-4">
        <StatCard label={t('employee.security.awareness.stats.assigned')} value={assigned} />
        <StatCard label={t('employee.security.awareness.stats.completed')} value={completed} />
        <StatCard
          label={t('employee.security.awareness.stats.outstanding')}
          value={outstanding}
          emphasize={outstanding > 0}
        />
        <StatCard
          label={t('employee.security.awareness.stats.overdue')}
          value={overdue}
          warn={overdue > 0}
        />
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
          <AwarenessListCard key={item.assignmentId} item={item} />
        ))}
        {!listQuery.isLoading && (listQuery.data?.length ?? 0) === 0 ? (
          <li className="rounded-2xl border border-dashed px-6 py-10 text-center text-sm text-muted-foreground">
            {filter === 'outstanding'
              ? t('employee.security.awareness.upToDate')
              : t('employee.security.awareness.none')}
          </li>
        ) : null}
      </ul>
    </div>
  )
}

function AwarenessListCard({ item }: { item: EmployeeAwarenessItem }) {
  const { t } = useTranslation()
  const completed = item.status === 'Completed'
  const actionLabel = completed
    ? t('employee.security.awareness.review')
    : item.status === 'InProgress'
      ? t('employee.security.awareness.continue')
      : t('employee.security.awareness.start')

  return (
    <li className="rounded-2xl border bg-card p-4 sm:p-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-base font-semibold">{item.title}</h2>
            <Badge variant={item.isOverdue ? 'warning' : completed ? 'secondary' : 'outline'}>
              {item.isOverdue
                ? t('employee.security.awareness.badge.overdue')
                : completed
                  ? t('employee.security.awareness.badge.completed')
                  : item.status === 'InProgress'
                    ? t('employee.security.awareness.badge.inProgress')
                    : t('employee.security.awareness.badge.assigned')}
            </Badge>
          </div>
          {item.summary ? <p className="text-sm text-muted-foreground">{item.summary}</p> : null}
          <p className="text-xs text-muted-foreground">
            {t('employee.security.awareness.minutes', { count: item.estimatedMinutes })}
            {item.dueAtUtc
              ? ` · ${t('employee.security.awareness.due', { date: new Date(item.dueAtUtc).toLocaleDateString() })}`
              : ''}
            {completed && item.completedAtUtc
              ? ` · ${t('employee.security.awareness.completedOn', { date: new Date(item.completedAtUtc).toLocaleDateString() })}`
              : ''}
            {completed && item.score != null
              ? ` · ${t('employee.security.awareness.score', { score: item.score })}`
              : ''}
          </p>
        </div>
        <Button asChild className="min-h-11 shrink-0">
          <Link to={`/employee/security/awareness/${item.assignmentId}`}>{actionLabel}</Link>
        </Button>
      </div>
    </li>
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
    <div
      className={cn(
        'rounded-2xl border px-4 py-3',
        warn ? 'border-amber-500/40 bg-amber-500/5' : emphasize ? 'border-primary/30 bg-primary/5' : 'bg-card',
      )}
    >
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-2xl font-semibold tabular-nums">{value}</p>
    </div>
  )
}
