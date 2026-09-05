import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { remoteSupportApi, type RemoteSessionRequest } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { remoteSupportKeys } from '@/features/it/query-keys'

function SessionCard({ session }: { session: RemoteSessionRequest }) {
  const { t } = useTranslation()

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div>
          <CardTitle className="text-base">{session.remoteNumber}</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">{session.reason}</p>
        </div>
        <Badge variant="secondary">
          {t(`remote.status.${session.status}`, { defaultValue: session.status })}
        </Badge>
      </CardHeader>
      <CardContent className="space-y-2 text-sm">
        <Row
          label={t('remote.fields.technician')}
          value={t('employee.remote.itTechnician')}
        />
        <Row
          label={t('remote.fields.requestedAt')}
          value={new Date(session.requestedAtUtc).toLocaleString()}
        />
        {session.expiresAtUtc ? (
          <Row
            label={t('remote.fields.expiresAt')}
            value={new Date(session.expiresAtUtc).toLocaleString()}
          />
        ) : null}
        <Button asChild size="sm" variant="outline">
          <Link to={`/employee/remote-support/${session.id}`}>{t('remote.viewDetail')}</Link>
        </Button>
      </CardContent>
    </Card>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-2">
      <span className="text-muted-foreground">{label}</span>
      <span>{value}</span>
    </div>
  )
}

export function EmployeeRemoteSupportPage() {
  const { t } = useTranslation()
  const query = useQuery({
    queryKey: remoteSupportKeys.mine(''),
    queryFn: () => remoteSupportApi.myList({ pageSize: 50 }),
  })

  return (
    <div className="space-y-6">
      <PageHeader title={t('employee.remote.listTitle')} description={t('employee.remote.listHint')} />

      {query.isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-28 w-full" />
          <Skeleton className="h-28 w-full" />
        </div>
      ) : (query.data?.items ?? []).length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('remote.myEmpty')}</p>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {(query.data?.items ?? []).map((session) => (
            <SessionCard key={session.id} session={session} />
          ))}
        </div>
      )}
    </div>
  )
}
