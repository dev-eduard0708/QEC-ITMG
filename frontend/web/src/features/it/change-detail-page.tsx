import { useEffect, useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ApiError, changesApi, cmdbApi, evidenceApi } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Timeline } from '@/components/shared/timeline'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { changeKeys, cmdbKeys } from '@/features/it/query-keys'

type TabId = 'overview' | 'cis' | 'assessment' | 'plans' | 'approvals' | 'impl' | 'pir' | 'history'

export function ChangeDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can, user } = useAuth()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<TabId>('overview')
  const [formError, setFormError] = useState<string | null>(null)
  const [comment, setComment] = useState('')
  const [ciSearch, setCiSearch] = useState('')
  const [selectedCiId, setSelectedCiId] = useState('')

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [type, setType] = useState('Normal')
  const [riskRating, setRiskRating] = useState('Medium')
  const [ownerUserId, setOwnerUserId] = useState('')
  const [businessImpact, setBusinessImpact] = useState('')
  const [technicalImpact, setTechnicalImpact] = useState('')
  const [securityImpact, setSecurityImpact] = useState('')
  const [implementationPlan, setImplementationPlan] = useState('')
  const [testPlan, setTestPlan] = useState('')
  const [rollbackPlan, setRollbackPlan] = useState('')
  const [scheduledStartUtc, setScheduledStartUtc] = useState('')
  const [scheduledEndUtc, setScheduledEndUtc] = useState('')
  const [isPreAuthorizedStandard, setIsPreAuthorizedStandard] = useState(false)
  const [validationNotes, setValidationNotes] = useState('')
  const [pirNotes, setPirNotes] = useState('')
  const [approverUserId, setApproverUserId] = useState('')
  const [retrospectiveReason, setRetrospectiveReason] = useState('')
  const [actualImplementationAtUtc, setActualImplementationAtUtc] = useState('')

  const changeQuery = useQuery({
    queryKey: changeKeys.detail(id),
    queryFn: () => changesApi.get(id),
    enabled: Boolean(id),
  })
  const cisQuery = useQuery({
    queryKey: changeKeys.cis(id),
    queryFn: () => changesApi.listCis(id),
    enabled: Boolean(id),
  })
  const approvalsQuery = useQuery({
    queryKey: changeKeys.approvals(id),
    queryFn: () => changesApi.listApprovals(id),
    enabled: Boolean(id),
  })
  const historyQuery = useQuery({
    queryKey: changeKeys.history(id),
    queryFn: () => changesApi.listHistory(id),
    enabled: Boolean(id),
  })
  const ciLookupQuery = useQuery({
    queryKey: cmdbKeys.cis(ciSearch),
    queryFn: () => cmdbApi.listCis(ciSearch || undefined),
    enabled: tab === 'cis' && can('cmdb.read'),
  })

  const change = changeQuery.data

  useEffect(() => {
    if (!change) return
    setTitle(change.title)
    setDescription(change.description)
    setType(change.type)
    setRiskRating(change.riskRating)
    setOwnerUserId(change.ownerUserId ?? '')
    setBusinessImpact(change.businessImpact ?? '')
    setTechnicalImpact(change.technicalImpact ?? '')
    setSecurityImpact(change.securityImpact ?? '')
    setImplementationPlan(change.implementationPlan ?? '')
    setTestPlan(change.testPlan ?? '')
    setRollbackPlan(change.rollbackPlan ?? '')
    setScheduledStartUtc(toLocalInput(change.scheduledStartUtc))
    setScheduledEndUtc(toLocalInput(change.scheduledEndUtc))
    setIsPreAuthorizedStandard(change.isPreAuthorizedStandard)
    setValidationNotes(change.validationNotes ?? '')
    setPirNotes(change.pirNotes ?? '')
    setRetrospectiveReason(change.retrospectiveReason ?? '')
    setActualImplementationAtUtc(toLocalInput(change.actualImplementationAtUtc))
  }, [change])

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: changeKeys.detail(id) }),
      queryClient.invalidateQueries({ queryKey: changeKeys.cis(id) }),
      queryClient.invalidateQueries({ queryKey: changeKeys.approvals(id) }),
      queryClient.invalidateQueries({ queryKey: changeKeys.history(id) }),
      queryClient.invalidateQueries({ queryKey: changeKeys.all }),
    ])
  }

  const onError = (error: unknown) => {
    setFormError(error instanceof ApiError ? error.message : t('changes.error.generic'))
  }

  const saveMutation = useMutation({
    mutationFn: () =>
      changesApi.update(id, {
        title,
        description,
        type,
        riskRating,
        ownerUserId: ownerUserId || null,
        businessImpact: businessImpact || null,
        technicalImpact: technicalImpact || null,
        securityImpact: securityImpact || null,
        implementationPlan: implementationPlan || null,
        testPlan: testPlan || null,
        rollbackPlan: rollbackPlan || null,
        scheduledStartUtc: fromLocalInput(scheduledStartUtc),
        scheduledEndUtc: fromLocalInput(scheduledEndUtc),
        isPreAuthorizedStandard: type === 'Standard' && isPreAuthorizedStandard,
        rowVersion: change?.rowVersion,
      }),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError,
  })

  const transitionMutation = useMutation({
    mutationFn: (payload: {
      targetStatus: string
      result?: string | null
      validationNotes?: string | null
      pirNotes?: string | null
      approverUserId?: string | null
    }) =>
      changesApi.transition(id, {
        ...payload,
        comment: comment || null,
        rowVersion: changeQuery.data?.rowVersion,
      }),
    onSuccess: async () => {
      setFormError(null)
      setComment('')
      await refresh()
    },
    onError,
  })

  const approveMutation = useMutation({
    mutationFn: () => changesApi.approve(id, comment || null),
    onSuccess: async () => {
      setFormError(null)
      setComment('')
      await refresh()
    },
    onError,
  })

  const rejectMutation = useMutation({
    mutationFn: () => changesApi.reject(id, comment || null),
    onSuccess: async () => {
      setFormError(null)
      setComment('')
      await refresh()
    },
    onError,
  })

  const retrospectiveMutation = useMutation({
    mutationFn: () =>
      changesApi.markRetrospective(id, {
        reason: retrospectiveReason,
        actualImplementationAtUtc: fromLocalInput(actualImplementationAtUtc),
        rowVersion: change?.rowVersion,
      }),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError,
  })

  const linkCiMutation = useMutation({
    mutationFn: (configurationItemId: string) => changesApi.linkCi(id, configurationItemId),
    onSuccess: async () => {
      setSelectedCiId('')
      setFormError(null)
      await refresh()
    },
    onError,
  })

  const unlinkCiMutation = useMutation({
    mutationFn: (ciId: string) => changesApi.unlinkCi(id, ciId),
    onSuccess: async () => {
      setFormError(null)
      await refresh()
    },
    onError,
  })

  if (changeQuery.isLoading) return <Skeleton className="h-40 w-full" />
  if (!change) return <p className="text-sm text-muted-foreground">{t('changes.notFound')}</p>

  const tabs: { id: TabId; label: string }[] = [
    { id: 'overview', label: t('changes.tabs.overview') },
    { id: 'cis', label: t('changes.tabs.cis') },
    { id: 'assessment', label: t('changes.tabs.assessment') },
    { id: 'plans', label: t('changes.tabs.plans') },
    { id: 'approvals', label: t('changes.tabs.approvals') },
    { id: 'impl', label: t('changes.tabs.impl') },
    { id: 'pir', label: t('changes.tabs.pir') },
    { id: 'history', label: t('changes.tabs.history') },
  ]

  const canEdit = can('change.assess') && !['Closed', 'Cancelled'].includes(change.status)
  const isRequester = user?.id === change.requesterUserId

  return (
    <div className="space-y-6">
      <PageHeader
        title={change.changeNumber}
        description={change.title}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link to="/it/changes">{t('changes.back')}</Link>
            </Button>
            {can('evidence.upload') ? (
              <Button
                type="button"
                variant="secondary"
                onClick={async () => {
                  const created = await evidenceApi.promote({
                    title: `Change ${change.changeNumber}`,
                    sourceType: 'Change',
                    sourceRecordId: change.id,
                    evidenceType: 'Approval',
                    description: change.title,
                  })
                  window.location.href = `/it/evidence/${created.id}`
                }}
              >
                {t('evidence.promote')}
              </Button>
            ) : null}
          </div>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge>{change.status}</Badge>
        <Badge variant="outline">{change.type}</Badge>
        <Badge variant="secondary">{change.riskRating}</Badge>
        {change.isRetrospective ? <Badge variant="warning">{t('changes.retrospective')}</Badge> : null}
        {change.isPreAuthorizedStandard ? (
          <Badge variant="outline">{t('changes.preAuthorizedBadge')}</Badge>
        ) : null}
        {change.catalogItemId ? <Badge variant="outline">{t('changes.catalogBadge')}</Badge> : null}
      </div>

      {!change.isRetrospective && canEdit ? (
        <div className="flex flex-wrap items-end gap-2 rounded-md border border-border p-3">
          <div className="min-w-[12rem] flex-1 space-y-1">
            <Label>{t('changes.fields.retrospectiveReason')}</Label>
            <Input value={retrospectiveReason} onChange={(e) => setRetrospectiveReason(e.target.value)} />
          </div>
          <div className="space-y-1">
            <Label>{t('changes.fields.actualImplementation')}</Label>
            <Input
              type="datetime-local"
              value={actualImplementationAtUtc}
              onChange={(e) => setActualImplementationAtUtc(e.target.value)}
            />
          </div>
          <Button type="button" variant="outline" onClick={() => retrospectiveMutation.mutate()}>
            {t('changes.actions.markRetrospective')}
          </Button>
        </div>
      ) : null}

      {change.isRetrospective && change.retrospectiveReason ? (
        <p className="text-sm text-muted-foreground">
          {t('changes.fields.retrospectiveReason')}: {change.retrospectiveReason}
        </p>
      ) : null}

      <div className="flex flex-wrap gap-2">
        {canEdit ? (
          <Button type="button" variant="secondary" onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending}>
            {t('changes.actions.save')}
          </Button>
        ) : null}
        {can('change.assess') && change.status === 'Draft' ? (
          <ActionButton label={t('changes.actions.submitAssessment')} onClick={() => transitionMutation.mutate({ targetStatus: 'Assessment' })} />
        ) : null}
        {can('change.assess') && change.status === 'Assessment' ? (
          <div className="flex flex-wrap items-end gap-2">
            <div className="space-y-1">
              <Label>{t('changes.fields.approver')}</Label>
              <Input
                className="w-[16rem]"
                value={approverUserId}
                onChange={(e) => setApproverUserId(e.target.value)}
                placeholder={t('changes.fields.approverPlaceholder')}
              />
            </div>
            <ActionButton
              label={t('changes.actions.submitApproval')}
              onClick={() =>
                transitionMutation.mutate({
                  targetStatus: 'Approval',
                  approverUserId: approverUserId || null,
                })
              }
            />
          </div>
        ) : null}
        {can('change.approve') && change.status === 'Approval' && !isRequester ? (
          <>
            <ActionButton label={t('changes.actions.approve')} onClick={() => approveMutation.mutate()} />
            <ActionButton label={t('changes.actions.reject')} onClick={() => rejectMutation.mutate()} />
          </>
        ) : null}
        {can('change.schedule') && change.status === 'Approval' && !change.isRetrospective ? (
          <ActionButton label={t('changes.actions.schedule')} onClick={() => transitionMutation.mutate({ targetStatus: 'Scheduled' })} />
        ) : null}
        {can('change.implement') && change.status === 'Approval' && change.isRetrospective ? (
          <ActionButton
            label={t('changes.actions.retrospectiveValidate')}
            onClick={() =>
              transitionMutation.mutate({
                targetStatus: 'Validation',
                result: 'Successful',
                validationNotes: validationNotes || null,
              })
            }
          />
        ) : null}
        {can('change.implement') && change.status === 'Scheduled' ? (
          <ActionButton label={t('changes.actions.startImpl')} onClick={() => transitionMutation.mutate({ targetStatus: 'Implementation' })} />
        ) : null}
        {can('change.implement') && change.status === 'Implementation' ? (
          <>
            <ActionButton
              label={t('changes.actions.success')}
              onClick={() =>
                transitionMutation.mutate({
                  targetStatus: 'Validation',
                  result: 'Successful',
                  validationNotes: validationNotes || null,
                })
              }
            />
            <ActionButton
              label={t('changes.actions.failed')}
              onClick={() => transitionMutation.mutate({ targetStatus: 'Failed', result: 'Failed' })}
            />
            <ActionButton
              label={t('changes.actions.rollback')}
              onClick={() => transitionMutation.mutate({ targetStatus: 'RolledBack', result: 'RolledBack' })}
            />
          </>
        ) : null}
        {can('change.implement') && change.status === 'Validation' ? (
          <>
            <ActionButton
              label={t('changes.actions.validatePir')}
              onClick={() =>
                transitionMutation.mutate({
                  targetStatus: 'PostImplementationReview',
                  validationNotes: validationNotes || null,
                })
              }
            />
            <ActionButton
              label={t('changes.actions.close')}
              onClick={() =>
                transitionMutation.mutate({
                  targetStatus: 'Closed',
                  validationNotes: validationNotes || null,
                })
              }
            />
          </>
        ) : null}
        {can('change.pir') && change.status === 'PostImplementationReview' ? (
          <ActionButton
            label={t('changes.actions.completePir')}
            onClick={() =>
              transitionMutation.mutate({
                targetStatus: 'Closed',
                pirNotes: pirNotes || null,
              })
            }
          />
        ) : null}
      </div>

      <div className="space-y-2">
        <Label htmlFor="action-comment">{t('changes.fields.comment')}</Label>
        <Input id="action-comment" value={comment} onChange={(e) => setComment(e.target.value)} />
      </div>

      {formError ? <p className="text-sm text-destructive">{formError}</p> : null}

      <div className="flex flex-wrap gap-2 border-b border-border pb-2">
        {tabs.map((item) => (
          <Button
            key={item.id}
            type="button"
            size="sm"
            variant={tab === item.id ? 'default' : 'ghost'}
            onClick={() => setTab(item.id)}
          >
            {item.label}
          </Button>
        ))}
      </div>

      {tab === 'overview' ? (
        <section className="grid gap-4 sm:grid-cols-2">
          <Field label={t('changes.fields.title')}>
            <Input value={title} onChange={(e) => setTitle(e.target.value)} disabled={!canEdit} />
          </Field>
          <Field label={t('changes.fields.type')}>
            <Select value={type} onValueChange={setType} disabled={!canEdit}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {['Standard', 'Normal', 'Emergency'].map((item) => (
                  <SelectItem key={item} value={item}>
                    {item}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <Field label={t('changes.fields.risk')}>
            <Select value={riskRating} onValueChange={setRiskRating} disabled={!canEdit}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {['Low', 'Medium', 'High', 'Critical'].map((item) => (
                  <SelectItem key={item} value={item}>
                    {item}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <Field label={t('changes.fields.owner')}>
            <Input value={ownerUserId} onChange={(e) => setOwnerUserId(e.target.value)} disabled={!canEdit} />
          </Field>
          <div className="sm:col-span-2">
            <Field label={t('changes.fields.description')}>
              <textarea
                className="min-h-24 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                disabled={!canEdit}
              />
            </Field>
          </div>
          {type === 'Standard' ? (
            <label className="flex items-center gap-2 text-sm sm:col-span-2">
              <Checkbox
                checked={isPreAuthorizedStandard}
                disabled={!canEdit}
                onCheckedChange={(v) => setIsPreAuthorizedStandard(v === true)}
              />
              {t('changes.fields.preAuthorized')}
            </label>
          ) : null}
          <p className="text-sm text-muted-foreground sm:col-span-2">
            {t('changes.fields.result')}: {change.result} · {t('changes.fields.cis')}: {change.affectedCiCount}
          </p>
        </section>
      ) : null}

      {tab === 'cis' ? (
        <section className="space-y-4">
          {canEdit ? (
            <div className="flex flex-wrap gap-2">
              <Input
                className="min-w-[12rem] flex-1"
                placeholder={t('changes.ci.search')}
                value={ciSearch}
                onChange={(e) => setCiSearch(e.target.value)}
              />
              <Select value={selectedCiId} onValueChange={setSelectedCiId}>
                <SelectTrigger className="w-[16rem]">
                  <SelectValue placeholder={t('changes.ci.select')} />
                </SelectTrigger>
                <SelectContent>
                  {(ciLookupQuery.data ?? []).map((ci) => (
                    <SelectItem key={ci.id} value={ci.id}>
                      {ci.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button
                type="button"
                disabled={!selectedCiId || linkCiMutation.isPending}
                onClick={() => linkCiMutation.mutate(selectedCiId)}
              >
                {t('changes.ci.link')}
              </Button>
            </div>
          ) : null}
          <ul className="space-y-2 text-sm">
            {(cisQuery.data ?? []).map((link) => (
              <li key={link.configurationItemId} className="flex items-center justify-between gap-2 rounded-md border border-border px-3 py-2">
                <span className="font-mono text-xs">{link.configurationItemId}</span>
                {canEdit ? (
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    onClick={() => unlinkCiMutation.mutate(link.configurationItemId)}
                  >
                    {t('changes.ci.unlink')}
                  </Button>
                ) : null}
              </li>
            ))}
            {(cisQuery.data ?? []).length === 0 ? (
              <li className="text-muted-foreground">{t('changes.ci.empty')}</li>
            ) : null}
          </ul>
        </section>
      ) : null}

      {tab === 'assessment' ? (
        <section className="grid gap-4">
          <Field label={t('changes.fields.businessImpact')}>
            <textarea className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={businessImpact} onChange={(e) => setBusinessImpact(e.target.value)} disabled={!canEdit} />
          </Field>
          <Field label={t('changes.fields.technicalImpact')}>
            <textarea className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={technicalImpact} onChange={(e) => setTechnicalImpact(e.target.value)} disabled={!canEdit} />
          </Field>
          <Field label={t('changes.fields.securityImpact')}>
            <textarea className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={securityImpact} onChange={(e) => setSecurityImpact(e.target.value)} disabled={!canEdit} />
          </Field>
        </section>
      ) : null}

      {tab === 'plans' ? (
        <section className="grid gap-4">
          <Field label={t('changes.fields.implementationPlan')}>
            <textarea className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={implementationPlan} onChange={(e) => setImplementationPlan(e.target.value)} disabled={!canEdit} />
          </Field>
          <Field label={t('changes.fields.testPlan')}>
            <textarea className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={testPlan} onChange={(e) => setTestPlan(e.target.value)} disabled={!canEdit} />
          </Field>
          <Field label={t('changes.fields.rollbackPlan')}>
            <textarea className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm" value={rollbackPlan} onChange={(e) => setRollbackPlan(e.target.value)} disabled={!canEdit} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('changes.fields.scheduledStart')}>
              <Input type="datetime-local" value={scheduledStartUtc} onChange={(e) => setScheduledStartUtc(e.target.value)} disabled={!canEdit} />
            </Field>
            <Field label={t('changes.fields.scheduledEnd')}>
              <Input type="datetime-local" value={scheduledEndUtc} onChange={(e) => setScheduledEndUtc(e.target.value)} disabled={!canEdit} />
            </Field>
          </div>
        </section>
      ) : null}

      {tab === 'approvals' ? (
        <ul className="space-y-2 text-sm">
          {(approvalsQuery.data ?? []).map((item) => (
            <li key={item.id} className="rounded-md border border-border px-3 py-2">
              <div className="flex flex-wrap gap-2">
                <Badge variant="secondary">{item.decision}</Badge>
                <span className="font-mono text-xs">{item.approverUserId.slice(0, 8)}</span>
                {item.decidedAtUtc ? (
                  <span className="text-muted-foreground">{new Date(item.decidedAtUtc).toLocaleString()}</span>
                ) : null}
              </div>
              {item.comment ? <p className="mt-1 text-muted-foreground">{item.comment}</p> : null}
            </li>
          ))}
          {(approvalsQuery.data ?? []).length === 0 ? (
            <li className="text-muted-foreground">{t('changes.approvals.empty')}</li>
          ) : null}
        </ul>
      ) : null}

      {tab === 'impl' ? (
        <section className="space-y-4">
          <p className="text-sm">
            {t('changes.fields.result')}: <strong>{change.result}</strong>
          </p>
          <p className="text-sm text-muted-foreground">
            {change.implementationStartedAtUtc
              ? `${t('changes.fields.implStarted')}: ${new Date(change.implementationStartedAtUtc).toLocaleString()}`
              : t('changes.impl.notStarted')}
          </p>
          <Field label={t('changes.fields.validationNotes')}>
            <textarea
              className="min-h-20 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              value={validationNotes}
              onChange={(e) => setValidationNotes(e.target.value)}
              disabled={!can('change.implement')}
            />
          </Field>
        </section>
      ) : null}

      {tab === 'pir' ? (
        <Field label={t('changes.fields.pirNotes')}>
          <textarea
            className="min-h-28 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={pirNotes}
            onChange={(e) => setPirNotes(e.target.value)}
            disabled={!can('change.pir') && !can('change.implement')}
          />
        </Field>
      ) : null}

      {tab === 'history' ? (
        <Timeline
          items={(historyQuery.data ?? []).map((item) => ({
            id: item.id,
            timestamp: item.occurredAtUtc,
            title: item.summary,
            description: item.details,
            actor: item.actorUserId?.slice(0, 8) ?? null,
            type: item.event,
          }))}
          emptyMessage={t('changes.history.empty')}
        />
      ) : null}
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
    </div>
  )
}

function ActionButton({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <Button type="button" variant="outline" onClick={onClick}>
      {label}
    </Button>
  )
}

function toLocalInput(value: string | null) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

function fromLocalInput(value: string) {
  if (!value.trim()) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date.toISOString()
}
