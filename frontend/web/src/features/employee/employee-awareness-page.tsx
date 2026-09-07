import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  awarenessApi,
  awarenessV1Api,
  type EmployeeAwarenessFilter,
  type EmployeeAwarenessItem,
  type EmployeeAwarenessV1Item,
} from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { isAppLanguage } from '@/i18n'

type FilterKey = EmployeeAwarenessFilter

type UnifiedItem = {
  assignmentId: string
  title: string
  summary: string | null
  estimatedMinutes: number
  dueAtUtc: string | null
  status: string
  completedAtUtc: string | null
  score: number | null
  isOverdue: boolean
  requireQuiz: boolean
  source: 'v1' | 'legacy'
}

export function EmployeeAwarenessPage() {
  const { t, i18n } = useTranslation()
  const [filter, setFilter] = useState<FilterKey>('outstanding')
  const lang = isAppLanguage(i18n.language) ? i18n.language : 'en'

  const v1Query = useQuery({
    queryKey: ['me', 'awareness', 'v1', filter],
    queryFn: () => awarenessV1Api.myList(filter),
  })
  const legacyQuery = useQuery({
    queryKey: ['me', 'security', 'awareness', filter],
    queryFn: () => awarenessApi.mine(filter),
  })
  const summaryQuery = useQuery({
    queryKey: ['me', 'security', 'awareness', 'summary'],
    queryFn: () => awarenessApi.mySummary(),
  })

  const items = useMemo(() => {
    const v1 = (v1Query.data ?? []).map((item) => mapV1(item, lang))
    const legacyIds = new Set(v1.map((x) => x.assignmentId))
    const legacy = (legacyQuery.data ?? [])
      .filter((item) => !legacyIds.has(item.assignmentId))
      .map(mapLegacy)
    return [...v1, ...legacy]
  }, [v1Query.data, legacyQuery.data, lang])

  const assigned = Math.max(summaryQuery.data?.assigned ?? 0, items.length)
  const completed =
    summaryQuery.data?.completed ?? items.filter((x) => x.status === 'Completed').length
  const outstanding =
    summaryQuery.data?.outstanding ??
    items.filter((x) => x.status !== 'Completed' && x.status !== 'Exempt').length
  const overdue =
    summaryQuery.data?.overdue ?? items.filter((x) => x.isOverdue || x.status === 'Overdue').length

  const filters: { key: FilterKey; label: string }[] = [
    { key: 'outstanding', label: t('employee.security.awareness.filter.todo') },
    { key: 'completed', label: t('employee.security.awareness.filter.completed') },
    { key: 'all', label: t('employee.security.awareness.filter.all') },
  ]

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader
        title={t('nav.myAwareness')}
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
        {items.map((item) => (
          <AwarenessListCard key={item.assignmentId} item={item} />
        ))}
        {!v1Query.isLoading && !legacyQuery.isLoading && items.length === 0 ? (
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

function mapV1(item: EmployeeAwarenessV1Item, lang: 'en' | 'ar'): UnifiedItem {
  const title =
    lang === 'ar' && item.titleAr?.trim() ? item.titleAr : item.titleEn
  const summary =
    lang === 'ar' && item.descriptionAr?.trim()
      ? item.descriptionAr
      : item.descriptionEn
  return {
    assignmentId: item.assignmentId,
    title,
    summary,
    estimatedMinutes: item.estimatedMinutes,
    dueAtUtc: item.dueAtUtc,
    status: item.status === 'NotStarted' ? 'Assigned' : item.status,
    completedAtUtc: item.completedAtUtc,
    score: item.score,
    isOverdue: item.isOverdue,
    requireQuiz: item.requireQuiz,
    source: 'v1',
  }
}

function mapLegacy(item: EmployeeAwarenessItem): UnifiedItem {
  return {
    assignmentId: item.assignmentId,
    title: item.title,
    summary: item.summary,
    estimatedMinutes: item.estimatedMinutes,
    dueAtUtc: item.dueAtUtc,
    status: item.status,
    completedAtUtc: item.completedAtUtc,
    score: item.score,
    isOverdue: item.isOverdue,
    requireQuiz: true,
    source: 'legacy',
  }
}

function AwarenessListCard({ item }: { item: UnifiedItem }) {
  const { t } = useTranslation()
  const completed = item.status === 'Completed'
  const actionLabel = completed
    ? t('employee.security.awareness.review')
    : item.status === 'InProgress'
      ? t('employee.security.awareness.continue')
      : item.requireQuiz && item.status !== 'Assigned' && item.status !== 'NotStarted'
        ? t('employee.awareness.takeQuiz')
        : t('employee.security.awareness.start')

  const to =
    item.source === 'v1'
      ? `/employee/awareness/${item.assignmentId}`
      : `/employee/security/awareness/${item.assignmentId}`

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
              ? ` · ${t('employee.security.awareness.due', {
                  date: new Date(item.dueAtUtc).toLocaleDateString(),
                })}`
              : ''}
            {item.completedAtUtc
              ? ` · ${t('employee.security.awareness.completedOn', {
                  date: new Date(item.completedAtUtc).toLocaleDateString(),
                })}`
              : ''}
            {item.score != null
              ? ` · ${t('employee.security.awareness.score', { score: item.score })}`
              : ''}
          </p>
        </div>
        <Button asChild className="min-h-11 shrink-0">
          <Link to={to}>{actionLabel}</Link>
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
        warn && 'border-amber-500/50 bg-amber-500/5',
        emphasize && !warn && 'border-primary/40 bg-primary/5',
      )}
    >
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-2xl font-semibold tabular-nums">{value}</div>
    </div>
  )
}
