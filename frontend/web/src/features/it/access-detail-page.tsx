import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
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
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { AccessNavTabs } from '@/features/it/access-nav'
import { useAccessUsers } from '@/features/it/access-users'
import { isAppLanguage } from '@/i18n'

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
  const [error, setError] = useState<string | null>(null)
  const [evidence, setEvidence] = useState<AccessEvidenceProjection | null>(null)

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
    await qc.invalidateQueries({ queryKey: ['access', 'case', id] })
    await qc.invalidateQueries({ queryKey: ['access', 'cases'] })
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

  const groupedItems = useMemo(() => {
    const items = itemsQuery.data ?? []
    return actionGroups
      .map((group) => ({
        ...group,
        items: items.filter((item) => item.action === group.action),
      }))
      .filter((group) => group.items.length > 0)
  }, [itemsQuery.data])

  const workflowStages = useMemo(() => {
    if (!accessCase) return []
    const status = accessCase.status
    const requesters = participantsFor(routes, 'Requester')
    const approvers = participantsFor(routes, 'Approver')
    const fulfillers = participantsFor(routes, 'Fulfiller')
    const verifiers = participantsFor(routes, 'Verifier')
    const fallbackVerifiers = verifiers.filter((item) => !item.isSubjectEmployeeDerived)
    const subjectVerifiers = verifiers.filter((item) => item.isSubjectEmployeeDerived)
    const closers = participantsFor(routes, 'Closer')

    const requesterDone = !['Draft'].includes(status)
    const approvalDone = Boolean(accessCase.approvedAtUtc) || ['Fulfillment', 'Verification', 'Closed'].includes(status)
    const fulfillmentDone = ['Verification', 'Closed'].includes(status) || Boolean(accessCase.isReadyToClose)
    const verificationDone = Boolean(accessCase.isReadyToClose) || status === 'Closed'
    const closedDone = status === 'Closed'

    return [
      {
        key: 'requester',
        title: t('access.workflow.requester'),
        names: requesters.length
          ? requesters.map((item) => nameFor(item.userId))
          : [nameFor(accessCase.requesterUserId)],
        state: requesterDone
          ? t('access.workflow.submitted')
          : status === 'Draft'
            ? t('access.workflow.draft')
            : t('access.workflow.waiting'),
        done: requesterDone,
        meta: null as string | null,
      },
      {
        key: 'approver',
        title: t('access.workflow.approver'),
        names: approvers.length ? approvers.map((item) => nameFor(item.userId)) : ['—'],
        state: approvalDone
          ? t('access.workflow.approved')
          : status === 'Approval'
            ? t('access.workflow.inProgress')
            : t('access.workflow.waiting'),
        done: approvalDone,
        meta: accessCase.approvedAtUtc
          ? `${nameFor(accessCase.approvedByUserId)} · ${formatWhen(accessCase.approvedAtUtc)}`
          : null,
      },
      {
        key: 'fulfiller',
        title: t('access.workflow.fulfiller'),
        names: fulfillers.length ? fulfillers.map((item) => nameFor(item.userId)) : ['—'],
        state: fulfillmentDone
          ? t('access.workflow.sentForVerification')
          : status === 'Fulfillment'
            ? t('access.workflow.inProgress')
            : t('access.workflow.waiting'),
        done: fulfillmentDone,
        meta: null,
      },
      {
        key: 'verifier',
        title: t('access.workflow.verifier'),
        names: (subjectVerifiers.length ? subjectVerifiers : verifiers).map((item) => nameFor(item.userId)),
        state: verificationDone
          ? t('access.workflow.verified')
          : status === 'Verification'
            ? t('access.workflow.waiting')
            : t('access.workflow.pending'),
        done: verificationDone,
        meta: accessCase.verifiedAtUtc
          ? `${nameFor(accessCase.verifiedByUserId)} · ${formatWhen(accessCase.verifiedAtUtc)}${
              accessCase.verificationMethod ? ` · ${accessCase.verificationMethod}` : ''
            }`
          : null,
      },
      {
        key: 'fallback',
        title: t('access.workflow.fallbackVerifier'),
        names: fallbackVerifiers.length ? fallbackVerifiers.map((item) => nameFor(item.userId)) : ['—'],
        state: accessCase.verificationMethod === 'Fallback' ? t('access.workflow.verified') : t('access.workflow.optional'),
        done: accessCase.verificationMethod === 'Fallback',
        meta: accessCase.fallbackReason ?? null,
      },
      {
        key: 'closer',
        title: t('access.workflow.closer'),
        names: closers.length ? closers.map((item) => nameFor(item.userId)) : ['—'],
        state: closedDone
          ? t('access.status.closed')
          : accessCase.isReadyToClose
            ? t('access.status.readyToClose')
            : t('access.workflow.pending'),
        done: closedDone,
        meta: closedDone
          ? `${nameFor(accessCase.closedByUserId)} · ${formatWhen(accessCase.closedAtUtc)}`
          : null,
      },
    ]
  }, [accessCase, nameFor, routes, t])

  if (caseQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('access.loading')}</p>
  if (!accessCase) return <p className="text-sm text-destructive">{t('access.notFound')}</p>

  const isSubject = Boolean(user?.id && accessCase.subjectUserId === user.id)
  const canVerifyEmployee =
    accessCase.status === 'Verification' && isSubject && accessCase.type !== 'Leaver' && !accessCase.isReadyToClose
  const canFallbackVerify =
    accessCase.status === 'Verification' &&
    !accessCase.isReadyToClose &&
    can('access.request') &&
    !isSubject
  const showClose = Boolean(accessCase.isReadyToClose) && accessCase.status !== 'Closed' && can('access.fulfill')

  const renderItemRow = (item: AccessCaseItem) => (
    <li key={item.id} className="flex flex-wrap items-center gap-2">
      <span className="font-medium">{itemLabel(item, language)}</span>
      {item.isCustom ? (
        <Badge variant="secondary">{t('access.custom')}</Badge>
      ) : (
        <Badge variant="outline">{t('access.catalog')}</Badge>
      )}
      {item.isPrivileged ? <Badge variant="outline">{t('access.privileged')}</Badge> : null}
      {item.isMandatory ? <Badge variant="warning">{t('access.mandatory')}</Badge> : null}
      <Badge variant="secondary">{item.status}</Badge>
      {can('access.fulfill') && item.status === 'Pending' ? (
        <Button
          type="button"
          size="sm"
          variant="secondary"
          className="ms-auto"
          onClick={() =>
            run.mutate(async () => {
              await accessApi.completeItem(id, item.id)
              await qc.invalidateQueries({ queryKey: ['access', 'case', id, 'items'] })
            })
          }
        >
          {t('access.actions.completeItem')}
        </Button>
      ) : null}
    </li>
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={accessCase.caseNumber}
        description={`${accessCase.type} · ${accessCase.status}`}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/access">{t('access.back')}</Link>
          </Button>
        }
      />
      <AccessNavTabs />

      <div className="flex flex-wrap gap-2">
        <Badge variant="outline">{accessCase.type}</Badge>
        <Badge variant="secondary">{accessCase.status}</Badge>
        {accessCase.accessCategoryNameSnapshot ? (
          <Badge variant="outline">{accessCase.accessCategoryNameSnapshot}</Badge>
        ) : null}
        {accessCase.isReadyToClose ? (
          <Badge variant="success">{t('access.status.readyToClose')}</Badge>
        ) : null}
        {accessCase.status === 'Closed' ? <Badge variant="success">{t('access.status.closed')}</Badge> : null}
        {accessCase.existingAccessConfirmed ? (
          <Badge variant="success">{t('access.existingConfirmed')}</Badge>
        ) : null}
      </div>
      <p className="text-sm">{accessCase.reason}</p>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('access.workflow.title')}</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {workflowStages.map((stage) => (
            <div key={stage.key} className="rounded-md border p-3 text-sm">
              <div className="flex items-center justify-between gap-2">
                <p className="font-medium">{stage.title}</p>
                <span aria-hidden>{stage.done ? '✓' : stage.state === t('access.workflow.inProgress') ? '●' : '○'}</span>
              </div>
              <p className="mt-1 text-muted-foreground">{stage.names.join(' / ') || '—'}</p>
              <p className="mt-1 text-xs">{stage.state}</p>
              {stage.meta ? <p className="mt-1 text-xs text-muted-foreground">{stage.meta}</p> : null}
            </div>
          ))}
        </CardContent>
      </Card>

      <div className="flex flex-wrap gap-2">
        {accessCase.status === 'Draft' && can('access.request') ? (
          <Button type="button" onClick={() => run.mutate(() => accessApi.submit(id))}>
            {t('access.actions.submitForApproval')}
          </Button>
        ) : null}
        {accessCase.status === 'Approval' && can('access.approve') ? (
          <>
            <Button type="button" onClick={() => run.mutate(() => accessApi.approve(id))}>
              {t('access.actions.approve')}
            </Button>
            <Button type="button" variant="secondary" onClick={() => run.mutate(() => accessApi.reject(id))}>
              {t('access.actions.reject')}
            </Button>
          </>
        ) : null}
        {accessCase.status === 'Fulfillment' && can('access.fulfill') ? (
          <Button type="button" onClick={() => run.mutate(() => accessApi.startVerification(id))}>
            {t('access.actions.sendForVerification')}
          </Button>
        ) : null}
        {showClose ? (
          <Button type="button" onClick={() => run.mutate(() => accessApi.close(id))}>
            {t('access.actions.closeCase')}
          </Button>
        ) : null}
        {accessCase.status === 'Closed' ? (
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
        ) : null}
      </div>

      {accessCase.status === 'Verification' && !accessCase.isReadyToClose ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('access.verification.title')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {canVerifyEmployee ? (
              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  onClick={() =>
                    run.mutate(() => accessApi.verify(id, { mode: 'employee', everythingWorks: true }))
                  }
                >
                  {t('access.verification.everythingWorks')}
                </Button>
                <div className="flex min-w-[240px] flex-1 flex-wrap items-end gap-2">
                  <div className="min-w-[180px] flex-1 space-y-1">
                    <Label htmlFor="problem-comment">{t('access.verification.problemComment')}</Label>
                    <Input
                      id="problem-comment"
                      value={problemComment}
                      onChange={(e) => setProblemComment(e.target.value)}
                    />
                  </div>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={!problemComment.trim()}
                    onClick={() =>
                      run.mutate(() =>
                        accessApi.verify(id, {
                          mode: 'employee',
                          everythingWorks: false,
                          comment: problemComment,
                        }),
                      )
                    }
                  >
                    {t('access.verification.haveProblem')}
                  </Button>
                </div>
              </div>
            ) : null}

            {canFallbackVerify ? (
              <div className="flex flex-wrap items-end gap-2 border-t pt-4">
                <div className="min-w-[220px] flex-1 space-y-1">
                  <Label htmlFor="fallback-reason">{t('access.verification.fallbackReason')}</Label>
                  <Input
                    id="fallback-reason"
                    value={fallbackReason}
                    onChange={(e) => setFallbackReason(e.target.value)}
                    placeholder={t('access.verification.fallbackPlaceholder')}
                  />
                </div>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={!fallbackReason.trim()}
                  onClick={() =>
                    run.mutate(() =>
                      accessApi.verify(id, { mode: 'fallback', fallbackReason }),
                    )
                  }
                >
                  {t('access.verification.verifyOnBehalf')}
                </Button>
              </div>
            ) : null}

            {!canVerifyEmployee && !canFallbackVerify ? (
              <p className="text-sm text-muted-foreground">{t('access.verification.waiting')}</p>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      {(accessCase.approvedAtUtc || accessCase.verifiedAtUtc || accessCase.closedAtUtc) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('access.lifecycle.title')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
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
          </CardContent>
        </Card>
      )}

      {accessCase.subjectUserId ? (
        <section className="space-y-3">
          <h2 className="text-base font-medium">{t('access.currentAccess')}</h2>
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
                  {row.isPrivileged ? <Badge variant="outline">{t('access.privileged')}</Badge> : null}
                  <Badge variant="secondary">{row.status}</Badge>
                </li>
              ))}
            </ul>
          )}
        </section>
      ) : null}

      {accessCase.type === 'Mover' ? (
        <section className="space-y-3">
          <h2 className="text-base font-medium">{t('access.existingTitle')}</h2>
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

      <section className="space-y-4">
        <h2 className="text-base font-medium">{t('access.itemsTitle')}</h2>
        {groupedItems.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('access.itemsEmpty')}</p>
        ) : (
          groupedItems.map((group) => (
            <div key={group.action} className="space-y-2">
              <h3 className="text-sm font-medium text-muted-foreground">{t(group.titleKey)}</h3>
              <ul className="space-y-2 text-sm">{group.items.map(renderItemRow)}</ul>
            </div>
          ))
        )}
        {can('access.request') && !['Closed', 'Rejected', 'Cancelled'].includes(accessCase.status) ? (
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
        ) : null}
      </section>

      {evidence ? (
        <section className="space-y-2 rounded-lg border p-4 text-sm">
          <h2 className="font-medium">{t('access.evidenceTitle')}</h2>
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
    </div>
  )
}
