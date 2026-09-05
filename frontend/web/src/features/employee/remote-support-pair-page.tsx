import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { remoteSupportApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { remoteSupportKeys } from '@/features/it/query-keys'

export function EmployeeRemoteSupportPairPage() {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const code = (params.get('code') ?? '').trim()
  const queryClient = useQueryClient()

  const pairingQuery = useQuery({
    queryKey: remoteSupportKeys.pairingByCode(code),
    queryFn: () => remoteSupportApi.getPairingByCode(code),
    enabled: code.length >= 6,
    retry: false,
  })

  const authorizeMutation = useMutation({
    mutationFn: () => remoteSupportApi.authorizePairing(code),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.pairingByCode(code) })
      await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.setup() })
      await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.myEndpoints() })
    },
  })

  const rejectMutation = useMutation({
    mutationFn: () => remoteSupportApi.rejectPairing(code),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: remoteSupportKeys.pairingByCode(code) })
    },
  })

  const status = pairingQuery.data?.status
  const done = useMemo(
    () =>
      authorizeMutation.isSuccess ||
      status === 'Authorized' ||
      status === 'Completed',
    [authorizeMutation.isSuccess, status],
  )

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <PageHeader
        title={t('employee.remote.pairTitle')}
        description={t('employee.remote.pairHint')}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/remote-support/setup">{t('remote.back')}</Link>
          </Button>
        }
      />

      {!code ? (
        <p className="text-sm text-muted-foreground">{t('employee.remote.pairMissingCode')}</p>
      ) : pairingQuery.isLoading ? (
        <Skeleton className="h-40 w-full" />
      ) : pairingQuery.isError ? (
        <p className="text-sm text-destructive">{t('employee.remote.pairNotFound')}</p>
      ) : done ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('employee.remote.pairSuccess')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">{t('employee.remote.pairSuccessHint')}</p>
            <Button asChild>
              <Link to="/employee/remote-support/setup">{t('employee.remote.setupCta')}</Link>
            </Button>
          </CardContent>
        </Card>
      ) : status === 'Rejected' || status === 'Expired' ? (
        <p className="text-sm text-muted-foreground">
          {status === 'Expired'
            ? t('employee.remote.pairExpired')
            : t('employee.remote.pairCancelled')}
        </p>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('employee.remote.pairTitle')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">{t('employee.remote.pairConfirm')}</p>
            <p className="font-mono text-lg tracking-wide">{pairingQuery.data?.userCode ?? code}</p>
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                disabled={authorizeMutation.isPending}
                onClick={() => authorizeMutation.mutate()}
              >
                {t('employee.remote.pairAction')}
              </Button>
              <Button
                type="button"
                variant="outline"
                disabled={rejectMutation.isPending}
                onClick={() => rejectMutation.mutate()}
              >
                {t('admin.cancel')}
              </Button>
            </div>
            {authorizeMutation.isError ? (
              <p className="text-sm text-destructive">{t('employee.remote.pairFailed')}</p>
            ) : null}
          </CardContent>
        </Card>
      )}
    </div>
  )
}
