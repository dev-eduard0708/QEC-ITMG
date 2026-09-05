import { useMutation, useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  ApiError,
  meApi,
  SECURITY_CONCERN_CATEGORIES,
  type SecurityConcernCategory,
  type Ticket,
} from '@/api/client'
import { formatDeviceLabel } from '@/features/employee/employee-request-helpers'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { cn } from '@/lib/utils'

type DeviceChoice = 'equipment' | 'service' | 'other' | 'not_sure' | null

export function ReportSecurityConcernPage() {
  const { t } = useTranslation()
  const [category, setCategory] = useState<SecurityConcernCategory | null>(null)
  const [description, setDescription] = useState('')
  const [noticedAt, setNoticedAt] = useState('')
  const [sender, setSender] = useState('')
  const [subject, setSubject] = useState('')
  const [suspiciousReason, setSuspiciousReason] = useState('')
  const [deviceChoice, setDeviceChoice] = useState<DeviceChoice>(null)
  const [equipmentId, setEquipmentId] = useState<string | null>(null)
  const [serviceLabel, setServiceLabel] = useState('')
  const [otherDevice, setOtherDevice] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attachWarning, setAttachWarning] = useState<string | null>(null)
  const [created, setCreated] = useState<Ticket | null>(null)

  const equipmentQuery = useQuery({
    queryKey: ['me', 'equipment', 'security-report'],
    queryFn: () => meApi.listEquipment(),
  })

  const submitMutation = useMutation({
    mutationFn: async () => {
      if (!category) throw new Error('category')
      const affected =
        deviceChoice === 'equipment'
          ? equipmentQuery.data?.find((x) => x.configurationItemId === equipmentId)
          : null
      const affectedLabel =
        deviceChoice === 'equipment' && affected
          ? formatDeviceLabel(affected)
          : deviceChoice === 'service'
            ? serviceLabel
            : deviceChoice === 'other'
              ? otherDevice
              : deviceChoice === 'not_sure'
                ? t('employee.service.notSure')
                : null

      const ticket = await meApi.reportSecurityConcern({
        categoryKey: category,
        description,
        noticedAtUtc: noticedAt || null,
        affectedDeviceOrService: affectedLabel,
        configurationItemId: deviceChoice === 'equipment' ? equipmentId : null,
        sender: category === 'phishing' || category === 'suspicious_link' ? sender || null : null,
        subject: category === 'phishing' || category === 'suspicious_link' ? subject || null : null,
        suspiciousReason:
          category === 'phishing' || category === 'suspicious_link' ? suspiciousReason || null : null,
      })

      if (file) {
        try {
          await meApi.uploadAttachment(ticket.id, file)
        } catch {
          setAttachWarning(t('employee.request.attachFailed'))
        }
      }
      return ticket
    },
    onSuccess: (ticket) => {
      setError(null)
      setCreated(ticket)
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : t('docs.error.generic'))
    },
  })

  if (created) {
    return (
      <div className="mx-auto max-w-2xl space-y-6">
        <PageHeader
          title={t('employee.security.report.successTitle')}
          description={t('employee.security.report.successHint')}
        />
        <p className="text-sm">
          {t('employee.security.report.reference', { number: created.ticketNumber })}
        </p>
        {attachWarning ? <p className="text-sm text-amber-700 dark:text-amber-400">{attachWarning}</p> : null}
        <Button asChild className="min-h-11">
          <Link to={`/employee/requests/${created.id}`}>{t('employee.security.report.viewRequest')}</Link>
        </Button>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={t('employee.security.report.title')}
        description={t('employee.security.report.description')}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/security">{t('employee.security.back')}</Link>
          </Button>
        }
      />

      {!category ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {SECURITY_CONCERN_CATEGORIES.map((key) => (
            <button
              key={key}
              type="button"
              onClick={() => setCategory(key)}
              className="min-h-[5.5rem] rounded-2xl border bg-card p-4 text-start transition-colors hover:bg-accent/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <span className="font-semibold">{t(`employee.security.report.category.${key}`)}</span>
            </button>
          ))}
        </div>
      ) : (
        <form
          className="space-y-5"
          onSubmit={(e) => {
            e.preventDefault()
            if (!description.trim()) {
              setError(t('employee.security.report.descriptionRequired'))
              return
            }
            submitMutation.mutate()
          }}
        >
          <div className="flex flex-wrap items-center gap-2">
            <span className="rounded-full border px-3 py-1 text-sm">
              {t(`employee.security.report.category.${category}`)}
            </span>
            <Button type="button" variant="ghost" size="sm" onClick={() => setCategory(null)}>
              {t('employee.security.report.changeCategory')}
            </Button>
          </div>

          {(category === 'lost_device' || category === 'account') && (
            <p className="rounded-xl border border-amber-500/40 bg-amber-500/5 px-4 py-3 text-sm">
              {category === 'lost_device'
                ? t('employee.security.report.urgent.lostDevice')
                : t('employee.security.report.urgent.account')}
            </p>
          )}

          <p className="rounded-xl border px-4 py-3 text-sm text-muted-foreground">
            {t('employee.security.report.noSecrets')}
          </p>

          <div className="space-y-2">
            <Label htmlFor="what-happened">{t('employee.security.report.whatHappened')}</Label>
            <Textarea
              id="what-happened"
              required
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="min-h-28"
              aria-required
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="noticed-at">{t('employee.security.report.noticedAt')}</Label>
            <Input
              id="noticed-at"
              type="datetime-local"
              value={noticedAt}
              onChange={(e) => setNoticedAt(e.target.value)}
            />
          </div>

          {(category === 'phishing' || category === 'suspicious_link') && (
            <div className="space-y-4 rounded-2xl border p-4">
              <div className="space-y-2">
                <Label htmlFor="sender">{t('employee.security.report.sender')}</Label>
                <Input id="sender" value={sender} onChange={(e) => setSender(e.target.value)} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="subject">{t('employee.security.report.subject')}</Label>
                <Input id="subject" value={subject} onChange={(e) => setSubject(e.target.value)} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="why">{t('employee.security.report.whySuspicious')}</Label>
                <Textarea id="why" value={suspiciousReason} onChange={(e) => setSuspiciousReason(e.target.value)} />
              </div>
            </div>
          )}

          <fieldset className="space-y-3">
            <legend className="text-sm font-medium">{t('employee.security.report.affected')}</legend>
            <div className="flex flex-wrap gap-2">
              {(
                [
                  ['equipment', t('employee.request.myDevices')],
                  ['service', t('employee.request.commonServices')],
                  ['other', t('employee.category.other')],
                  ['not_sure', t('employee.service.notSure')],
                ] as const
              ).map(([key, label]) => (
                <button
                  key={key}
                  type="button"
                  onClick={() => setDeviceChoice(key)}
                  className={cn(
                    'min-h-11 rounded-full border px-3 text-sm',
                    deviceChoice === key ? 'border-primary bg-primary/10' : 'border-border',
                  )}
                >
                  {label}
                </button>
              ))}
            </div>
            {deviceChoice === 'equipment' ? (
              <ul className="space-y-2">
                {(equipmentQuery.data ?? []).map((item) => (
                  <li key={item.id}>
                    <button
                      type="button"
                      onClick={() => {
                        if (item.configurationItemId) setEquipmentId(item.configurationItemId)
                      }}
                      className={cn(
                        'w-full rounded-xl border px-3 py-2 text-start text-sm',
                        equipmentId === item.configurationItemId
                          ? 'border-primary bg-primary/5'
                          : 'border-border',
                      )}
                      disabled={!item.configurationItemId}
                    >
                      {formatDeviceLabel(item)}
                    </button>
                  </li>
                ))}
                {(equipmentQuery.data?.length ?? 0) === 0 ? (
                  <li className="text-sm text-muted-foreground">{t('employee.request.noDevices')}</li>
                ) : null}
              </ul>
            ) : null}
            {deviceChoice === 'service' ? (
              <div className="flex flex-wrap gap-2">
                {(['wifi', 'm365', 'printer', 'business_app'] as const).map((key) => (
                  <button
                    key={key}
                    type="button"
                    onClick={() => setServiceLabel(t(`employee.service.${key}`))}
                    className={cn(
                      'min-h-11 rounded-full border px-3 text-sm',
                      serviceLabel === t(`employee.service.${key}`)
                        ? 'border-primary bg-primary/10'
                        : 'border-border',
                    )}
                  >
                    {t(`employee.service.${key}`)}
                  </button>
                ))}
              </div>
            ) : null}
            {deviceChoice === 'other' ? (
              <Input
                value={otherDevice}
                onChange={(e) => setOtherDevice(e.target.value)}
                placeholder={t('employee.security.report.otherDevicePlaceholder')}
              />
            ) : null}
          </fieldset>

          <div className="space-y-2">
            <Label htmlFor="screenshot">{t('employee.security.report.attachment')}</Label>
            <Input
              id="screenshot"
              type="file"
              accept="image/*,.pdf"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
          </div>

          {error ? <p className="text-sm text-destructive">{error}</p> : null}

          <Button type="submit" className="min-h-11" disabled={submitMutation.isPending || !description.trim()}>
            {t('employee.security.report.submit')}
          </Button>
        </form>
      )}
    </div>
  )
}
