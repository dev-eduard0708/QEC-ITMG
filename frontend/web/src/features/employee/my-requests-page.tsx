import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { meApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { ticketKeys } from '@/features/it/query-keys'

export function MyRequestsPage() {
  const { t } = useTranslation()
  const query = useQuery({
    queryKey: ticketKeys.mine(''),
    queryFn: () => meApi.listTickets({ pageSize: 50 }),
  })

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('requests.title')}
        description={t('requests.description')}
        actions={
          <Button asChild>
            <Link to="/employee/requests/new">{t('requests.new')}</Link>
          </Button>
        }
      />

      {query.isLoading ? (
        <Skeleton className="h-40 w-full" />
      ) : (query.data?.items ?? []).length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('requests.empty')}</p>
      ) : (
        <div className="overflow-x-auto rounded-md border border-border">
          <table className="w-full min-w-[640px] text-sm">
            <thead className="bg-muted/40 text-start">
              <tr>
                <th className="px-3 py-2 text-start font-medium">{t('requests.columns.number')}</th>
                <th className="px-3 py-2 text-start font-medium">{t('requests.columns.title')}</th>
                <th className="px-3 py-2 text-start font-medium">{t('requests.columns.type')}</th>
                <th className="px-3 py-2 text-start font-medium">{t('requests.columns.status')}</th>
                <th className="px-3 py-2 text-start font-medium">{t('requests.columns.priority')}</th>
              </tr>
            </thead>
            <tbody>
              {(query.data?.items ?? []).map((ticket) => (
                <tr key={ticket.id} className="border-t border-border hover:bg-muted/30">
                  <td className="px-3 py-2">
                    <Link className="font-medium text-primary underline-offset-2 hover:underline" to={`/employee/requests/${ticket.id}`}>
                      {ticket.ticketNumber}
                    </Link>
                  </td>
                  <td className="px-3 py-2">{ticket.title}</td>
                  <td className="px-3 py-2">{ticket.type}</td>
                  <td className="px-3 py-2">
                    <Badge variant="secondary">{ticket.status}</Badge>
                  </td>
                  <td className="px-3 py-2">{ticket.priority}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
