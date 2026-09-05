import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import {
  ApiError,
  meApi,
  remoteSupportApi,
  type EnrollmentIssueResult,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
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
import { remoteSupportKeys, ticketKeys } from '@/features/it/query-keys'
import { RemoteSessionChat } from '@/features/remote-support/remote-session-chat'
import { RemoteDeviceCard } from '@/features/remote-support/remote-device-card'
import {
  friendlySessionStatusKey,
  sessionStatusVariant,
} from '@/features/remote-support/employee-remote-helpers'
import { isChatOpen } from '@/features/remote-support/chat-window'

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
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [formError, setFormError] = useState<string | null>(null)
  const [enrollment, setEnrollment] = useState<EnrollmentIssueResult | null>(null)

  const sessionQuery = useQuery({
    queryKey: remoteSupportKeys.mineDetail(id),
    queryFn: () => remoteSupportApi.myGet(id),
    enabled: Boolean(id),
  })

  const endpointQuery = useQuery({
    queryKey: [...remoteSupportKeys.mineDetail(id), 'endpoint'],
    queryFn: () => remoteSupportApi.getMyEndpoint(id),
    enabled: Boolean(id),
    refetchInterval: (query) => (query.state.data ? false : 5_000),
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

  const enrollmentMutation = useMutation({
    mutationFn: () => remoteSupportApi.issueEnrollment(id),
    onSuccess: (issued) => {
      setEnrollment(issued)
      setFormError(null)
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  const mockMutation = useMutation({
    mutationFn: () => remoteSupportApi.devMockEndpoint(id),
    onSuccess: async () => {
      setFormError(null)
      await endpointQuery.refetch()
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

  const awaitingConsent = session.status === 'NotifyUser'
  const inSession = session.status === 'InSession'
  const endpoint = endpointQuery.data
  const preparingThisComputer = !endpoint && !session.configurationItemId

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

      <Badge variant={sessionStatusVariant(session.status)}>
        {t(friendlySessionStatusKey(session.status))}
      </Badge>

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}

      {endpoint ? <RemoteDeviceCard endpoint={endpoint} variant="employee" /> : null}

      {preparingThisComputer ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('employee.remote.prepare.title')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">
              {t('employee.remote.prepare.privacy')}
            </p>
            <p className="text-sm text-muted-foreground">
              {t('employee.remote.prepare.oneTimeWarning')}
            </p>
            <p className="text-sm font-medium text-amber-700 dark:text-amber-300">
              {t('employee.remote.prepare.doNotShare')}
            </p>
            {!enrollment ? (
              <Button
                type="button"
                onClick={() => enrollmentMutation.mutate()}
                disabled={enrollmentMutation.isPending}
              >
                {t('employee.remote.prepare.download')}
              </Button>
            ) : enrollment.helperDownloadConfigured && enrollment.helperDownloadUrl ? (
              <>
                <Button asChild>
                  <a
                    href={enrollment.helperDownloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    {t('employee.remote.prepare.download')}
                  </a>
                </Button>
                {enrollment.helperInstallInstructions ? (
                  <p className="whitespace-pre-wrap text-sm text-muted-foreground">
                    {enrollment.helperInstallInstructions}
                  </p>
                ) : null}
              </>
            ) : (
              <p className="text-sm text-muted-foreground">
                {t('employee.remote.prepare.unavailable')}
              </p>
            )}
            {import.meta.env.DEV ? (
              <Button
                type="button"
                variant="outline"
                onClick={() => mockMutation.mutate()}
                disabled={mockMutation.isPending}
              >
                {t('employee.remote.prepare.developmentMock')}
              </Button>
            ) : null}
            <p className="text-xs text-muted-foreground">
              {t('employee.remote.prepare.waiting')}
            </p>
          </CardContent>
        </Card>
      ) : null}

      {awaitingConsent ? (
        <Card className="border-2 border-amber-400 bg-amber-50/60 dark:bg-amber-950/30">
          <CardHeader>
            <CardTitle className="text-base">{t('employee.remote.consentPrompt')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm">{t('remote.consent.warning')}</p>
            <p className="text-sm text-muted-foreground">{t('employee.remote.chatNotConsent')}</p>
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

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('employee.remote.connectTitle')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <DetailRow label={t('remote.fields.technician')} value={t('employee.remote.itTechnician')} />
          <DetailRow label={t('remote.fields.reason')} value={session.reason} />
          <DetailRow
            label={t('employee.remote.device')}
            value={endpoint?.deviceName ?? t('employee.remote.yourDevice')}
          />
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

      <RemoteSessionChat
        sessionId={session.id}
        currentUserId={user?.id ?? null}
        canPost={isChatOpen(session)}
      />

      {inSession ? (
        <Card>
          <CardContent className="space-y-2 pt-6">
            <p className="text-sm text-muted-foreground">{t('employee.remote.endHint')}</p>
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
