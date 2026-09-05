import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { ApiError, remoteSupportApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { remoteSupportKeys } from '@/features/it/query-keys'

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-2 text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="max-w-[70%] break-all text-end">{value}</span>
    </div>
  )
}

export function EmployeeRemoteSupportDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [formError, setFormError] = useState<string | null>(null)

  const sessionQuery = useQuery({
    queryKey: remoteSupportKeys.mineDetail(id),
    queryFn: () => remoteSupportApi.myGet(id),
    enabled: Boolean(id),
  })

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.mineDetail(id) })
    await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.mine('') })
  }

  const allowMutation = useMutation({
    mutationFn: () => remoteSupportApi.allow(id),
    onSuccess: async () => {
      setFormError(null)
      await invalidate()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  const declineMutation = useMutation({
    mutationFn: () => remoteSupportApi.decline(id),
    onSuccess: async () => {
      setFormError(null)
      await invalidate()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  const endMutation = useMutation({
    mutationFn: () => remoteSupportApi.myEnd(id),
    onSuccess: async () => {
      setFormError(null)
      await invalidate()
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  if (sessionQuery.isLoading) {
    return <Skeleton className="h-40 w-full" />
  }

  const session = sessionQuery.data
  if (!session) {
    return <p className="text-sm text-muted-foreground">{t('remote.notFound')}</p>
  }

  const awaitingConsent = session.status === 'NotifyUser' || session.status === 'Requested'
  const inSession = session.status === 'InSession'

  return (
    <div className="space-y-6">
      <PageHeader
        title={session.remoteNumber}
        description={t('remote.myDetailDescription')}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/remote-support">{t('remote.back')}</Link>
          </Button>
        }
      />

      <Badge variant="secondary">
        {t(`remote.status.${session.status}`, { defaultValue: session.status })}
      </Badge>

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('remote.sections.request')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <DetailRow
            label={t('remote.fields.technician')}
            value={session.technicianUserId?.slice(0, 8) ?? '—'}
          />
          <DetailRow label={t('remote.fields.reason')} value={session.reason} />
          <DetailRow
            label={t('remote.fields.ticket')}
            value={session.ticketId?.slice(0, 8) ?? '—'}
          />
          <DetailRow
            label={t('remote.fields.configurationItem')}
            value={session.configurationItemId.slice(0, 8)}
          />
          <DetailRow
            label={t('remote.fields.privileges')}
            value={session.requestedPrivileges ?? '—'}
          />
          <DetailRow
            label={t('remote.fields.expiresAt')}
            value={session.expiresAtUtc ? new Date(session.expiresAtUtc).toLocaleString() : '—'}
          />
        </CardContent>
      </Card>

      {awaitingConsent ? (
        <Card className="border-warning/40">
          <CardHeader>
            <CardTitle className="text-base">{t('remote.consent.title')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">{t('remote.consent.warning')}</p>
            <div className="flex flex-wrap gap-2">
              <AlertDialog>
                <AlertDialogTrigger asChild>
                  <Button type="button">{t('remote.consent.allow')}</Button>
                </AlertDialogTrigger>
                <AlertDialogContent>
                  <AlertDialogHeader>
                    <AlertDialogTitle>{t('remote.consent.confirmTitle')}</AlertDialogTitle>
                    <AlertDialogDescription>{t('remote.consent.confirmBody')}</AlertDialogDescription>
                  </AlertDialogHeader>
                  <AlertDialogFooter>
                    <AlertDialogCancel>{t('admin.cancel')}</AlertDialogCancel>
                    <AlertDialogAction
                      onClick={() => allowMutation.mutate()}
                      disabled={allowMutation.isPending}
                    >
                      {t('remote.consent.allow')}
                    </AlertDialogAction>
                  </AlertDialogFooter>
                </AlertDialogContent>
              </AlertDialog>
              <Button
                type="button"
                variant="secondary"
                onClick={() => declineMutation.mutate()}
                disabled={declineMutation.isPending}
              >
                {t('remote.consent.decline')}
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {inSession ? (
        <Card>
          <CardContent className="pt-6">
            <Button
              type="button"
              variant="secondary"
              onClick={() => endMutation.mutate()}
              disabled={endMutation.isPending}
            >
              {t('remote.actions.endAsUser')}
            </Button>
          </CardContent>
        </Card>
      ) : null}
    </div>
  )
}
