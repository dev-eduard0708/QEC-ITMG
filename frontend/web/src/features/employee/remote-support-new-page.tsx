import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError, remoteSupportApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { remoteSupportKeys } from '@/features/it/query-keys'

type DeviceChoice = 'this-computer' | 'company-device'

export function EmployeeRemoteSupportNewPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [reason, setReason] = useState('')
  const [deviceChoice, setDeviceChoice] = useState<DeviceChoice>('this-computer')
  const [configurationItemId, setConfigurationItemId] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const onboardingQuery = useQuery({
    queryKey: remoteSupportKeys.onboarding(),
    queryFn: () => remoteSupportApi.onboarding(),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      remoteSupportApi.createSelfHelp({
        reason: reason.trim(),
        configurationItemId:
          deviceChoice === 'company-device' ? configurationItemId : null,
      }),
    onSuccess: (created) => navigate(`/employee/remote-support/${created.id}`),
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('remote.error.generic'))
    },
  })

  const devices = (onboardingQuery.data?.devices ?? []).filter(
    (device) => Boolean(device.configurationItemId),
  )

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
              if (
                !reason.trim() ||
                (deviceChoice === 'company-device' && !configurationItemId)
              ) {
                setFormError(t('remote.error.required'))
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
              <legend className="text-sm font-medium">{t('employee.remote.new.affectedDevice')}</legend>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="device-choice"
                  value="this-computer"
                  checked={deviceChoice === 'this-computer'}
                  onChange={() => setDeviceChoice('this-computer')}
                />
                {t('employee.remote.new.thisComputer')}
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="device-choice"
                  value="company-device"
                  checked={deviceChoice === 'company-device'}
                  onChange={() => setDeviceChoice('company-device')}
                />
                {t('employee.remote.new.companyDevice')}
              </label>
            </fieldset>

            {deviceChoice === 'company-device' ? (
              <div className="space-y-2">
                <Label>{t('employee.remote.new.selectDevice')}</Label>
                <Select value={configurationItemId} onValueChange={setConfigurationItemId}>
                  <SelectTrigger>
                    <SelectValue placeholder={t('employee.remote.new.selectDevicePlaceholder')} />
                  </SelectTrigger>
                  <SelectContent>
                    {devices.map((device) => (
                      <SelectItem
                        key={device.assetId}
                        value={device.configurationItemId!}
                      >
                        {device.assetName} — {device.assetNumber}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {!onboardingQuery.isLoading && devices.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    {t('employee.remote.new.noCompanyDevices')}
                  </p>
                ) : null}
              </div>
            ) : null}

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
