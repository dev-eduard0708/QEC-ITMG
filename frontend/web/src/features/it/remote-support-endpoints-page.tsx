import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { ApiError, remoteSupportApi, type RemoteEndpoint } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { RemoteDeviceCard } from '@/features/remote-support/remote-device-card'
import { remoteSupportKeys } from '@/features/it/query-keys'

function EndpointActions({ endpoint }: { endpoint: RemoteEndpoint }) {
  const { t } = useTranslation()
  const { can } = useAuth()
  const queryClient = useQueryClient()
  const [configurationItemId, setConfigurationItemId] = useState(
    endpoint.configurationItemId ?? '',
  )
  const [error, setError] = useState<string | null>(null)

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: [...remoteSupportKeys.all, 'endpoints'] })

  const expireMutation = useMutation({
    mutationFn: () => remoteSupportApi.expireEndpoint(endpoint.id),
    onSuccess: refresh,
    onError: (value) =>
      setError(value instanceof ApiError ? value.message : t('remote.error.generic')),
  })

  const linkMutation = useMutation({
    mutationFn: () => remoteSupportApi.linkEndpointCi(endpoint.id, configurationItemId),
    onSuccess: refresh,
    onError: (value) =>
      setError(value instanceof ApiError ? value.message : t('remote.error.generic')),
  })

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2">
        {endpoint.currentRemoteSessionRequestId ? (
          <Button asChild size="sm" variant="outline">
            <Link to={`/it/remote-support/${endpoint.currentRemoteSessionRequestId}`}>
              {t('remote.endpoints.viewRequest')}
            </Link>
          </Button>
        ) : null}
        {can('remote.admin') ? (
          <Button
            type="button"
            size="sm"
            variant="secondary"
            onClick={() => expireMutation.mutate()}
            disabled={expireMutation.isPending}
          >
            {t('remote.endpoints.expire')}
          </Button>
        ) : null}
      </div>
      {can('remote.admin') ? (
        <div className="flex flex-wrap gap-2">
          <Input
            className="max-w-sm"
            value={configurationItemId}
            onChange={(event) => setConfigurationItemId(event.target.value)}
            placeholder={t('remote.endpoints.ciPlaceholder')}
          />
          <Button
            type="button"
            size="sm"
            onClick={() => linkMutation.mutate()}
            disabled={!configurationItemId || linkMutation.isPending}
          >
            {t('remote.endpoints.linkCi')}
          </Button>
        </div>
      ) : null}
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  )
}

export function RemoteSupportEndpointsPage() {
  const { t } = useTranslation()
  const [kind, setKind] = useState('all')
  const [status, setStatus] = useState('all')

  const endpointsQuery = useQuery({
    queryKey: [...remoteSupportKeys.all, 'endpoints', kind, status],
    queryFn: () =>
      remoteSupportApi.listEndpoints({
        kind: kind === 'all' ? undefined : kind,
        status: status === 'all' ? undefined : status,
        includeExpired: true,
        take: 100,
      }),
  })

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('remote.endpoints.title')}
        description={t('remote.endpoints.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/it/remote-support">{t('remote.back')}</Link>
          </Button>
        }
      />
      <div className="flex flex-wrap gap-3">
        <Select value={kind} onValueChange={setKind}>
          <SelectTrigger className="w-48">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('remote.endpoints.allKinds')}</SelectItem>
            <SelectItem value="Temporary">{t('remote.device.kindValue.Temporary')}</SelectItem>
            <SelectItem value="Managed">{t('remote.device.kindValue.Managed')}</SelectItem>
          </SelectContent>
        </Select>
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="w-48">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('remote.endpoints.allStatuses')}</SelectItem>
            <SelectItem value="Online">{t('remote.endpoints.online')}</SelectItem>
            <SelectItem value="Offline">{t('remote.endpoints.offline')}</SelectItem>
          </SelectContent>
        </Select>
      </div>
      {endpointsQuery.isLoading ? (
        <p className="text-sm text-muted-foreground">{t('remote.endpoints.loading')}</p>
      ) : (endpointsQuery.data ?? []).length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('remote.endpoints.empty')}</p>
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {(endpointsQuery.data ?? []).map((endpoint) => (
            <Card key={endpoint.id}>
              <CardContent className="space-y-4 pt-6">
                <RemoteDeviceCard endpoint={endpoint} variant="technician" />
                <EndpointActions endpoint={endpoint} />
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
