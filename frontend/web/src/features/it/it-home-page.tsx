import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ticketsApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'

type Widget = {
  key: string
  labelKey: string
  value: number
  to: string
}

export function ItHomePage() {
  const { t } = useTranslation()
  const { can } = useAuth()

  const dashboardQuery = useQuery({
    queryKey: ['tickets', 'dashboard'],
    queryFn: () => ticketsApi.dashboard(),
    enabled: can('tickets.read'),
  })

  const widgets: Widget[] = dashboardQuery.data
    ? [
        {
          key: 'open',
          labelKey: 'dashboard.openTickets',
          value: dashboardQuery.data.openTickets,
          to: '/it/tickets',
        },
        {
          key: 'unassigned',
          labelKey: 'dashboard.unassigned',
          value: dashboardQuery.data.unassigned,
          to: '/it/tickets',
        },
        {
          key: 'critical',
          labelKey: 'dashboard.criticalOpen',
          value: dashboardQuery.data.criticalOpen,
          to: '/it/tickets?priority=Critical',
        },
        {
          key: 'sla',
          labelKey: 'dashboard.slaBreached',
          value: dashboardQuery.data.slaBreached,
          to: '/it/tickets',
        },
        {
          key: 'mine',
          labelKey: 'dashboard.myAssigned',
          value: dashboardQuery.data.myAssigned,
          to: '/it/tickets',
        },
        {
          key: 'newToday',
          labelKey: 'dashboard.newToday',
          value: dashboardQuery.data.newToday,
          to: '/it/tickets',
        },
        {
          key: 'resolvedToday',
          labelKey: 'dashboard.resolvedToday',
          value: dashboardQuery.data.resolvedToday,
          to: '/it/tickets?status=Resolved',
        },
      ]
    : []

  return (
    <div className="space-y-6">
      <PageHeader title={t('it.title')} description={t('it.description')} />

      {can('tickets.read') ? (
        <section className="space-y-3">
          <h2 className="text-sm font-semibold">{t('dashboard.title')}</h2>
          {dashboardQuery.isLoading ? (
            <Skeleton className="h-28 w-full" />
          ) : (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              {widgets.map((widget) => (
                <Link
                  key={widget.key}
                  to={widget.to}
                  className="rounded-md border border-border bg-card px-4 py-3 transition-colors hover:bg-muted/40"
                >
                  <p className="text-xs text-muted-foreground">{t(widget.labelKey)}</p>
                  <p className="mt-1 text-2xl font-semibold tabular-nums">{widget.value}</p>
                </Link>
              ))}
            </div>
          )}
        </section>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('nav.it')}</CardTitle>
            <CardDescription>{t('it.homeHint')}</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {can('tickets.read') ? (
              <Button asChild>
                <Link to="/it/tickets">{t('nav.tickets')}</Link>
              </Button>
            ) : null}
            {can('kb.read') ? (
              <Button asChild variant="secondary">
                <Link to="/it/knowledge">{t('nav.knowledge')}</Link>
              </Button>
            ) : null}
            {can('assets.read') ? (
              <Button asChild variant="secondary">
                <Link to="/it/assets">{t('nav.assets')}</Link>
              </Button>
            ) : null}
            {can('cmdb.read') ? (
              <Button asChild variant="secondary">
                <Link to="/it/cmdb">{t('nav.cmdb')}</Link>
              </Button>
            ) : null}
          </CardContent>
        </Card>
        {(can('admin.users') || can('admin.roles') || can('admin.lookups')) ? (
          <Card>
            <CardHeader>
              <CardTitle>{t('admin.title')}</CardTitle>
              <CardDescription>{t('admin.description')}</CardDescription>
            </CardHeader>
            <CardContent>
              <Button asChild>
                <Link to="/it/admin/users">{t('admin.nav.users')}</Link>
              </Button>
            </CardContent>
          </Card>
        ) : null}
      </div>
    </div>
  )
}
