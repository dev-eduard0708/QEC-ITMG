import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Check, CircleDot, Circle } from 'lucide-react'
import { ApiError, policiesApi, type ManagedDocument } from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { UserMultiPicker, UserPicker } from '@/components/shared/user-picker'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { usePolicyUsers } from '@/features/it/policy-users'
import { cn } from '@/lib/utils'

/** The domain treats an empty GUID as "clear this responsibility"; null means "leave unchanged". */
const CLEAR_ASSIGNMENT = '00000000-0000-0000-0000-000000000000'

const STATUS_ORDER = ['Draft', 'InReview', 'Approved', 'Published'] as const

type StepState = 'done' | 'current' | 'pending'

function formatDate(value: string | null | undefined): string {
  return value ? new Date(value).toLocaleDateString() : '—'
}

function formatDateTime(value: string | null | undefined): string | null {
  return value ? new Date(value).toLocaleString() : null
}

export function PolicyDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const { can, user } = useAuth()
  const qc = useQueryClient()
  const { activeUsers, employeeUsers, nameFor, isDirectoryAvailable } = usePolicyUsers()

  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const canManage = can('policy.manage')
  const canApprove = can('policy.approve')

  const docQuery = useQuery({
    queryKey: ['policies', id],
    queryFn: () => policiesApi.get(id),
    enabled: !!id,
  })
  const versionsQuery = useQuery({
    queryKey: ['policies', id, 'versions'],
    queryFn: () => policiesApi.listVersions(id),
    enabled: !!id,
  })
  const statsQuery = useQuery({
    queryKey: ['policies', id, 'ack-stats'],
    queryFn: () => policiesApi.acknowledgementStats(id),
    enabled: !!id && can('policy.read') && docQuery.data?.status === 'Published',
  })
  const rowsQuery = useQuery({
    queryKey: ['policies', id, 'ack-rows'],
    queryFn: () => policiesApi.acknowledgementRows(id),
    enabled: !!id && can('policy.read') && docQuery.data?.status === 'Published',
  })

  async function invalidateAll() {
    await qc.invalidateQueries({ queryKey: ['policies'] })
  }

  const run = useMutation({
    mutationFn: async (fn: () => Promise<unknown>) => fn(),
    onSuccess: async () => {
      setError(null)
      setNotice(null)
      await invalidateAll()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  const doc = docQuery.data

  if (docQuery.isLoading) return <p className="text-sm text-muted-foreground">{t('docs.loading')}</p>
  if (!doc) return <p className="text-sm text-destructive">{t('docs.notFound')}</p>

  return (
    <div className="space-y-6">
      <PageHeader
        title={doc.documentNumber}
        description={doc.title}
        actions={
          <Button asChild variant="secondary">
            <Link to="/it/policies">{t('docs.back')}</Link>
          </Button>
        }
      />

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      {notice ? <p className="text-sm text-muted-foreground">{notice}</p> : null}

      <OverviewSection doc={doc} nameFor={nameFor} />

      <ContentSection
        doc={doc}
        canEdit={canManage && doc.status === 'Draft'}
        onSaved={async (message) => {
          setNotice(message)
          await invalidateAll()
        }}
        onError={setError}
      />

      <WorkflowSection
        doc={doc}
        canManage={canManage}
        canApprove={canApprove}
        nameFor={nameFor}
        busy={run.isPending}
        onSubmit={() => run.mutate(() => policiesApi.submit(id))}
        onApprove={() => run.mutate(() => policiesApi.approve(id))}
        onReturn={() => run.mutate(() => policiesApi.returnToDraft(id))}
        onPublish={() => run.mutate(() => policiesApi.publish(id))}
        onRevise={() => run.mutate(() => policiesApi.createRevision(id))}
      />

      {canManage ? (
        <ResponsibilitiesSection
          doc={doc}
          users={activeUsers}
          directoryAvailable={isDirectoryAvailable}
          nameFor={nameFor}
          currentUserId={user?.id ?? null}
          onError={setError}
          onSaved={invalidateAll}
        />
      ) : null}

      {canManage && doc.status === 'Published' && doc.requiresAcknowledgement ? (
        <AssignmentSection
          policyId={id}
          employees={employeeUsers}
          directoryAvailable={isDirectoryAvailable}
          onError={setError}
          onSaved={invalidateAll}
        />
      ) : null}

      {doc.status === 'Published' && doc.requiresAcknowledgement ? (
        <AcknowledgementSection
          stats={statsQuery.data}
          rows={rowsQuery.data ?? []}
          exportUrl={canManage ? policiesApi.acknowledgementExportUrl(id) : null}
        />
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('policyMgmt.history.title')}</CardTitle>
          <CardDescription>{t('policyMgmt.history.description')}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-2">
          {(versionsQuery.data ?? []).length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('policyMgmt.history.empty')}</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {(versionsQuery.data ?? []).map((version) => (
                <li key={version.id} className="rounded-md border p-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">v{version.versionNumber}</Badge>
                    {version.changeSummary ? <span>{version.changeSummary}</span> : null}
                  </div>
                  <dl className="mt-2 grid gap-x-6 gap-y-1 text-xs text-muted-foreground sm:grid-cols-2">
                    <HistoryLine
                      label={t('policyMgmt.history.created')}
                      who={nameFor(version.createdByUserId)}
                      when={formatDateTime(version.createdAtUtc)}
                    />
                    <HistoryLine
                      label={t('policyMgmt.history.submitted')}
                      who={version.submittedByUserId ? nameFor(version.submittedByUserId) : null}
                      when={formatDateTime(version.submittedAtUtc)}
                    />
                    <HistoryLine
                      label={t('policyMgmt.history.approved')}
                      who={version.approvedByUserId ? nameFor(version.approvedByUserId) : null}
                      when={formatDateTime(version.approvedAtUtc)}
                    />
                    <HistoryLine
                      label={t('policyMgmt.history.published')}
                      who={version.publishedByUserId ? nameFor(version.publishedByUserId) : null}
                      when={formatDateTime(version.publishedAtUtc)}
                    />
                  </dl>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function HistoryLine({
  label,
  who,
  when,
}: {
  label: string
  who: string | null
  when: string | null
}) {
  if (!when) return null
  return (
    <div className="flex gap-1">
      <dt>{label}:</dt>
      <dd>{who ? `${who} · ${when}` : when}</dd>
    </div>
  )
}

function OverviewSection({
  doc,
  nameFor,
}: {
  doc: ManagedDocument
  nameFor: (userId: string | null | undefined) => string
}) {
  const { t } = useTranslation()
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t('policyMgmt.overview.title')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-2">
          <Badge variant="secondary">
            {t(`policyMgmt.statusValue.${doc.status}`, { defaultValue: doc.status })}
          </Badge>
          <Badge variant="outline">
            {t(`policyMgmt.classification.${doc.classification}`, {
              defaultValue: doc.classification,
            })}
          </Badge>
          <Badge variant="outline">v{doc.currentVersionNumber ?? '—'}</Badge>
          {doc.requiresAcknowledgement ? (
            <Badge variant="outline">{t('docs.requiresAck')}</Badge>
          ) : null}
          {doc.reviewOverdue ? <Badge variant="warning">{t('docs.overdue')}</Badge> : null}
          {doc.reviewDueSoon && !doc.reviewOverdue ? (
            <Badge variant="outline">{t('docs.dueSoon')}</Badge>
          ) : null}
        </div>
        <dl className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <Meta label={t('docs.columns.effective')} value={formatDate(doc.effectiveDate)} />
          <Meta label={t('docs.columns.review')} value={formatDate(doc.reviewDate)} />
          <Meta label={t('policyMgmt.roles.owner')} value={nameFor(doc.ownerUserId)} />
          <Meta label={t('policyMgmt.roles.approver')} value={nameFor(doc.designatedApproverUserId)} />
          <Meta
            label={t('policyMgmt.columns.assigned')}
            value={String(doc.assignedEmployeeCount ?? 0)}
          />
          <Meta
            label={t('policyMgmt.columns.outstanding')}
            value={String(doc.outstandingAcknowledgementCount ?? 0)}
          />
        </dl>
      </CardContent>
    </Card>
  )
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5">{value}</dd>
    </div>
  )
}

function ContentSection({
  doc,
  canEdit,
  onSaved,
  onError,
}: {
  doc: ManagedDocument
  canEdit: boolean
  onSaved: (message: string) => Promise<void> | void
  onError: (message: string | null) => void
}) {
  const { t } = useTranslation()
  const [editing, setEditing] = useState(false)
  const [title, setTitle] = useState(doc.title)
  const [content, setContent] = useState(doc.currentContentText ?? '')
  const [effectiveDate, setEffectiveDate] = useState(toDateInput(doc.effectiveDate))
  const [reviewDate, setReviewDate] = useState(toDateInput(doc.reviewDate))
  const [requiresAck, setRequiresAck] = useState(doc.requiresAcknowledgement)
  const [requireReAck, setRequireReAck] = useState(doc.requireReAcknowledgement ?? true)

  useEffect(() => {
    if (editing) return
    setTitle(doc.title)
    setContent(doc.currentContentText ?? '')
    setEffectiveDate(toDateInput(doc.effectiveDate))
    setReviewDate(toDateInput(doc.reviewDate))
    setRequiresAck(doc.requiresAcknowledgement)
    setRequireReAck(doc.requireReAcknowledgement ?? true)
  }, [doc, editing])

  const saveMutation = useMutation({
    mutationFn: () =>
      policiesApi.update(doc.id, {
        title: title.trim(),
        ownerUserId: doc.ownerUserId,
        reviewerUserId: doc.reviewerUserId,
        designatedApproverUserId: doc.designatedApproverUserId,
        publisherUserId: doc.publisherUserId,
        classification: doc.classification,
        effectiveDate: effectiveDate ? new Date(effectiveDate).toISOString() : null,
        reviewDate: reviewDate ? new Date(reviewDate).toISOString() : null,
        requiresAcknowledgement: requiresAck,
        requireReAcknowledgement: requireReAck,
        contentText: content,
      }),
    onSuccess: async () => {
      onError(null)
      setEditing(false)
      await onSaved(t('policyMgmt.content.saved'))
    },
    onError: (err) => onError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  return (
    <Card>
      <CardHeader className="flex-row items-start justify-between gap-3 space-y-0">
        <div className="space-y-1.5">
          <CardTitle className="text-base">{t('policyMgmt.content.title')}</CardTitle>
          <CardDescription>{t('policyMgmt.content.description')}</CardDescription>
        </div>
        {canEdit && !editing ? (
          <Button type="button" variant="secondary" size="sm" onClick={() => setEditing(true)}>
            {t('policyMgmt.content.edit')}
          </Button>
        ) : null}
      </CardHeader>
      <CardContent className="space-y-3">
        {editing ? (
          <>
            <div className="space-y-1">
              <Label htmlFor="edit-title">{t('docs.columns.title')}</Label>
              <Input
                id="edit-title"
                value={title}
                onChange={(event) => setTitle(event.target.value)}
              />
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1">
                <Label htmlFor="edit-effective">{t('docs.columns.effective')}</Label>
                <Input
                  id="edit-effective"
                  type="date"
                  value={effectiveDate}
                  onChange={(event) => setEffectiveDate(event.target.value)}
                />
              </div>
              <div className="space-y-1">
                <Label htmlFor="edit-review">{t('docs.columns.review')}</Label>
                <Input
                  id="edit-review"
                  type="date"
                  value={reviewDate}
                  onChange={(event) => setReviewDate(event.target.value)}
                />
              </div>
            </div>
            <div className="space-y-1">
              <Label htmlFor="edit-content">{t('policyMgmt.fields.content')}</Label>
              <Textarea
                id="edit-content"
                className="min-h-64 font-mono"
                value={content}
                onChange={(event) => setContent(event.target.value)}
              />
            </div>
            <div className="flex flex-wrap gap-4">
              <label className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={requiresAck}
                  onCheckedChange={(checked) => setRequiresAck(checked === true)}
                />
                {t('policyMgmt.fields.requiresAck')}
              </label>
              <label className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={requireReAck}
                  onCheckedChange={(checked) => setRequireReAck(checked === true)}
                />
                {t('policyMgmt.fields.requireReAck')}
              </label>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                disabled={!title.trim() || saveMutation.isPending}
                onClick={() => saveMutation.mutate()}
              >
                {t('docs.save')}
              </Button>
              <Button type="button" variant="outline" onClick={() => setEditing(false)}>
                {t('docs.cancel')}
              </Button>
            </div>
          </>
        ) : doc.currentContentText ? (
          <pre className="max-h-96 overflow-auto whitespace-pre-wrap rounded-lg border bg-muted/20 p-4 text-sm">
            {doc.currentContentText}
          </pre>
        ) : (
          <p className="text-sm text-muted-foreground">{t('policyMgmt.content.empty')}</p>
        )}
      </CardContent>
    </Card>
  )
}

function toDateInput(value: string | null): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toISOString().slice(0, 10)
}

function WorkflowSection({
  doc,
  canManage,
  canApprove,
  nameFor,
  busy,
  onSubmit,
  onApprove,
  onReturn,
  onPublish,
  onRevise,
}: {
  doc: ManagedDocument
  canManage: boolean
  canApprove: boolean
  nameFor: (userId: string | null | undefined) => string
  busy: boolean
  onSubmit: () => void
  onApprove: () => void
  onReturn: () => void
  onPublish: () => void
  onRevise: () => void
}) {
  const { t } = useTranslation()
  const currentIndex = STATUS_ORDER.indexOf(doc.status as (typeof STATUS_ORDER)[number])
  const assigned = doc.assignedEmployeeCount ?? 0
  const outstanding = doc.outstandingAcknowledgementCount ?? 0

  const steps = useMemo(() => {
    function stateFor(index: number): StepState {
      if (currentIndex < 0) return index === 0 ? 'current' : 'pending'
      if (index < currentIndex) return 'done'
      if (index === currentIndex) return 'current'
      return 'pending'
    }

    const ackState: StepState = !doc.requiresAcknowledgement
      ? 'pending'
      : assigned > 0 && outstanding === 0
        ? 'done'
        : assigned > 0
          ? 'current'
          : 'pending'

    return [
      {
        key: 'draft',
        title: t('policyMgmt.steps.draft'),
        hint: t('policyMgmt.steps.draftHint'),
        state: stateFor(0),
        actor: null as string | null,
      },
      {
        key: 'review',
        title: t('policyMgmt.steps.review'),
        hint: t('policyMgmt.steps.reviewHint'),
        state: stateFor(1),
        actor: doc.currentSubmittedAtUtc
          ? t('policyMgmt.steps.actor', {
              name: nameFor(doc.currentSubmittedByUserId),
              when: formatDateTime(doc.currentSubmittedAtUtc),
            })
          : null,
      },
      {
        key: 'approve',
        title: t('policyMgmt.steps.approve'),
        hint: t('policyMgmt.steps.approveHint'),
        state: stateFor(2),
        actor: doc.currentApprovedAtUtc
          ? t('policyMgmt.steps.actor', {
              name: nameFor(doc.currentApprovedByUserId),
              when: formatDateTime(doc.currentApprovedAtUtc),
            })
          : null,
      },
      {
        key: 'publish',
        title: t('policyMgmt.steps.publish'),
        hint: t('policyMgmt.steps.publishHint'),
        state: stateFor(3),
        actor: doc.currentPublishedAtUtc
          ? t('policyMgmt.steps.actor', {
              name: nameFor(doc.currentPublishedByUserId),
              when: formatDateTime(doc.currentPublishedAtUtc),
            })
          : null,
      },
      {
        key: 'acknowledge',
        title: t('policyMgmt.steps.acknowledge'),
        hint: t('policyMgmt.steps.acknowledgeHint'),
        state: ackState,
        actor:
          assigned > 0
            ? t('policyMgmt.steps.ackProgress', { assigned, outstanding })
            : null,
      },
    ]
  }, [assigned, currentIndex, doc, nameFor, outstanding, t])

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t('policyMgmt.workflow.title')}</CardTitle>
        <CardDescription>{t('policyMgmt.workflow.description')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <ol className="space-y-3">
          {steps.map((step, index) => (
            <li key={step.key} className="flex gap-3">
              <StepIcon state={step.state} />
              <div className="min-w-0 flex-1 space-y-0.5">
                <div className="flex flex-wrap items-center gap-2">
                  <span
                    className={cn(
                      'text-sm',
                      step.state === 'pending' ? 'text-muted-foreground' : 'font-medium',
                    )}
                  >
                    {index + 1}. {step.title}
                  </span>
                  {step.state === 'current' ? (
                    <Badge variant="default">{t('policyMgmt.steps.current')}</Badge>
                  ) : null}
                  {step.state === 'done' ? (
                    <Badge variant="success">{t('policyMgmt.steps.done')}</Badge>
                  ) : null}
                </div>
                <p className="text-xs text-muted-foreground">{step.actor ?? step.hint}</p>
              </div>
            </li>
          ))}
        </ol>

        <div className="flex flex-wrap gap-2 border-t pt-4">
          {doc.status === 'Draft' && canManage ? (
            <Button type="button" disabled={busy} onClick={onSubmit}>
              {t('policyMgmt.actions.submit')}
            </Button>
          ) : null}
          {doc.status === 'InReview' && canApprove ? (
            <Button type="button" disabled={busy} onClick={onApprove}>
              {t('policyMgmt.actions.approve')}
            </Button>
          ) : null}
          {(doc.status === 'InReview' || doc.status === 'Approved') && (canApprove || canManage) ? (
            <Button type="button" variant="secondary" disabled={busy} onClick={onReturn}>
              {t('policyMgmt.actions.return')}
            </Button>
          ) : null}
          {doc.status === 'Approved' && canManage ? (
            <Button type="button" disabled={busy} onClick={onPublish}>
              {t('policyMgmt.actions.publish')}
            </Button>
          ) : null}
          {doc.status === 'Published' && canManage ? (
            <Button type="button" variant="secondary" disabled={busy} onClick={onRevise}>
              {t('policyMgmt.actions.revise')}
            </Button>
          ) : null}
        </div>
      </CardContent>
    </Card>
  )
}

function StepIcon({ state }: { state: StepState }) {
  if (state === 'done') {
    return (
      <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300">
        <Check className="h-3.5 w-3.5" aria-hidden />
      </span>
    )
  }
  if (state === 'current') {
    return (
      <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
        <CircleDot className="h-3.5 w-3.5" aria-hidden />
      </span>
    )
  }
  return (
    <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-muted text-muted-foreground">
      <Circle className="h-3.5 w-3.5" aria-hidden />
    </span>
  )
}

function ResponsibilitiesSection({
  doc,
  users,
  directoryAvailable,
  nameFor,
  currentUserId,
  onError,
  onSaved,
}: {
  doc: ManagedDocument
  users: { id: string; displayName: string; upn: string }[]
  directoryAvailable: boolean
  nameFor: (userId: string | null | undefined) => string
  currentUserId: string | null
  onError: (message: string | null) => void
  onSaved: () => Promise<void>
}) {
  const { t } = useTranslation()
  const [owner, setOwner] = useState<string | null>(doc.ownerUserId)
  const [reviewer, setReviewer] = useState<string | null>(doc.reviewerUserId)
  const [approver, setApprover] = useState<string | null>(doc.designatedApproverUserId)
  const [publisher, setPublisher] = useState<string | null>(doc.publisherUserId)
  const [confirmAll, setConfirmAll] = useState(false)

  useEffect(() => {
    setOwner(doc.ownerUserId)
    setReviewer(doc.reviewerUserId)
    setApprover(doc.designatedApproverUserId)
    setPublisher(doc.publisherUserId)
  }, [doc])

  const saveMutation = useMutation({
    mutationFn: (payload: Parameters<typeof policiesApi.assignResponsibilities>[1]) =>
      policiesApi.assignResponsibilities(doc.id, payload),
    onSuccess: async () => {
      onError(null)
      setConfirmAll(false)
      await onSaved()
    },
    onError: (err) => onError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  function save() {
    saveMutation.mutate({
      ownerUserId: owner,
      reviewerUserId: reviewer ?? CLEAR_ASSIGNMENT,
      designatedApproverUserId: approver ?? CLEAR_ASSIGNMENT,
      publisherUserId: publisher ?? CLEAR_ASSIGNMENT,
    })
  }

  const dirty =
    owner !== doc.ownerUserId ||
    reviewer !== doc.reviewerUserId ||
    approver !== doc.designatedApproverUserId ||
    publisher !== doc.publisherUserId

  const roles = [
    {
      key: 'owner',
      label: t('policyMgmt.roles.owner'),
      hint: t('policyMgmt.roles.ownerHint'),
      value: owner,
      set: setOwner,
      allowClear: false,
    },
    {
      key: 'reviewer',
      label: t('policyMgmt.roles.reviewer'),
      hint: t('policyMgmt.roles.reviewerHint'),
      value: reviewer,
      set: setReviewer,
      allowClear: true,
    },
    {
      key: 'approver',
      label: t('policyMgmt.roles.approver'),
      hint: t('policyMgmt.roles.approverHint'),
      value: approver,
      set: setApprover,
      allowClear: true,
    },
    {
      key: 'publisher',
      label: t('policyMgmt.roles.publisher'),
      hint: t('policyMgmt.roles.publisherHint'),
      value: publisher,
      set: setPublisher,
      allowClear: true,
    },
  ]

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t('policyMgmt.responsibilities.title')}</CardTitle>
        <CardDescription>{t('policyMgmt.responsibilities.description')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="rounded-md border border-dashed p-3 text-xs text-muted-foreground">
          {t('policyMgmt.responsibilities.smallTeamHint')}
        </p>

        {directoryAvailable ? (
          <div className="grid gap-4 sm:grid-cols-2">
            {roles.map((role) => (
              <div key={role.key} className="space-y-1.5">
                <Label htmlFor={`role-${role.key}`}>{role.label}</Label>
                <UserPicker
                  id={`role-${role.key}`}
                  users={users}
                  value={role.value}
                  allowClear={role.allowClear}
                  onChange={role.set}
                />
                <div className="flex items-center justify-between gap-2">
                  <p className="text-xs text-muted-foreground">{role.hint}</p>
                  {currentUserId ? (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => role.set(currentUserId)}
                    >
                      {t('policyMgmt.responsibilities.assignToMe')}
                    </Button>
                  ) : null}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <>
            <dl className="grid gap-3 text-sm sm:grid-cols-2">
              <Meta label={t('policyMgmt.roles.owner')} value={nameFor(doc.ownerUserId)} />
              <Meta label={t('policyMgmt.roles.reviewer')} value={nameFor(doc.reviewerUserId)} />
              <Meta
                label={t('policyMgmt.roles.approver')}
                value={nameFor(doc.designatedApproverUserId)}
              />
              <Meta label={t('policyMgmt.roles.publisher')} value={nameFor(doc.publisherUserId)} />
            </dl>
            <p className="text-xs text-muted-foreground">
              {t('policyMgmt.responsibilities.directoryUnavailable')}
            </p>
          </>
        )}

        <div className="flex flex-wrap gap-2">
          {directoryAvailable ? (
            <Button type="button" disabled={!dirty || saveMutation.isPending} onClick={save}>
              {t('docs.save')}
            </Button>
          ) : null}
          <Button
            type="button"
            variant="secondary"
            disabled={saveMutation.isPending}
            onClick={() => setConfirmAll(true)}
          >
            {t('policyMgmt.responsibilities.assignAllToMe')}
          </Button>
        </div>
      </CardContent>

      <AlertDialog open={confirmAll} onOpenChange={setConfirmAll}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {t('policyMgmt.responsibilities.assignAllToMe')}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {t('policyMgmt.responsibilities.assignAllConfirm')}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('docs.cancel')}</AlertDialogCancel>
            <AlertDialogAction onClick={() => saveMutation.mutate({ assignAllToMe: true })}>
              {t('policyMgmt.confirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  )
}

function AssignmentSection({
  policyId,
  employees,
  directoryAvailable,
  onError,
  onSaved,
}: {
  policyId: string
  employees: { id: string; displayName: string; upn: string }[]
  directoryAvailable: boolean
  onError: (message: string | null) => void
  onSaved: () => Promise<void>
}) {
  const { t } = useTranslation()
  const [dueAt, setDueAt] = useState('')
  const [selected, setSelected] = useState<string[]>([])
  const [confirmAll, setConfirmAll] = useState(false)

  const assignMutation = useMutation({
    mutationFn: (payload: Parameters<typeof policiesApi.assign>[1]) =>
      policiesApi.assign(policyId, payload),
    onSuccess: async () => {
      onError(null)
      setConfirmAll(false)
      setSelected([])
      await onSaved()
    },
    onError: (err) => onError(err instanceof ApiError ? err.message : t('docs.error.generic')),
  })

  const dueAtUtc = dueAt ? new Date(dueAt).toISOString() : null

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{t('policyMgmt.assign.title')}</CardTitle>
        <CardDescription>{t('policyMgmt.assign.description')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="assign-due">{t('policyMgmt.assign.due')}</Label>
            <Input
              id="assign-due"
              type="datetime-local"
              value={dueAt}
              onChange={(event) => setDueAt(event.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label>{t('policyMgmt.assign.specificEmployees')}</Label>
            <UserMultiPicker
              users={employees}
              value={selected}
              onChange={setSelected}
              disabled={!directoryAvailable}
            />
            {!directoryAvailable ? (
              <p className="text-xs text-muted-foreground">
                {t('policyMgmt.responsibilities.directoryUnavailable')}
              </p>
            ) : null}
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            disabled={assignMutation.isPending}
            onClick={() => setConfirmAll(true)}
          >
            {t('policyMgmt.assign.allEmployees')}
          </Button>
          <Button
            type="button"
            variant="secondary"
            disabled={selected.length === 0 || assignMutation.isPending}
            onClick={() =>
              assignMutation.mutate({
                scope: 'SpecificUser',
                userIds: selected,
                dueAtUtc,
                isRequired: true,
              })
            }
          >
            {t('policyMgmt.assign.assignSelected', { total: selected.length })}
          </Button>
          <Button asChild variant="outline">
            <a href={policiesApi.acknowledgementExportUrl(policyId)} target="_blank" rel="noreferrer">
              {t('docs.policyAssign.exportCsv')}
            </a>
          </Button>
        </div>

        <p className="text-xs text-muted-foreground">{t('policyMgmt.assign.hint')}</p>
      </CardContent>

      <AlertDialog open={confirmAll} onOpenChange={setConfirmAll}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('policyMgmt.assign.allEmployees')}</AlertDialogTitle>
            <AlertDialogDescription>{t('policyMgmt.assign.allConfirm')}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('docs.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() =>
                assignMutation.mutate({
                  scope: 'AllEmployees',
                  dueAtUtc,
                  isRequired: true,
                })
              }
            >
              {t('policyMgmt.confirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  )
}

function AcknowledgementSection({
  stats,
  rows,
  exportUrl,
}: {
  stats:
    | {
        assigned: number
        acknowledged: number
        outstanding: number
        overdue: number
      }
    | undefined
  rows: {
    userId: string
    displayName: string | null
    upn: string | null
    status: string
    dueAtUtc: string | null
    acknowledgedAtUtc: string | null
  }[]
  exportUrl: string | null
}) {
  const { t } = useTranslation()
  return (
    <Card>
      <CardHeader className="flex-row items-start justify-between gap-3 space-y-0">
        <div className="space-y-1.5">
          <CardTitle className="text-base">{t('policyMgmt.ack.title')}</CardTitle>
          <CardDescription>{t('policyMgmt.ack.description')}</CardDescription>
        </div>
        {exportUrl ? (
          <Button asChild variant="outline" size="sm">
            <a href={exportUrl} target="_blank" rel="noreferrer">
              {t('docs.policyAssign.exportCsv')}
            </a>
          </Button>
        ) : null}
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-4">
          <Stat label={t('docs.policyAssign.assigned')} value={stats?.assigned ?? 0} />
          <Stat label={t('docs.policyAssign.acknowledged')} value={stats?.acknowledged ?? 0} />
          <Stat label={t('docs.policyAssign.outstanding')} value={stats?.outstanding ?? 0} />
          <Stat label={t('docs.policyAssign.overdue')} value={stats?.overdue ?? 0} />
        </div>

        {rows.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('policyMgmt.ack.empty')}</p>
        ) : (
          <div className="overflow-x-auto rounded-lg border">
            <table className="w-full min-w-[640px] text-sm">
              <thead className="bg-muted/40">
                <tr>
                  <th className="px-3 py-2 text-start font-medium">
                    {t('docs.policyAssign.employee')}
                  </th>
                  <th className="px-3 py-2 text-start font-medium">{t('docs.policyAssign.upn')}</th>
                  <th className="px-3 py-2 text-start font-medium">
                    {t('docs.policyAssign.status')}
                  </th>
                  <th className="px-3 py-2 text-start font-medium">
                    {t('policyMgmt.assign.due')}
                  </th>
                  <th className="px-3 py-2 text-start font-medium">
                    {t('docs.policyAssign.ackedAt')}
                  </th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.userId} className="border-t">
                    <td className="px-3 py-2">{row.displayName ?? '—'}</td>
                    <td className="px-3 py-2">{row.upn ?? '—'}</td>
                    <td className="px-3 py-2">
                      <Badge variant={row.acknowledgedAtUtc ? 'success' : 'secondary'}>
                        {row.status}
                      </Badge>
                    </td>
                    <td className="px-3 py-2">{formatDate(row.dueAtUtc)}</td>
                    <td className="px-3 py-2">{formatDateTime(row.acknowledgedAtUtc) ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 text-2xl font-semibold tabular-nums">{value}</p>
    </div>
  )
}
