import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ChevronDown } from 'lucide-react'
import {
  ApiError,
  accessApi,
  type AccessCaseItem,
  type AccessCaseRouteParticipant,
  type AccessEvidenceProjection,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Progress } from '@/components/ui/progress'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { AccessNavTabs } from '@/features/it/access-nav'
import {
  AccessCaseHero,
  AccessCurrentActionCard,
  AccessItemTaskRow,
  AccessWorkflowStepper,
} from '@/features/it/access-ux'
import { useAccessUsers } from '@/features/it/access-users'
import { isAppLanguage } from '@/i18n'
import { cn } from '@/lib/utils'

type StageKey = 'Requester' | 'Approver' | 'Fulfiller' | 'Verifier' | 'Closer'

const actionGroups: Array<{ action: string; titleKey: string }> = [
  { action: 'Grant', titleKey: 'access.groups.toGrant' },
  { action: 'Remove', titleKey: 'access.groups.toRemove' },
  { action: 'Disable', titleKey: 'access.groups.toDisable' },
  { action: 'Reassign', titleKey: 'access.groups.toReassign' },
]

function participantsFor(
  routes: AccessCaseRouteParticipant[] | null | undefined,
  stage: StageKey,
): AccessCaseRouteParticipant[] {
  return (routes ?? []).filter((item) => item.stage === stage)
}

function formatWhen(value: string | null | undefined): string {
  if (!value) return '—'
  return new Date(value).toLocaleString()
}

function itemLabel(item: AccessCaseItem, language: string): string {
  const name = language === 'ar' ? item.nameAr || item.nameEn : item.nameEn || item.nameAr
  return name || item.entitlementKey
}

function namesJoined(
  people: AccessCaseRouteParticipant[],
  nameFor: (id: string | null | undefined) => string,
): string {
  if (people.length === 0) return '—'
  return people.map((p) => nameFor(p.userId)).join(' / ')
}

export function AccessDetailPage() {
  const { id = '' } = useParams()
  const { t, i18n } = useTranslation()
  const language = isAppLanguage(i18n.language) ? i18n.language : 'en'
  const { can, user } = useAuth()
  const { nameFor } = useAccessUsers()
  const qc = useQueryClient()
  const [entitlement, setEntitlement] = useState('')
  const [action, setAction] = useState('Grant')
  const [existingKey, setExistingKey] = useState('')
  const [problemComment, setProblemComment] = useState('')
  const [fallbackReason, setFallbackReason] = useState('')
  const [rejectReason, setRejectReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [evidence, setEvidence] = useState<AccessEvidenceProjection | null>(null)
  const [rejectOpen, setRejectOpen] = useState(false)
  const [problemOpen, setProblemOpen] = useState(false)
  const [fallbackOpen, setFallbackOpen] = useState(false)
  const [advancedOpen, setAdvancedOpen] = useState(false)

  const caseQuery = useQuery({
    queryKey: ['access', 'case', id],
    queryFn: () => accessApi.getCase(id),
    enabled: !!id,
  })
  const itemsQuery = useQuery({
    queryKey: ['access', 'case', id, 'items'],
    queryFn: () => accessApi.listItems(id),
    enabled: !!id,
  })
  const existingQuery = useQuery({
    queryKey: ['access', 'case', id, 'existing'],
    queryFn: () => accessApi.listExistingAccess(id),
    enabled: !!id && caseQuery.data?.type === 'Mover',
  })
  const currentAccessQuery = useQuery({
    queryKey: ['access', 'users', caseQuery.data?.subjectUserId, 'current-access'],
    queryFn: () => accessApi.getCurrentAccess(caseQuery.data!.subjectUserId!, { activeOnly: true }),
    enabled: Boolean(caseQuery.data?.subjectUserId),
  })

  const invalidate = async () => {
    await Promise.all([
      qc.invalidateQueries({ queryKey: ['access', 'case', id] }),
      qc.invalidateQueries({ queryKey: ['access', 'cases'] }),
      qc.invalidateQueries({ queryKey: ['me', 'notifications'] }),
    ])
  }

  const run = useMutation({
    mutationFn: async (fn: () => Promise<unknown>) => fn(),
    onSuccess: async () => {
      setError(null)
      await invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('access.error.generic')),
  })

  const accessCase = caseQuery.data
  const routes = accessCase?.routeParticipants
  const items = itemsQuery.data ?? []

  const groupedItems = useMemo(() => {
    const list = itemsQuery.data ?? []
    return actionGroups
      .map((group) => ({
        ...group,
        items: list.filter((item) => item.action === group.action),
      }))
      .filter((group) => group.items.length > 0)
  }, [itemsQuery.data])

  const pendingItems = items.filter((item) => item.status === 'Pending')
  const completedItems = items.filter((item) => item.status !== 'Pending')
  const progressValue = items.length === 0 ? 0 : (completedItems.length / items.length) * 100

  const outcomeCounts = useMemo(() => {
    const list = itemsQuery.data ?? []
    const grants = list.filter((i) => i.action === 'Grant').length
    const removes = list.filter((i) => i.action === 'Remove').length
    const disables = list.filter((i) => i.action === 'Disable').length
    return { grants, removes, disables, total: list.length }
  }, [itemsQuery.data])

  if (caseQuery.isLoading) {
    return <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
  }
  if (!accessCase) {
    return <p className="text-sm text-destructive">{t('access.notFound')}</p>
  }

  const isSubject = Boolean(user?.id && accessCase.subjectUserId === user.id)
  const approvers = participantsFor(routes, 'Approver')
  const fulfillers = participantsFor(routes, 'Fulfiller')
  const verifiers = participantsFor(routes, 'Verifier')
  const fallbackVerifiers = verifiers.filter((item) => !item.isSubjectEmployeeDerived)
  const subjectVerifiers = verifiers.filter((item) => item.isSubjectEmployeeDerived)
  const closers = participantsFor(routes, 'Closer')
  const requesters = participantsFor(routes, 'Requester')

  const isRouteApprover =
    approvers.length === 0 ||
    Boolean(user?.id && approvers.some((item) => item.userId === user.id))
  const showApproveReject =
    accessCase.status === 'Approval' && can('access.approve') && isRouteApprover
  const canVerifyEmployee =
    accessCase.status === 'Verification' &&
    isSubject &&
    accessCase.type !== 'Leaver' &&
    !accessCase.isReadyToClose
  const canFallbackVerify =
    accessCase.status === 'Verification' && !accessCase.isReadyToClose && !isSubject
  const showClose =
    can('access.fulfill') &&
    accessCase.status === 'Verification' &&
    Boolean(accessCase.isReadyToClose)
  const showFulfill =
    accessCase.status === 'Fulfillment' && can('access.fulfill')
  const categoryLabel =
    accessCase.accessCategoryDisplayName ?? accessCase.accessCategoryNameSnapshot ?? null

  const employeeLabel =
    accessCase.subjectName?.trim() ||
    (accessCase.subjectUserId ? nameFor(accessCase.subjectUserId) : null) ||
    accessCase.subjectEmail ||
    '—'

  const currentOwner = (() => {
    if (accessCase.status === 'Approval') return namesJoined(approvers, nameFor)
    if (accessCase.status === 'Fulfillment') return namesJoined(fulfillers, nameFor)
    if (accessCase.status === 'Verification' && accessCase.isReadyToClose) {
      return namesJoined(closers, nameFor)
    }
    if (accessCase.status === 'Verification') {
      return namesJoined(subjectVerifiers.length ? subjectVerifiers : verifiers, nameFor)
    }
    if (accessCase.status === 'Draft') return nameFor(accessCase.requesterUserId)
    return null
  })()

  const completeItem = (itemId: string) => {
    run.mutate(async () => {
      await accessApi.completeItem(id, itemId)
      await qc.invalidateQueries({ queryKey: ['access', 'case', id, 'items'] })
    })
  }

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <PageHeader
        title={accessCase.caseNumber}
        description={`${t(`access.types.${accessCase.type}`, { defaultValue: accessCase.type })} · ${categoryLabel ?? ''}`}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access">{t('access.back')}</Link>
          </Button>
        }
      />
      <AccessNavTabs />

      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <AccessCaseHero
        accessCase={accessCase}
        employeeLabel={employeeLabel}
        categoryLabel={categoryLabel}
        requestedBy={nameFor(accessCase.requesterUserId)}
        currentOwner={currentOwner}
      />

      <Card>
        <CardContent className="space-y-4 p-5 sm:p-6">
          <AccessWorkflowStepper accessCase={accessCase} />
          <div className="grid gap-2 border-t pt-4 text-xs text-muted-foreground sm:grid-cols-2 lg:grid-cols-3">
            <p>
              <span className="font-medium text-foreground">{t('access.workflowSteps.request')}: </span>
              {requesters.length
                ? namesJoined(requesters, nameFor)
                : nameFor(accessCase.requesterUserId)}
            </p>
            <p>
              <span className="font-medium text-foreground">{t('access.workflowSteps.approval')}: </span>
              {namesJoined(approvers, nameFor)}
              {accessCase.approvedAtUtc
                ? ` · ${formatWhen(accessCase.approvedAtUtc)}`
                : ''}
            </p>
            <p>
              <span className="font-medium text-foreground">{t('access.workflowSteps.fulfillment')}: </span>
              {namesJoined(fulfillers, nameFor)}
            </p>
            <p>
              <span className="font-medium text-foreground">{t('access.workflowSteps.verification')}: </span>
              {namesJoined(subjectVerifiers.length ? subjectVerifiers : verifiers, nameFor)}
              {fallbackVerifiers.length > 0 ? (
                <span className="mt-0.5 block">
                  {t('access.fallbackNote', { names: namesJoined(fallbackVerifiers, nameFor) })}
                </span>
              ) : null}
              {accessCase.verifiedAtUtc
                ? ` · ${formatWhen(accessCase.verifiedAtUtc)}${
                    accessCase.verificationMethod ? ` · ${accessCase.verificationMethod}` : ''
                  }`
                : ''}
            </p>
            <p>
              <span className="font-medium text-foreground">{t('access.workflowSteps.closure')}: </span>
              {namesJoined(closers, nameFor)}
              {accessCase.closedAtUtc ? ` · ${formatWhen(accessCase.closedAtUtc)}` : ''}
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Current action card */}
      {accessCase.status === 'Draft' && can('access.request') ? (
        <AccessCurrentActionCard
          title={t('access.actionCard.draftTitle')}
          description={t('access.draftNotYetSent')}
          actions={
            <Button type="button" onClick={() => run.mutate(() => accessApi.submit(id))}>
              {t('access.actions.submitForApproval')}
            </Button>
          }
        />
      ) : null}

      {showApproveReject ? (
        <AccessCurrentActionCard
          title={t('access.actionCard.approvalTitle')}
          description={t('access.actionCard.approvalDesc')}
          actions={
            <>
              <Button type="button" variant="secondary" onClick={() => setRejectOpen(true)}>
                {t('access.actions.reject')}
              </Button>
              <Button type="button" onClick={() => run.mutate(() => accessApi.approve(id))}>
                {t('access.actions.approve')}
              </Button>
            </>
          }
        >
          <ul className="space-y-1.5">
            {items.slice(0, 8).map((item) => (
              <AccessItemTaskRow
                key={item.id}
                name={itemLabel(item, language)}
                action={item.action}
                privileged={item.isPrivileged}
                isCustom={item.isCustom}
              />
            ))}
            {items.length > 8 ? (
              <li className="text-xs text-muted-foreground">
                {t('access.moreItems', { count: items.length - 8 })}
              </li>
            ) : null}
          </ul>
        </AccessCurrentActionCard>
      ) : null}

      {showFulfill ? (
        <AccessCurrentActionCard
          title={t('access.actionCard.fulfillTitle')}
          description={t('access.actionCard.fulfillProgress', {
            done: completedItems.length,
            total: items.length,
          })}
        >
          <Progress value={progressValue} className="mb-3" />
          <div className="space-y-3">
            {groupedItems.map((group) => (
              <div key={group.action} className="space-y-1.5">
                <h3 className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {t(group.titleKey)}
                </h3>
                <ul className="space-y-1.5">
                  {group.items.map((item) => (
                    <AccessItemTaskRow
                      key={item.id}
                      name={itemLabel(item, language)}
                      status={item.status}
                      privileged={item.isPrivileged}
                      isCustom={item.isCustom}
                      isMandatory={item.isMandatory}
                      quiet
                      onComplete={
                        item.status === 'Pending' ? () => completeItem(item.id) : undefined
                      }
                    />
                  ))}
                </ul>
              </div>
            ))}
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2 border-t pt-3">
            {pendingItems.length > 0 ? (
              <p className="text-sm text-muted-foreground">
                {t('access.tasksRemaining', { count: pendingItems.length })}
              </p>
            ) : (
              <Button type="button" onClick={() => run.mutate(() => accessApi.startVerification(id))}>
                {t('access.actions.sendForVerification')}
              </Button>
            )}
          </div>
        </AccessCurrentActionCard>
      ) : null}

      {canVerifyEmployee ? (
        <AccessCurrentActionCard
          title={t('access.actionCard.verifyTitle')}
          description={t('access.actionCard.verifyDesc')}
          actions={
            <>
              <Button
                type="button"
                onClick={() =>
                  run.mutate(() => accessApi.verify(id, { mode: 'employee', everythingWorks: true }))
                }
              >
                {t('access.verification.everythingWorks')}
              </Button>
              <Button type="button" variant="secondary" onClick={() => setProblemOpen(true)}>
                {t('access.verification.reportProblem')}
              </Button>
            </>
          }
        >
          <ul className="space-y-1.5">
            {items.map((item) => (
              <AccessItemTaskRow
                key={item.id}
                name={itemLabel(item, language)}
                action={item.action}
                quiet
                completed
              />
            ))}
          </ul>
        </AccessCurrentActionCard>
      ) : null}

      {canFallbackVerify && !canVerifyEmployee ? (
        <AccessCurrentActionCard
          title={t('access.actionCard.waitingVerifyTitle')}
          description={t('access.verification.waiting')}
          actions={
            <Button type="button" variant="secondary" onClick={() => setFallbackOpen(true)}>
              {t('access.verification.employeeUnable')}
            </Button>
          }
        />
      ) : null}

      {showClose ? (
        <AccessCurrentActionCard
          title={t('access.actionCard.closeTitle')}
          description={t('access.actionCard.closeDesc')}
          actions={
            <Button type="button" onClick={() => run.mutate(() => accessApi.close(id))}>
              {t('access.actions.closeCase')}
            </Button>
          }
        >
          <ul className="space-y-1 text-sm text-muted-foreground">
            <li>{t('access.closeChecklist.verified')}</li>
            <li>{t('access.closeChecklist.currentAccess')}</li>
            <li>
              {t('access.summaryCounts', {
                grants: outcomeCounts.grants,
                removes: outcomeCounts.removes,
                disables: outcomeCounts.disables,
                total: outcomeCounts.total,
              })}
            </li>
          </ul>
        </AccessCurrentActionCard>
      ) : null}

      {accessCase.status === 'Closed' ? (
        <AccessCurrentActionCard
          title={t('access.actionCard.completedTitle')}
          description={t('access.summaryCounts', {
            grants: outcomeCounts.grants,
            removes: outcomeCounts.removes,
            disables: outcomeCounts.disables,
            total: outcomeCounts.total,
          })}
          actions={
            <Button
              type="button"
              variant="secondary"
              onClick={() =>
                run.mutate(async () => {
                  setEvidence(await accessApi.prepareCaseEvidence(id))
                })
              }
            >
              {t('access.actions.prepareEvidence')}
            </Button>
          }
        />
      ) : null}

      {/* Requested access for non-fulfiller views */}
      {!showFulfill && accessCase.status !== 'Closed' ? (
        <section className="space-y-3">
          <h2 className="text-sm font-semibold tracking-tight">{t('access.requestedAccess')}</h2>
          {groupedItems.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('access.itemsEmpty')}</p>
          ) : (
            groupedItems.map((group) => (
              <div key={group.action} className="space-y-1.5">
                <h3 className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {t(group.titleKey)}
                </h3>
                <ul className="space-y-1.5">
                  {group.items.map((item) => (
                    <AccessItemTaskRow
                      key={item.id}
                      name={itemLabel(item, language)}
                      action={item.action}
                      status={item.status}
                      privileged={item.isPrivileged}
                      isCustom={item.isCustom}
                      isMandatory={item.isMandatory}
                      quiet={item.status !== 'Pending'}
                    />
                  ))}
                </ul>
              </div>
            ))
          )}
        </section>
      ) : null}

      {/* Advanced / technical */}
      <div className="rounded-lg border">
        <button
          type="button"
          className="flex w-full items-center justify-between gap-2 px-4 py-3 text-start text-sm font-medium"
          onClick={() => setAdvancedOpen((v) => !v)}
        >
          {t('access.advanced.title')}
          <ChevronDown
            className={cn('h-4 w-4 text-muted-foreground transition-transform', advancedOpen && 'rotate-180')}
          />
        </button>
        {advancedOpen ? (
          <div className="space-y-6 border-t px-4 py-4">
            {accessCase.subjectUserId ? (
              <section className="space-y-2">
                <h3 className="text-sm font-medium">{t('access.currentAccess')}</h3>
                {currentAccessQuery.isLoading ? (
                  <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
                ) : (currentAccessQuery.data ?? []).length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t('access.currentAccessEmpty')}</p>
                ) : (
                  <ul className="space-y-1 text-sm">
                    {(currentAccessQuery.data ?? []).map((row) => (
                      <li key={row.id} className="flex flex-wrap items-center gap-2">
                        <span>
                          {language === 'ar' ? row.nameAr || row.nameEn : row.nameEn || row.nameAr}
                        </span>
                        {row.isCustom ? (
                          <Badge variant="secondary">{t('access.custom')}</Badge>
                        ) : (
                          <Badge variant="outline">{t('access.catalog')}</Badge>
                        )}
                        {row.isPrivileged ? (
                          <Badge variant="outline">{t('access.privileged')}</Badge>
                        ) : null}
                        <Badge variant="secondary">{row.status}</Badge>
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            ) : null}

            {accessCase.type === 'Mover' ? (
              <section className="space-y-3">
                <h3 className="text-sm font-medium">{t('access.existingTitle')}</h3>
                <ul className="space-y-1 text-sm">
                  {(existingQuery.data ?? []).map((item) => (
                    <li key={item.id}>
                      <Badge variant="outline" className="me-2">
                        {t('access.existing')}
                      </Badge>
                      {item.entitlementKey}
                      {item.accessSummary ? ` — ${item.accessSummary}` : ''}
                    </li>
                  ))}
                </ul>
                {can('access.request') && accessCase.status !== 'Closed' ? (
                  <div className="flex flex-wrap gap-2">
                    <Input
                      className="max-w-xs"
                      value={existingKey}
                      placeholder={t('access.fields.entitlement')}
                      onChange={(e) => setExistingKey(e.target.value)}
                    />
                    <Button
                      type="button"
                      size="sm"
                      disabled={!existingKey.trim()}
                      onClick={() =>
                        run.mutate(async () => {
                          await accessApi.addExistingAccess(id, { entitlementKey: existingKey })
                          setExistingKey('')
                          await qc.invalidateQueries({ queryKey: ['access', 'case', id, 'existing'] })
                        })
                      }
                    >
                      {t('access.actions.addExisting')}
                    </Button>
                    <Button
                      type="button"
                      size="sm"
                      variant="secondary"
                      onClick={() => run.mutate(() => accessApi.confirmExistingAccess(id))}
                    >
                      {t('access.actions.confirmExisting')}
                    </Button>
                  </div>
                ) : null}
              </section>
            ) : null}

            {can('access.request') &&
            !['Closed', 'Rejected', 'Cancelled'].includes(accessCase.status) ? (
              <section className="space-y-3">
                <h3 className="text-sm font-medium">{t('access.advanced.manualItem')}</h3>
                <div className="flex flex-wrap items-end gap-2">
                  <div className="space-y-1">
                    <Label>{t('access.fields.entitlement')}</Label>
                    <Input value={entitlement} onChange={(e) => setEntitlement(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <Label>{t('access.fields.action')}</Label>
                    <Select value={action} onValueChange={setAction}>
                      <SelectTrigger className="w-[140px]">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {['Grant', 'Remove', 'Disable', 'Reassign'].map((item) => (
                          <SelectItem key={item} value={item}>
                            {item}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <Button
                    type="button"
                    disabled={!entitlement.trim()}
                    onClick={() =>
                      run.mutate(async () => {
                        await accessApi.addItem(id, { entitlementKey: entitlement, action })
                        setEntitlement('')
                        await qc.invalidateQueries({ queryKey: ['access', 'case', id, 'items'] })
                      })
                    }
                  >
                    {t('access.actions.addItem')}
                  </Button>
                </div>
              </section>
            ) : null}

            {evidence ? (
              <section className="space-y-2 rounded-lg border p-4 text-sm">
                <h3 className="font-medium">{t('access.evidenceTitle')}</h3>
                <p>
                  {evidence.sourceType} · {evidence.businessNumber} · {evidence.status}
                </p>
                <ul className="list-disc ps-5">
                  {evidence.actorHistorySummary.map((line) => (
                    <li key={line}>{line}</li>
                  ))}
                </ul>
                <ul className="list-disc ps-5">
                  {evidence.fulfillmentOrReviewDecisions.map((line) => (
                    <li key={line}>{line}</li>
                  ))}
                </ul>
              </section>
            ) : null}

            {(accessCase.approvedAtUtc || accessCase.verifiedAtUtc || accessCase.closedAtUtc) && (
              <section className="space-y-2 text-sm">
                <h3 className="font-medium">{t('access.lifecycle.title')}</h3>
                {accessCase.approvedAtUtc ? (
                  <p>
                    {t('access.lifecycle.approvedBy')}: {nameFor(accessCase.approvedByUserId)} ·{' '}
                    {formatWhen(accessCase.approvedAtUtc)}
                  </p>
                ) : null}
                {accessCase.verifiedAtUtc ? (
                  <p>
                    {t('access.lifecycle.verifiedBy')}: {nameFor(accessCase.verifiedByUserId)} ·{' '}
                    {formatWhen(accessCase.verifiedAtUtc)}
                    {accessCase.verificationMethod ? ` · ${accessCase.verificationMethod}` : ''}
                  </p>
                ) : null}
                {accessCase.closedAtUtc ? (
                  <p>
                    {t('access.lifecycle.closedBy')}: {nameFor(accessCase.closedByUserId)} ·{' '}
                    {formatWhen(accessCase.closedAtUtc)}
                  </p>
                ) : null}
              </section>
            )}
          </div>
        ) : null}
      </div>

      {/* Dialogs */}
      <Dialog open={rejectOpen} onOpenChange={setRejectOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('access.actions.reject')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="reject-reason">{t('access.rejectReason')}</Label>
            <Textarea
              id="reject-reason"
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder={t('access.rejectReasonPlaceholder')}
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => setRejectOpen(false)}>
              {t('access.cancel')}
            </Button>
            <Button
              type="button"
              onClick={() => {
                run.mutate(() => accessApi.reject(id, rejectReason.trim() || undefined))
                setRejectOpen(false)
                setRejectReason('')
              }}
            >
              {t('access.actions.reject')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={problemOpen} onOpenChange={setProblemOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('access.verification.reportProblem')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="problem-comment">{t('access.verification.problemComment')}</Label>
            <Textarea
              id="problem-comment"
              value={problemComment}
              onChange={(e) => setProblemComment(e.target.value)}
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => setProblemOpen(false)}>
              {t('access.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!problemComment.trim()}
              onClick={() => {
                run.mutate(() =>
                  accessApi.verify(id, {
                    mode: 'employee',
                    everythingWorks: false,
                    comment: problemComment,
                  }),
                )
                setProblemOpen(false)
              }}
            >
              {t('access.verification.sendBackToIt')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={fallbackOpen} onOpenChange={setFallbackOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('access.verification.verifyOnBehalf')}</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="fallback-reason">{t('access.verification.fallbackReason')}</Label>
            <Textarea
              id="fallback-reason"
              value={fallbackReason}
              onChange={(e) => setFallbackReason(e.target.value)}
              placeholder={t('access.verification.fallbackPlaceholder')}
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => setFallbackOpen(false)}>
              {t('access.cancel')}
            </Button>
            <Button
              type="button"
              disabled={!fallbackReason.trim()}
              onClick={() => {
                run.mutate(() => accessApi.verify(id, { mode: 'fallback', fallbackReason }))
                setFallbackOpen(false)
              }}
            >
              {t('access.verification.verifyOnBehalf')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
