import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMemo, useState } from 'react'
import {
  AlertTriangle,
  Check,
  HelpCircle,
  Laptop,
  Monitor,
  Package,
  Wifi,
} from 'lucide-react'
import { ApiError, meApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { equipmentKeys, ticketKeys } from '@/features/it/query-keys'
import {
  EMPLOYEE_CATEGORIES,
  IMPACT_TO_PRIORITY,
  formatDeviceLabel,
  type EmployeeCategory,
  type TicketImpactChoice,
} from '@/features/employee/employee-request-helpers'
import { cn } from '@/lib/utils'

type RequestKind = 'ServiceRequest' | 'Incident'

type AffectedChoice =
  | { kind: 'device'; assetId: string; configurationItemId: string | null; label: string }
  | { kind: 'service'; key: string; label: string }
  | { kind: 'not_sure' }

const SERVICE_OPTIONS = ['wifi', 'm365', 'printer', 'business_app', 'other'] as const

const TITLE_EXAMPLES: Record<string, string> = {
  'Incident:internet': 'Cannot connect to Wi-Fi',
  'Incident:email': 'Cannot sign in to Outlook',
  'Incident:account': 'Cannot sign in to my account',
  'Incident:computer': 'My laptop will not start',
  'ServiceRequest:software': 'Need Microsoft Visio installed',
  'ServiceRequest:access': 'Need access to a shared folder',
  'ServiceRequest:equipment': 'Need a replacement keyboard',
}

export function NewRequestPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [kind, setKind] = useState<RequestKind | null>(null)
  const [category, setCategory] = useState<EmployeeCategory | null>(null)
  const [affected, setAffected] = useState<AffectedChoice | null>(null)
  const [impact, setImpact] = useState<TicketImpactChoice | null>(null)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  const equipmentQuery = useQuery({
    queryKey: equipmentKeys.mine,
    queryFn: () => meApi.listEquipment(),
  })

  const titlePlaceholder = useMemo(() => {
    if (!kind || !category) return t('employee.request.titlePlaceholder')
    return TITLE_EXAMPLES[`${kind}:${category}`] ?? t('employee.request.titlePlaceholder')
  }, [kind, category, t])

  const createMutation = useMutation({
    mutationFn: async () => {
      if (!kind || !category || !impact) throw new Error('incomplete')
      const configurationItemId =
        affected?.kind === 'device' ? affected.configurationItemId : null
      const ticket = await meApi.createTicket({
        type: kind,
        title: title.trim(),
        description: description.trim(),
        priority: IMPACT_TO_PRIORITY[impact],
        configurationItemId,
        category,
      })
      let attachmentWarning: string | null = null
      if (file) {
        try {
          await meApi.uploadTicketAttachment(ticket.id, file)
        } catch {
          attachmentWarning = t('employee.request.attachFailed')
        }
      }
      return { ticket, attachmentWarning }
    },
    onSuccess: async ({ ticket, attachmentWarning }) => {
      setFormError(null)
      await queryClient.invalidateQueries({ queryKey: ticketKeys.all })
      navigate(`/employee/requests/${ticket.id}`, {
        state: { createdNumber: ticket.ticketNumber, attachWarning: attachmentWarning },
      })
    },
    onError: (error) => {
      setFormError(error instanceof ApiError ? error.message : t('requests.error.generic'))
    },
  })

  const canSubmit =
    Boolean(kind && category && impact && title.trim() && description.trim()) &&
    !createMutation.isPending

  return (
    <div className="mx-auto max-w-3xl space-y-8">
      <PageHeader
        title={t('employee.request.pageTitle')}
        description={t('employee.request.pageHint')}
        actions={
          <Button asChild variant="outline" size="sm">
            <Link to="/employee/requests">{t('requests.back')}</Link>
          </Button>
        }
      />

      <p className="rounded-xl border border-dashed bg-muted/30 px-4 py-3 text-sm text-muted-foreground">
        {t('employee.request.kbHint')}{' '}
        <Link to="/employee/knowledge" className="font-medium text-foreground underline-offset-4 hover:underline">
          {t('employee.actions.findAnswer')}
        </Link>
      </p>

      {/* Type */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold">{t('employee.request.whatHelp')}</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <ChoiceCard
            selected={kind === 'ServiceRequest'}
            onClick={() => setKind('ServiceRequest')}
            icon={Package}
            title={t('employee.types.needSomething')}
            description={t('employee.types.needSomethingHint')}
          />
          <ChoiceCard
            selected={kind === 'Incident'}
            onClick={() => setKind('Incident')}
            icon={AlertTriangle}
            title={t('employee.types.notWorking')}
            description={t('employee.types.notWorkingHint')}
          />
        </div>
      </section>

      {kind ? (
        <section className="space-y-3">
          <h2 className="text-base font-semibold">{t('employee.request.whatArea')}</h2>
          <div className="grid gap-2 sm:grid-cols-2">
            {EMPLOYEE_CATEGORIES.map((key) => (
              <button
                key={key}
                type="button"
                onClick={() => setCategory(key)}
                className={cn(
                  'flex min-h-12 items-center justify-between rounded-xl border px-4 py-3 text-start text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  category === key
                    ? 'border-primary bg-primary/10 font-medium'
                    : 'border-border bg-card hover:bg-muted/40',
                )}
              >
                <span>{t(`employee.category.${key}`)}</span>
                {category === key ? <Check className="h-4 w-4 text-primary" aria-hidden /> : null}
              </button>
            ))}
          </div>
        </section>
      ) : null}

      {kind && category ? (
        <section className="space-y-3">
          <div>
            <h2 className="text-base font-semibold">{t('employee.request.affected')}</h2>
            <p className="text-sm text-muted-foreground">{t('employee.request.affectedHint')}</p>
          </div>

          <div className="space-y-2">
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              {t('employee.request.myDevices')}
            </p>
            {equipmentQuery.isLoading ? (
              <Skeleton className="h-12 w-full" />
            ) : (equipmentQuery.data ?? []).length === 0 ? (
              <p className="text-sm text-muted-foreground">{t('employee.request.noDevices')}</p>
            ) : (
              <div className="grid gap-2">
                {(equipmentQuery.data ?? []).map((asset) => {
                  const label = formatDeviceLabel(asset)
                  const selected =
                    affected?.kind === 'device' && affected.assetId === asset.id
                  return (
                    <button
                      key={asset.id}
                      type="button"
                      onClick={() =>
                        setAffected({
                          kind: 'device',
                          assetId: asset.id,
                          configurationItemId: asset.configurationItemId,
                          label,
                        })
                      }
                      className={cn(
                        'flex min-h-12 items-center gap-3 rounded-xl border px-4 py-3 text-start text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                        selected
                          ? 'border-primary bg-primary/10'
                          : 'border-border bg-card hover:bg-muted/40',
                      )}
                    >
                      <Laptop className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
                      <span className="flex-1">{label}</span>
                      {selected ? <Check className="h-4 w-4 text-primary" aria-hidden /> : null}
                    </button>
                  )
                })}
              </div>
            )}
          </div>

          <div className="space-y-2">
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              {t('employee.request.commonServices')}
            </p>
            <div className="grid gap-2 sm:grid-cols-2">
              {SERVICE_OPTIONS.map((key) => {
                const selected = affected?.kind === 'service' && affected.key === key
                return (
                  <button
                    key={key}
                    type="button"
                    onClick={() =>
                      setAffected({
                        kind: 'service',
                        key,
                        label: t(`employee.service.${key}`),
                      })
                    }
                    className={cn(
                      'flex min-h-12 items-center gap-3 rounded-xl border px-4 py-3 text-start text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                      selected
                        ? 'border-primary bg-primary/10'
                        : 'border-border bg-card hover:bg-muted/40',
                    )}
                  >
                    {key === 'wifi' ? (
                      <Wifi className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
                    ) : key === 'printer' ? (
                      <Monitor className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
                    ) : (
                      <Package className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
                    )}
                    <span className="flex-1">{t(`employee.service.${key}`)}</span>
                    {selected ? <Check className="h-4 w-4 text-primary" aria-hidden /> : null}
                  </button>
                )
              })}
              <button
                type="button"
                onClick={() => setAffected({ kind: 'not_sure' })}
                className={cn(
                  'flex min-h-12 items-center gap-3 rounded-xl border px-4 py-3 text-start text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring sm:col-span-2',
                  affected?.kind === 'not_sure'
                    ? 'border-primary bg-primary/10'
                    : 'border-border bg-card hover:bg-muted/40',
                )}
              >
                <HelpCircle className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
                <span className="flex-1">{t('employee.service.notSure')}</span>
                {affected?.kind === 'not_sure' ? (
                  <Check className="h-4 w-4 text-primary" aria-hidden />
                ) : null}
              </button>
            </div>
          </div>
        </section>
      ) : null}

      {kind && category ? (
        <section className="space-y-3">
          <h2 className="text-base font-semibold">{t('employee.request.impact')}</h2>
          <div className="grid gap-2">
            {(
              [
                ['can_work', 'employee.impact.canWork', 'employee.impact.canWorkHint'],
                ['difficult', 'employee.impact.difficult', 'employee.impact.difficultHint'],
                ['cannot_work', 'employee.impact.cannotWork', 'employee.impact.cannotWorkHint'],
                ['several_people', 'employee.impact.several', 'employee.impact.severalHint'],
              ] as const
            ).map(([value, labelKey, hintKey]) => (
              <button
                key={value}
                type="button"
                onClick={() => setImpact(value)}
                className={cn(
                  'flex min-h-14 flex-col items-start gap-0.5 rounded-xl border px-4 py-3 text-start transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  impact === value
                    ? 'border-primary bg-primary/10'
                    : 'border-border bg-card hover:bg-muted/40',
                )}
              >
                <span className="text-sm font-medium">{t(labelKey)}</span>
                <span className="text-xs text-muted-foreground">{t(hintKey)}</span>
              </button>
            ))}
          </div>
        </section>
      ) : null}

      {kind && category && impact ? (
        <section className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="emp-title">{t('employee.request.title')}</Label>
            <Input
              id="emp-title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder={titlePlaceholder}
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="emp-desc">{t('employee.request.description')}</Label>
            <textarea
              id="emp-desc"
              className="min-h-28 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={5}
              required
            />
            <p className="text-xs text-muted-foreground">{t('employee.request.descriptionHint')}</p>
          </div>
          <div className="space-y-2">
            <Label htmlFor="emp-file">{t('employee.request.attachment')}</Label>
            <Input
              id="emp-file"
              type="file"
              accept="image/*,.pdf,.png,.jpg,.jpeg,.webp"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
            <p className="text-xs text-muted-foreground">{t('employee.request.attachmentHint')}</p>
          </div>
        </section>
      ) : null}

      {kind && category && impact && title.trim() && description.trim() ? (
        <section className="rounded-2xl border bg-muted/20 p-4 text-sm">
          <p className="mb-2 font-medium">{t('employee.request.review')}</p>
          <dl className="space-y-1.5 text-muted-foreground">
            <ReviewRow
              label={t('employee.request.reviewType')}
              value={
                kind === 'Incident'
                  ? t('employee.types.notWorking')
                  : t('employee.types.needSomething')
              }
            />
            <ReviewRow label={t('employee.request.reviewArea')} value={t(`employee.category.${category}`)} />
            <ReviewRow
              label={t('employee.request.reviewAffected')}
              value={
                affected?.kind === 'device'
                  ? affected.label
                  : affected?.kind === 'service'
                    ? affected.label
                    : affected?.kind === 'not_sure'
                      ? t('employee.service.notSure')
                      : t('employee.service.notSure')
              }
            />
            <ReviewRow
              label={t('employee.request.reviewImpact')}
              value={t(
                impact === 'can_work'
                  ? 'employee.impact.canWork'
                  : impact === 'difficult'
                    ? 'employee.impact.difficult'
                    : impact === 'cannot_work'
                      ? 'employee.impact.cannotWork'
                      : 'employee.impact.several',
              )}
            />
          </dl>
        </section>
      ) : null}

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}

      <div className="flex flex-wrap gap-2 pb-8">
        <Button
          type="button"
          size="lg"
          disabled={!canSubmit}
          onClick={() => createMutation.mutate()}
        >
          {createMutation.isPending ? t('controls.loading') : t('employee.request.send')}
        </Button>
        <Button asChild type="button" variant="outline" size="lg">
          <Link to="/employee/requests">{t('admin.cancel')}</Link>
        </Button>
      </div>
    </div>
  )
}

function ChoiceCard({
  selected,
  onClick,
  icon: Icon,
  title,
  description,
}: {
  selected: boolean
  onClick: () => void
  icon: typeof Package
  title: string
  description: string
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'flex min-h-[7.5rem] flex-col gap-2 rounded-2xl border p-5 text-start transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        selected
          ? 'border-primary bg-primary/10 shadow-sm'
          : 'border-border bg-card hover:bg-muted/40',
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <Icon className="h-6 w-6 text-muted-foreground" aria-hidden />
        {selected ? <Check className="h-5 w-5 text-primary" aria-hidden /> : null}
      </div>
      <div className="text-base font-semibold">{title}</div>
      <p className="text-sm leading-relaxed text-muted-foreground">{description}</p>
    </button>
  )
}

function ReviewRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-2">
      <dt>{label}</dt>
      <dd className="font-medium text-foreground">{value}</dd>
    </div>
  )
}
