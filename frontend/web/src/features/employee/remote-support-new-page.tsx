import { useMutation, useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError, isRemoteEndpointReady, remoteSupportApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { remoteSupportKeys } from '@/features/it/query-keys'

export function EmployeeRemoteSupportNewPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [reason, setReason] = useState('')
  const [selectedEndpointId, setSelectedEndpointId] = useState<string>('')
  const [formError, setFormError] = useState<string | null>(null)

  const endpointsQuery = useQuery({
    queryKey: remoteSupportKeys.myEndpoints(),
    queryFn: () => remoteSupportApi.listMyEndpoints(),
  })

  const endpoints = endpointsQuery.data ?? []
  const readyEndpoints = useMemo(
    () => endpoints.filter(isRemoteEndpointReady),
    [endpoints],
  )

  const effectiveEndpointId = useMemo(() => {
    if (selectedEndpointId) return selectedEndpointId
    if (readyEndpoints.length === 1) return readyEndpoints[0].id
    return ''
  }, [readyEndpoints, selectedEndpointId])

  const createMutation = useMutation({
    mutationFn: () =>
      remoteSupportApi.createSelfHelp({
        reason: reason.trim(),
        remoteEndpointId: effectiveEndpointId || null,
        configurationItemId: null,
      }),
    onSuccess: (created) => navigate(`/employee/remote-support/${created.id}`),
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageHeader
        title={t('employee.remote.new.title')}
        description={t('employee.remote.new.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/remote-support">{t('remote.back')}</Link>
          </Button>
        }
      />

      <Card>
        <CardContent className="pt-6">
          <form
            className="space-y-5"
            onSubmit={(event) => {
              event.preventDefault()
              if (!reason.trim()) {
                setFormError(t('remote.error.required'))
                return
              }
              if (readyEndpoints.length > 1 && !effectiveEndpointId) {
                setFormError(t('employee.remote.new.selectComputerRequired'))
                return
              }
              setFormError(null)
              createMutation.mutate()
            }}
          >
            <div className="space-y-2">
              <Label htmlFor="remote-help-reason">{t('employee.remote.new.helpQuestion')}</Label>
              <Textarea
                id="remote-help-reason"
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                rows={5}
                required
              />
            </div>

            <fieldset className="space-y-3">
              <legend className="text-sm font-medium">{t('employee.remote.new.affectedComputer')}</legend>
              {readyEndpoints.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  {t('employee.remote.new.noReadyHint')}
                </p>
              ) : readyEndpoints.length === 1 ? (
                <p className="text-sm">
                  <span className="me-2" aria-hidden>
                    ●
                  </span>
                  {readyEndpoints[0].deviceName}
                  <span className="ms-2 text-muted-foreground">
                    {t('employee.remote.readyForSupport')}
                  </span>
                </p>
              ) : (
                <ul className="space-y-2">
                  {readyEndpoints.map((endpoint) => (
                    <li key={endpoint.id}>
                      <label className="flex items-center gap-2 text-sm">
                        <input
                          type="radio"
                          name="endpoint"
                          value={endpoint.id}
                          checked={effectiveEndpointId === endpoint.id}
                          onChange={() => setSelectedEndpointId(endpoint.id)}
                        />
                        <span>
                          ● {endpoint.deviceName}
                          <span className="ms-2 text-muted-foreground">
                            {[endpoint.operatingSystem, endpoint.architecture]
                              .filter(Boolean)
                              .join(' ')}
                          </span>
                        </span>
                      </label>
                    </li>
                  ))}
                </ul>
              )}
            </fieldset>

            {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
            <Button type="submit" disabled={createMutation.isPending}>
              {t('employee.remote.new.submit')}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
