import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMemo, useState } from 'react'
import { ApiError, meApi, remoteSupportApi } from '@/api/client'
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
import { equipmentKeys, remoteSupportKeys, ticketKeys } from '@/features/it/query-keys'
import { formatDeviceLabel } from '@/features/employee/employee-request-helpers'

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-2 text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="max-w-[70%] text-end">{value}</span>
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

  const equipmentQuery = useQuery({
    queryKey: equipmentKeys.mine,
    queryFn: () => meApi.listEquipment(),
  })

  const ticketId = sessionQuery.data?.ticketId
  const ticketQuery = useQuery({
    queryKey: ticketKeys.mineDetail(ticketId ?? ''),
    queryFn: () => meApi.getTicket(ticketId!),
    enabled: Boolean(ticketId),
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

  const deviceLabel = useMemo(() => {
    const session = sessionQuery.data
    if (!session) return t('employee.remote.yourDevice')
    const match = (equipmentQuery.data ?? []).find(
      (asset) => asset.configurationItemId === session.configurationItemId,
    )
    if (match) return formatDeviceLabel(match)
    return t('employee.remote.yourDevice')
  }, [equipmentQuery.data, sessionQuery.data, t])

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
    <div className="mx-auto max-w-2xl space-y-6">
      <PageHeader
        title={t('employee.remote.connectTitle')}
        description={session.remoteNumber}
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
          <CardTitle className="text-base">{t('employee.remote.connectTitle')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <DetailRow label={t('remote.fields.technician')} value={t('employee.remote.itTechnician')} />
          <DetailRow label={t('remote.fields.reason')} value={session.reason} />
          <DetailRow label={t('employee.remote.device')} value={deviceLabel} />
          <DetailRow
            label={t('employee.remote.relatedRequest')}
            value={ticketQuery.data?.ticketNumber ?? t('employee.remote.noRelatedRequest')}
          />
          <DetailRow
            label={t('remote.fields.privileges')}
            value={session.requestedPrivileges ?? t('employee.remote.standardAccess')}
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
            <CardTitle className="text-base">{t('employee.remote.consentPrompt')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">{t('remote.consent.warning')}</p>
            <div className="flex flex-wrap gap-2">
              <AlertDialog>
                <AlertDialogTrigger asChild>
                  <Button type="button">{t('employee.remote.allow')}</Button>
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
                      {t('employee.remote.allow')}
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
                {t('employee.remote.decline')}
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
