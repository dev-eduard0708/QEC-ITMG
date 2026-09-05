import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Plus } from 'lucide-react'
import { meApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { ticketKeys } from '@/features/it/query-keys'
import {
  friendlyStatusKey,
  friendlyTicketTypeKey,
  isOpenTicketStatus,
  isWaitingForEmployee,
} from '@/features/employee/employee-request-helpers'
import { cn } from '@/lib/utils'

type FilterKey = 'all' | 'open' | 'waiting' | 'resolved'

export function MyRequestsPage() {
  const { t } = useTranslation()
  const [filter, setFilter] = useState<FilterKey>('all')

  const query = useQuery({
    queryKey: ticketKeys.mine(''),
    queryFn: () => meApi.listTickets({ pageSize: 50 }),
  })

  const items = useMemo(() => {
    const list = query.data?.items ?? []
    switch (filter) {
      case 'open':
        return list.filter((item) => isOpenTicketStatus(item.status))
      case 'waiting':
        return list.filter((item) => isWaitingForEmployee(item.status))
      case 'resolved':
        return list.filter((item) => item.status === 'Resolved' || item.status === 'Closed')
      default:
        return list
    }
  }, [filter, query.data?.items])

  const filters: { key: FilterKey; label: string }[] = [
    { key: 'all', label: t('employee.filters.all') },
    { key: 'open', label: t('employee.filters.open') },
    { key: 'waiting', label: t('employee.filters.waiting') },
    { key: 'resolved', label: t('employee.filters.resolved') },
  ]

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <PageHeader
        title={t('requests.title')}
        description={t('employee.requestsListHint')}
        actions={
          <Button asChild size="lg">
            <Link to="/employee/requests/new">
              <Plus className="me-1.5 h-4 w-4" aria-hidden />
              {t('employee.actions.getHelp')}
            </Link>
          </Button>
        }
      />

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

      {query.isLoading ? (
        <Skeleton className="h-40 w-full" />
      ) : items.length === 0 ? (
        <div className="rounded-2xl border border-dashed px-6 py-12 text-center">
          <p className="text-sm text-muted-foreground">{t('requests.empty')}</p>
          <Button asChild className="mt-4">
            <Link to="/employee/requests/new">{t('employee.actions.getHelp')}</Link>
          </Button>
        </div>
      ) : (
        <ul className="space-y-3">
          {items.map((ticket) => (
            <li key={ticket.id}>
              <Link
                to={`/employee/requests/${ticket.id}`}
                className="block rounded-2xl border bg-card p-4 transition-colors hover:bg-muted/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="space-y-1">
                    <div className="text-sm font-semibold text-primary">{ticket.ticketNumber}</div>
                    <div className="text-base font-medium">{ticket.title}</div>
                    <div className="flex flex-wrap gap-2 text-xs text-muted-foreground">
                      <span>{t(friendlyTicketTypeKey(ticket.type))}</span>
                      <span>·</span>
                      <span>
                        {t('employee.updatedAt', {
                          date: new Date(ticket.updatedAtUtc).toLocaleString(),
                        })}
                      </span>
                    </div>
                  </div>
                  <Badge variant="secondary">{t(friendlyStatusKey(ticket.status))}</Badge>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
