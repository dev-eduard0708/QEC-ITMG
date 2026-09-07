import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  ApiError,
  awarenessV1Api,
  organizationHierarchyApi,
  type AwarenessV1AudiencePreview,
  type AwarenessV1CampaignDetail,
  type AwarenessV1SetContentBlockPayload,
  type AwarenessV1SetQuestionPayload,
} from '@/api/client'
import { useAuth } from '@/auth/auth-provider'
import { PageHeader } from '@/components/page-header'
import { UserMultiPicker } from '@/components/shared/user-picker'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { cn } from '@/lib/utils'
import { isAppLanguage } from '@/i18n'

type Section =
  | 'details'
  | 'audience'
  | 'content'
  | 'quiz'
  | 'schedule'
  | 'review'
  | 'report'

type ContentDraft = AwarenessV1SetContentBlockPayload
type QuestionDraft = AwarenessV1SetQuestionPayload

const CONTENT_TYPES = ['Text', 'ExternalLink', 'VideoLink', 'DocumentReference'] as const
const QUESTION_TYPES = ['SingleChoice', 'MultipleChoice', 'TrueFalse'] as const

export function SecurityAwarenessCampaignPage() {
  const { id = '' } = useParams()
  const { t, i18n } = useTranslation()
  const { can } = useAuth()
  const qc = useQueryClient()
  const [section, setSection] = useState<Section>('details')
  const [error, setError] = useState<string | null>(null)
  const [preview, setPreview] = useState<AwarenessV1AudiencePreview | null>(null)
  const [launchOpen, setLaunchOpen] = useState(false)

  const canManage = can('sec.awareness.manage')
  const canReport = can('sec.awareness.report')
  const canRead = can('sec.awareness.read') || canManage || canReport

  const campaignQuery = useQuery({
    queryKey: ['security', 'awareness-v1', 'campaign', id],
    queryFn: () => awarenessV1Api.getCampaign(id),
    enabled: Boolean(id) && canRead,
  })

  const campaign = campaignQuery.data
  const isDraft = campaign?.status === 'Draft'
  const editable = Boolean(isDraft && canManage)

  const [titleEn, setTitleEn] = useState('')
  const [titleAr, setTitleAr] = useState('')
  const [descriptionEn, setDescriptionEn] = useState('')
  const [descriptionAr, setDescriptionAr] = useState('')
  const [requireQuiz, setRequireQuiz] = useState(false)
  const [passingScore, setPassingScore] = useState(80)
  const [allowRetry, setAllowRetry] = useState(true)
  const [maxAttempts, setMaxAttempts] = useState('')
  const [requireCompletion, setRequireCompletion] = useState(true)
  const [startAt, setStartAt] = useState('')
  const [dueAt, setDueAt] = useState('')

  const [allHeadOffice, setAllHeadOffice] = useState(false)
  const [departmentIds, setDepartmentIds] = useState<string[]>([])
  const [positionIds, setPositionIds] = useState<string[]>([])
  const [userIds, setUserIds] = useState<string[]>([])

  const [contentBlocks, setContentBlocks] = useState<ContentDraft[]>([])
  const [questions, setQuestions] = useState<QuestionDraft[]>([])

  useEffect(() => {
    if (!campaign) return
    setTitleEn(campaign.titleEn)
    setTitleAr(campaign.titleAr ?? '')
    setDescriptionEn(campaign.descriptionEn ?? '')
    setDescriptionAr(campaign.descriptionAr ?? '')
    setRequireQuiz(campaign.requireQuiz)
    setPassingScore(campaign.passingScorePercent)
    setAllowRetry(campaign.allowRetry)
    setMaxAttempts(campaign.maxAttempts?.toString() ?? '')
    setRequireCompletion(campaign.requireCompletion)
    setStartAt(toLocalInput(campaign.startAtUtc))
    setDueAt(toLocalInput(campaign.dueAtUtc))

    setAllHeadOffice(campaign.audienceRules.some((r) => r.ruleType === 'AllHeadOffice'))
    setDepartmentIds(
      campaign.audienceRules
        .filter((r) => r.ruleType === 'Department' && r.departmentId)
        .map((r) => r.departmentId!),
    )
    setPositionIds(
      campaign.audienceRules
        .filter((r) => r.ruleType === 'Position' && r.positionId)
        .map((r) => r.positionId!),
    )
    setUserIds(
      campaign.audienceRules
        .filter((r) => r.ruleType === 'SpecificUser' && r.userId)
        .map((r) => r.userId!),
    )

    setContentBlocks(
      campaign.contentBlocks.map((b) => ({
        contentType: b.contentType,
        titleEn: b.titleEn,
        titleAr: b.titleAr,
        bodyEn: b.bodyEn,
        bodyAr: b.bodyAr,
        url: b.url,
        documentId: b.documentId,
        estimatedMinutes: b.estimatedMinutes,
      })),
    )
    setQuestions(
      campaign.questions.map((q) => ({
        type: q.type,
        questionEn: q.questionEn,
        questionAr: q.questionAr,
        explanationEn: q.explanationEn,
        explanationAr: q.explanationAr,
        points: q.points,
        options: q.options.map((o) => ({
          textEn: o.textEn,
          textAr: o.textAr,
          isCorrect: o.isCorrect === true,
        })),
      })),
    )
  }, [campaign])

  const deptsQuery = useQuery({
    queryKey: ['organization', 'departments'],
    queryFn: () => organizationHierarchyApi.listDepartments(),
    enabled: section === 'audience' || section === 'review',
  })
  const positionsQuery = useQuery({
    queryKey: ['organization', 'positions', 'all'],
    queryFn: () => organizationHierarchyApi.listPositions(undefined, true),
    enabled: section === 'audience' || section === 'review',
  })
  const usersQuery = useQuery({
    queryKey: ['organization', 'active-users', 'awareness'],
    queryFn: () => organizationHierarchyApi.searchActiveUsers(undefined, { searchAll: true }),
    enabled: section === 'audience',
  })
  const reportQuery = useQuery({
    queryKey: ['security', 'awareness-v1', 'report', id],
    queryFn: () => awarenessV1Api.report(id),
    enabled: section === 'report' && canReport && Boolean(id),
  })

  const invalidate = async () => {
    await qc.invalidateQueries({ queryKey: ['security', 'awareness-v1'] })
    await campaignQuery.refetch()
  }

  const saveDetails = useMutation({
    mutationFn: () =>
      awarenessV1Api.updateDetails(id, {
        titleEn: titleEn.trim(),
        titleAr: titleAr.trim() || null,
        descriptionEn: descriptionEn.trim() || null,
        descriptionAr: descriptionAr.trim() || null,
        startAtUtc: fromLocalInput(startAt),
        dueAtUtc: fromLocalInput(dueAt),
        requireQuiz,
        passingScorePercent: passingScore,
        allowRetry,
        maxAttempts: maxAttempts.trim() ? Number(maxAttempts) : null,
        requireCompletion,
        rowVersion: campaign?.rowVersion,
      }),
    onSuccess: async () => {
      setError(null)
      await invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('awarenessAdmin.error.generic')),
  })

  const saveAudience = useMutation({
    mutationFn: () =>
      awarenessV1Api.setAudience(id, {
        allHeadOffice,
        departmentIds,
        positionIds,
        userIds,
      }),
    onSuccess: async () => {
      setError(null)
      setPreview(null)
      await invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('awarenessAdmin.error.generic')),
  })

  const saveContent = useMutation({
    mutationFn: () => awarenessV1Api.setContent(id, contentBlocks),
    onSuccess: async () => {
      setError(null)
      await invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('awarenessAdmin.error.generic')),
  })

  const saveQuiz = useMutation({
    mutationFn: () => awarenessV1Api.setQuiz(id, questions),
    onSuccess: async () => {
      setError(null)
      await invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('awarenessAdmin.error.generic')),
  })

  const previewMutation = useMutation({
    mutationFn: async () => {
      if (editable) await saveAudience.mutateAsync()
      return awarenessV1Api.previewAudience(id)
    },
    onSuccess: (data) => {
      setPreview(data)
      setError(null)
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('awarenessAdmin.error.generic')),
  })

  const launchMutation = useMutation({
    mutationFn: () => awarenessV1Api.launch(id, campaign?.rowVersion),
    onSuccess: async () => {
      setLaunchOpen(false)
      setError(null)
      await invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('awarenessAdmin.error.generic')),
  })

  const closeMutation = useMutation({
    mutationFn: () => awarenessV1Api.close(id),
    onSuccess: async () => {
      setError(null)
      await invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('awarenessAdmin.error.generic')),
  })

  const archiveMutation = useMutation({
    mutationFn: () => awarenessV1Api.archive(id),
    onSuccess: async () => {
      setError(null)
      await invalidate()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t('awarenessAdmin.error.generic')),
  })

  const missingArabic = useMemo(() => {
    const titleMissing = !titleAr.trim()
    const contentMissing = contentBlocks.some((b) => !b.titleAr?.trim())
    const quizMissing = questions.some(
      (q) => !q.questionAr?.trim() || q.options.some((o) => !o.textAr?.trim()),
    )
    return titleMissing || contentMissing || quizMissing
  }, [titleAr, contentBlocks, questions])

  const lang = isAppLanguage(i18n.language) ? i18n.language : 'en'
  const displayTitle =
    lang === 'ar' && campaign?.titleAr?.trim() ? campaign.titleAr : (campaign?.titleEn ?? '')

  if (!canRead) {
    return <p className="text-sm text-muted-foreground">{t('awarenessAdmin.noPermission')}</p>
  }

  if (campaignQuery.isLoading) {
    return <p className="text-sm text-muted-foreground">{t('docs.loading')}</p>
  }

  if (!campaign) {
    return <p className="text-sm text-destructive">{t('awarenessAdmin.notFound')}</p>
  }

  const sections: [Section, string][] = [
    ['details', 'awarenessAdmin.sections.details'],
    ['audience', 'awarenessAdmin.sections.audience'],
    ['content', 'awarenessAdmin.sections.content'],
    ['quiz', 'awarenessAdmin.sections.quiz'],
    ['schedule', 'awarenessAdmin.sections.schedule'],
    ['review', 'awarenessAdmin.sections.review'],
  ]
  if (canReport && campaign.status !== 'Draft') {
    sections.push(['report', 'awarenessAdmin.sections.report'])
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={displayTitle}
        description={campaign.number ?? t('awarenessAdmin.draftUntitled')}
        actions={
          <div className="flex flex-wrap gap-2">
            <Badge variant="outline">{campaign.status}</Badge>
            <Button asChild variant="outline" size="sm">
              <Link to="/it/security/awareness">{t('awarenessAdmin.back')}</Link>
            </Button>
            {campaign.status === 'Active' && canManage ? (
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={closeMutation.isPending}
                onClick={() => closeMutation.mutate()}
              >
                {t('awarenessAdmin.close')}
              </Button>
            ) : null}
            {(campaign.status === 'Active' || campaign.status === 'Closed') && canManage ? (
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={archiveMutation.isPending}
                onClick={() => archiveMutation.mutate()}
              >
                {t('awarenessAdmin.archive')}
              </Button>
            ) : null}
          </div>
        }
      />

      <div className="flex flex-wrap gap-2">
        {sections.map(([key, label]) => (
          <Button
            key={key}
            type="button"
            size="sm"
            variant={section === key ? 'default' : 'outline'}
            onClick={() => setSection(key)}
          >
            {t(label)}
          </Button>
        ))}
      </div>

      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      {missingArabic && editable ? (
        <p className="rounded-lg border border-amber-500/40 bg-amber-500/5 px-3 py-2 text-sm">
          {t('awarenessAdmin.arabicOptionalWarning')}
        </p>
      ) : null}

      {section === 'details' || section === 'schedule' ? (
        <section className="space-y-4 rounded-xl border bg-card p-4 sm:p-6">
          <h2 className="text-lg font-semibold">
            {t(section === 'schedule' ? 'awarenessAdmin.sections.schedule' : 'awarenessAdmin.sections.details')}
          </h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('awarenessAdmin.fields.titleEn')} required>
              <Input value={titleEn} disabled={!editable} onChange={(e) => setTitleEn(e.target.value)} />
            </Field>
            <Field label={t('awarenessAdmin.fields.titleAr')}>
              <Input value={titleAr} disabled={!editable} onChange={(e) => setTitleAr(e.target.value)} />
            </Field>
            <Field label={t('awarenessAdmin.fields.descriptionEn')} className="sm:col-span-2">
              <Textarea
                value={descriptionEn}
                disabled={!editable}
                onChange={(e) => setDescriptionEn(e.target.value)}
                rows={3}
              />
            </Field>
            <Field label={t('awarenessAdmin.fields.descriptionAr')} className="sm:col-span-2">
              <Textarea
                value={descriptionAr}
                disabled={!editable}
                onChange={(e) => setDescriptionAr(e.target.value)}
                rows={3}
              />
            </Field>
            <Field label={t('awarenessAdmin.fields.startAt')}>
              <Input
                type="datetime-local"
                value={startAt}
                disabled={!editable}
                onChange={(e) => setStartAt(e.target.value)}
              />
            </Field>
            <Field label={t('awarenessAdmin.fields.dueAt')}>
              <Input
                type="datetime-local"
                value={dueAt}
                disabled={!editable}
                onChange={(e) => setDueAt(e.target.value)}
              />
            </Field>
            <label className="flex items-center gap-2 text-sm sm:col-span-2">
              <Checkbox
                checked={requireQuiz}
                disabled={!editable}
                onCheckedChange={(v) => setRequireQuiz(v === true)}
              />
              {t('awarenessAdmin.fields.requireQuiz')}
            </label>
            <Field label={t('awarenessAdmin.fields.passingScore')}>
              <Input
                type="number"
                min={1}
                max={100}
                value={passingScore}
                disabled={!editable || !requireQuiz}
                onChange={(e) => setPassingScore(Number(e.target.value) || 80)}
              />
            </Field>
            <Field label={t('awarenessAdmin.fields.maxAttempts')}>
              <Input
                type="number"
                min={1}
                value={maxAttempts}
                disabled={!editable || !requireQuiz}
                placeholder={t('awarenessAdmin.fields.unlimited')}
                onChange={(e) => setMaxAttempts(e.target.value)}
              />
            </Field>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={allowRetry}
                disabled={!editable || !requireQuiz}
                onCheckedChange={(v) => setAllowRetry(v === true)}
              />
              {t('awarenessAdmin.fields.allowRetry')}
            </label>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={requireCompletion}
                disabled={!editable}
                onCheckedChange={(v) => setRequireCompletion(v === true)}
              />
              {t('awarenessAdmin.fields.requireCompletion')}
            </label>
          </div>
          {editable ? (
            <Button
              type="button"
              disabled={saveDetails.isPending || !titleEn.trim()}
              onClick={() => saveDetails.mutate()}
            >
              {t('awarenessAdmin.saveDraft')}
            </Button>
          ) : null}
        </section>
      ) : null}

      {section === 'audience' ? (
        <section className="space-y-4 rounded-xl border bg-card p-4 sm:p-6">
          <h2 className="text-lg font-semibold">{t('awarenessAdmin.sections.audience')}</h2>
          <p className="text-sm text-muted-foreground">{t('awarenessAdmin.audience.hint')}</p>

          <label className="flex items-center gap-2 text-sm font-medium">
            <Checkbox
              checked={allHeadOffice}
              disabled={!editable}
              onCheckedChange={(v) => setAllHeadOffice(v === true)}
            />
            {t('awarenessAdmin.audience.allHeadOffice')}
          </label>

          <div className="space-y-2">
            <Label>{t('awarenessAdmin.audience.departments')}</Label>
            <div className="grid max-h-48 gap-2 overflow-y-auto rounded-lg border p-3 sm:grid-cols-2">
              {(deptsQuery.data ?? [])
                .filter((d) => d.isActive)
                .map((d) => {
                  const checked = departmentIds.includes(d.id)
                  return (
                    <label key={d.id} className="flex items-center gap-2 text-sm">
                      <Checkbox
                        checked={checked}
                        disabled={!editable}
                        onCheckedChange={(v) =>
                          setDepartmentIds((prev) =>
                            v === true ? [...prev, d.id] : prev.filter((x) => x !== d.id),
                          )
                        }
                      />
                      {lang === 'ar' && d.nameAr ? d.nameAr : d.nameEn}
                    </label>
                  )
                })}
            </div>
          </div>

          <div className="space-y-2">
            <Label>{t('awarenessAdmin.audience.positions')}</Label>
            <div className="grid max-h-48 gap-2 overflow-y-auto rounded-lg border p-3 sm:grid-cols-2">
              {(positionsQuery.data ?? []).map((p) => {
                const checked = positionIds.includes(p.id)
                return (
                  <label key={p.id} className="flex items-center gap-2 text-sm">
                    <Checkbox
                      checked={checked}
                      disabled={!editable}
                      onCheckedChange={(v) =>
                        setPositionIds((prev) =>
                          v === true ? [...prev, p.id] : prev.filter((x) => x !== p.id),
                        )
                      }
                    />
                    {lang === 'ar' ? p.nameAr : p.nameEn}
                  </label>
                )
              })}
            </div>
          </div>

          <div className="space-y-2">
            <Label>{t('awarenessAdmin.audience.specificUsers')}</Label>
            <UserMultiPicker
              users={usersQuery.data ?? []}
              value={userIds}
              onChange={setUserIds}
              disabled={!editable}
            />
          </div>

          <div className="flex flex-wrap gap-2">
            {editable ? (
              <Button
                type="button"
                variant="outline"
                disabled={saveAudience.isPending}
                onClick={() => saveAudience.mutate()}
              >
                {t('awarenessAdmin.saveDraft')}
              </Button>
            ) : null}
            <Button
              type="button"
              disabled={previewMutation.isPending}
              onClick={() => previewMutation.mutate()}
            >
              {t('awarenessAdmin.previewAudience')}
            </Button>
          </div>

          {preview ? <AudienceSummary preview={preview} /> : null}
        </section>
      ) : null}

      {section === 'content' ? (
        <ContentEditor
          blocks={contentBlocks}
          setBlocks={setContentBlocks}
          editable={editable}
          onSave={() => saveContent.mutate()}
          saving={saveContent.isPending}
        />
      ) : null}

      {section === 'quiz' ? (
        <QuizEditor
          questions={questions}
          setQuestions={setQuestions}
          editable={editable}
          requireQuiz={requireQuiz}
          onSave={() => saveQuiz.mutate()}
          saving={saveQuiz.isPending}
        />
      ) : null}

      {section === 'review' ? (
        <section className="space-y-4 rounded-xl border bg-card p-4 sm:p-6">
          <h2 className="text-lg font-semibold">{t('awarenessAdmin.sections.review')}</h2>
          <ReviewSummary campaign={campaign} contentCount={contentBlocks.length} questionCount={questions.length} />
          {preview ? <AudienceSummary preview={preview} /> : null}
          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              variant="outline"
              disabled={previewMutation.isPending}
              onClick={() => previewMutation.mutate()}
            >
              {t('awarenessAdmin.previewAudience')}
            </Button>
            {editable ? (
              <Button type="button" onClick={() => setLaunchOpen(true)}>
                {t('awarenessAdmin.launch')}
              </Button>
            ) : null}
          </div>
        </section>
      ) : null}

      {section === 'report' && reportQuery.data ? (
        <ReportPanel
          report={reportQuery.data}
          exportUrl={awarenessV1Api.exportUrl(id)}
        />
      ) : null}

      <Dialog open={launchOpen} onOpenChange={setLaunchOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('awarenessAdmin.launch')}</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">{t('awarenessAdmin.launchConfirm')}</p>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setLaunchOpen(false)}>
              {t('awarenessAdmin.cancel')}
            </Button>
            <Button
              type="button"
              disabled={launchMutation.isPending}
              onClick={() => launchMutation.mutate()}
            >
              {t('awarenessAdmin.launch')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function Field({
  label,
  children,
  required,
  className,
}: {
  label: string
  children: ReactNode
  required?: boolean
  className?: string
}) {
  return (
    <div className={cn('space-y-1.5', className)}>
      <Label>
        {label}
        {required ? ' *' : ''}
      </Label>
      {children}
    </div>
  )
}

function AudienceSummary({ preview }: { preview: AwarenessV1AudiencePreview }) {
  const { t } = useTranslation()
  return (
    <div className="space-y-3 rounded-lg border bg-muted/20 p-4">
      <h3 className="font-medium">{t('awarenessAdmin.audience.summary')}</h3>
      <dl className="grid gap-2 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-muted-foreground">{t('awarenessAdmin.audience.departments')}</dt>
          <dd className="tabular-nums font-medium">{preview.departmentRuleCount}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('awarenessAdmin.audience.positions')}</dt>
          <dd className="tabular-nums font-medium">{preview.positionRuleCount}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('awarenessAdmin.audience.specificUsers')}</dt>
          <dd className="tabular-nums font-medium">{preview.specificUserRuleCount}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t('awarenessAdmin.audience.unique')}</dt>
          <dd className="tabular-nums font-medium">{preview.uniqueEmployees}</dd>
        </div>
      </dl>
      {preview.members.length > 0 ? (
        <ul className="max-h-48 space-y-1 overflow-y-auto text-sm">
          {preview.members.slice(0, 50).map((m) => (
            <li key={m.userId} className="flex flex-wrap gap-x-2">
              <span className="font-medium">{m.displayName}</span>
              <span className="text-muted-foreground">{m.upn}</span>
              {m.departmentName ? (
                <span className="text-muted-foreground">· {m.departmentName}</span>
              ) : null}
            </li>
          ))}
          {preview.members.length > 50 ? (
            <li className="text-muted-foreground">
              {t('awarenessAdmin.audience.more', { count: preview.members.length - 50 })}
            </li>
          ) : null}
        </ul>
      ) : null}
    </div>
  )
}

function ContentEditor({
  blocks,
  setBlocks,
  editable,
  onSave,
  saving,
}: {
  blocks: ContentDraft[]
  setBlocks: (blocks: ContentDraft[]) => void
  editable: boolean
  onSave: () => void
  saving: boolean
}) {
  const { t } = useTranslation()

  function update(index: number, patch: Partial<ContentDraft>) {
    setBlocks(blocks.map((b, i) => (i === index ? { ...b, ...patch } : b)))
  }

  return (
    <section className="space-y-4 rounded-xl border bg-card p-4 sm:p-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">{t('awarenessAdmin.sections.content')}</h2>
        {editable ? (
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() =>
              setBlocks([
                ...blocks,
                {
                  contentType: 'Text',
                  titleEn: '',
                  titleAr: null,
                  bodyEn: '',
                  bodyAr: null,
                  url: null,
                  documentId: null,
                  estimatedMinutes: 2,
                },
              ])
            }
          >
            {t('awarenessAdmin.content.add')}
          </Button>
        ) : null}
      </div>

      {blocks.map((block, index) => (
        <div key={index} className="space-y-3 rounded-lg border p-3">
          <div className="flex flex-wrap items-end gap-3">
            <Field label={t('awarenessAdmin.content.type')}>
              <Select
                value={block.contentType}
                disabled={!editable}
                onValueChange={(v) => update(index, { contentType: v })}
              >
                <SelectTrigger className="w-48">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CONTENT_TYPES.map((type) => (
                    <SelectItem key={type} value={type}>
                      {t(`awarenessAdmin.content.types.${type}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <Field label={t('awarenessAdmin.fields.minutes')}>
              <Input
                type="number"
                min={0}
                className="w-24"
                value={block.estimatedMinutes ?? ''}
                disabled={!editable}
                onChange={(e) =>
                  update(index, {
                    estimatedMinutes: e.target.value ? Number(e.target.value) : null,
                  })
                }
              />
            </Field>
            {editable ? (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => setBlocks(blocks.filter((_, i) => i !== index))}
              >
                {t('awarenessAdmin.remove')}
              </Button>
            ) : null}
          </div>
          <Field label={t('awarenessAdmin.fields.titleEn')} required>
            <Input
              value={block.titleEn}
              disabled={!editable}
              onChange={(e) => update(index, { titleEn: e.target.value })}
            />
          </Field>
          <Field label={t('awarenessAdmin.fields.titleAr')}>
            <Input
              value={block.titleAr ?? ''}
              disabled={!editable}
              onChange={(e) => update(index, { titleAr: e.target.value || null })}
            />
          </Field>
          {block.contentType === 'Text' ? (
            <>
              <Field label={t('awarenessAdmin.fields.bodyEn')} required>
                <Textarea
                  rows={4}
                  value={block.bodyEn ?? ''}
                  disabled={!editable}
                  onChange={(e) => update(index, { bodyEn: e.target.value })}
                />
              </Field>
              <Field label={t('awarenessAdmin.fields.bodyAr')}>
                <Textarea
                  rows={4}
                  value={block.bodyAr ?? ''}
                  disabled={!editable}
                  onChange={(e) => update(index, { bodyAr: e.target.value || null })}
                />
              </Field>
            </>
          ) : null}
          {block.contentType === 'ExternalLink' || block.contentType === 'VideoLink' ? (
            <Field label={t('awarenessAdmin.fields.url')} required>
              <Input
                value={block.url ?? ''}
                disabled={!editable}
                onChange={(e) => update(index, { url: e.target.value })}
              />
            </Field>
          ) : null}
          {block.contentType === 'DocumentReference' ? (
            <Field label={t('awarenessAdmin.fields.documentId')} required>
              <Input
                value={block.documentId ?? ''}
                disabled={!editable}
                onChange={(e) => update(index, { documentId: e.target.value || null })}
              />
            </Field>
          ) : null}
        </div>
      ))}

      {blocks.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('awarenessAdmin.content.empty')}</p>
      ) : null}

      {editable ? (
        <Button type="button" disabled={saving} onClick={onSave}>
          {t('awarenessAdmin.saveDraft')}
        </Button>
      ) : null}
    </section>
  )
}

function QuizEditor({
  questions,
  setQuestions,
  editable,
  requireQuiz,
  onSave,
  saving,
}: {
  questions: QuestionDraft[]
  setQuestions: (q: QuestionDraft[]) => void
  editable: boolean
  requireQuiz: boolean
  onSave: () => void
  saving: boolean
}) {
  const { t } = useTranslation()

  function update(index: number, patch: Partial<QuestionDraft>) {
    setQuestions(questions.map((q, i) => (i === index ? { ...q, ...patch } : q)))
  }

  function updateOption(
    qIndex: number,
    oIndex: number,
    patch: Partial<QuestionDraft['options'][number]>,
  ) {
    const q = questions[qIndex]
    const options = q.options.map((o, i) => (i === oIndex ? { ...o, ...patch } : o))
    update(qIndex, { options })
  }

  return (
    <section className="space-y-4 rounded-xl border bg-card p-4 sm:p-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">{t('awarenessAdmin.sections.quiz')}</h2>
        {editable ? (
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() =>
              setQuestions([
                ...questions,
                {
                  type: 'SingleChoice',
                  questionEn: '',
                  questionAr: null,
                  explanationEn: null,
                  explanationAr: null,
                  points: 1,
                  options: [
                    { textEn: '', textAr: null, isCorrect: true },
                    { textEn: '', textAr: null, isCorrect: false },
                  ],
                },
              ])
            }
          >
            {t('awarenessAdmin.quiz.add')}
          </Button>
        ) : null}
      </div>

      {!requireQuiz ? (
        <p className="text-sm text-muted-foreground">{t('awarenessAdmin.quiz.optionalHint')}</p>
      ) : null}

      {questions.map((q, qIndex) => (
        <div key={qIndex} className="space-y-3 rounded-lg border p-3">
          <div className="flex flex-wrap items-end gap-3">
            <Field label={t('awarenessAdmin.quiz.type')}>
              <Select
                value={q.type}
                disabled={!editable}
                onValueChange={(v) => {
                  if (v === 'TrueFalse') {
                    update(qIndex, {
                      type: v,
                      options: [
                        { textEn: 'True', textAr: 'صحيح', isCorrect: true },
                        { textEn: 'False', textAr: 'خطأ', isCorrect: false },
                      ],
                    })
                  } else {
                    update(qIndex, { type: v })
                  }
                }}
              >
                <SelectTrigger className="w-48">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {QUESTION_TYPES.map((type) => (
                    <SelectItem key={type} value={type}>
                      {t(`awarenessAdmin.quiz.types.${type}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            {editable ? (
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => setQuestions(questions.filter((_, i) => i !== qIndex))}
              >
                {t('awarenessAdmin.remove')}
              </Button>
            ) : null}
          </div>
          <Field label={t('awarenessAdmin.quiz.questionEn')} required>
            <Input
              value={q.questionEn}
              disabled={!editable}
              onChange={(e) => update(qIndex, { questionEn: e.target.value })}
            />
          </Field>
          <Field label={t('awarenessAdmin.quiz.questionAr')}>
            <Input
              value={q.questionAr ?? ''}
              disabled={!editable}
              onChange={(e) => update(qIndex, { questionAr: e.target.value || null })}
            />
          </Field>
          <div className="space-y-2">
            <Label>{t('awarenessAdmin.quiz.options')}</Label>
            {q.options.map((opt, oIndex) => (
              <div key={oIndex} className="flex flex-wrap items-center gap-2">
                <Checkbox
                  checked={opt.isCorrect}
                  disabled={!editable}
                  onCheckedChange={(v) => {
                    if (q.type === 'MultipleChoice') {
                      updateOption(qIndex, oIndex, { isCorrect: v === true })
                    } else {
                      update(qIndex, {
                        options: q.options.map((o, i) => ({
                          ...o,
                          isCorrect: i === oIndex,
                        })),
                      })
                    }
                  }}
                />
                <Input
                  className="min-w-[12rem] flex-1"
                  value={opt.textEn}
                  disabled={!editable}
                  placeholder={t('awarenessAdmin.quiz.optionEn')}
                  onChange={(e) => updateOption(qIndex, oIndex, { textEn: e.target.value })}
                />
                <Input
                  className="min-w-[12rem] flex-1"
                  value={opt.textAr ?? ''}
                  disabled={!editable}
                  placeholder={t('awarenessAdmin.quiz.optionAr')}
                  onChange={(e) =>
                    updateOption(qIndex, oIndex, { textAr: e.target.value || null })
                  }
                />
                {editable && q.type !== 'TrueFalse' ? (
                  <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    onClick={() =>
                      update(qIndex, {
                        options: q.options.filter((_, i) => i !== oIndex),
                      })
                    }
                  >
                    {t('awarenessAdmin.remove')}
                  </Button>
                ) : null}
              </div>
            ))}
            {editable && q.type !== 'TrueFalse' ? (
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() =>
                  update(qIndex, {
                    options: [...q.options, { textEn: '', textAr: null, isCorrect: false }],
                  })
                }
              >
                {t('awarenessAdmin.quiz.addOption')}
              </Button>
            ) : null}
          </div>
        </div>
      ))}

      {editable ? (
        <Button type="button" disabled={saving} onClick={onSave}>
          {t('awarenessAdmin.saveDraft')}
        </Button>
      ) : null}
    </section>
  )
}

function ReviewSummary({
  campaign,
  contentCount,
  questionCount,
}: {
  campaign: AwarenessV1CampaignDetail
  contentCount: number
  questionCount: number
}) {
  const { t } = useTranslation()
  return (
    <dl className="grid gap-3 text-sm sm:grid-cols-2">
      <div>
        <dt className="text-muted-foreground">{t('awarenessAdmin.columns.campaign')}</dt>
        <dd className="font-medium">{campaign.titleEn}</dd>
      </div>
      <div>
        <dt className="text-muted-foreground">{t('awarenessAdmin.columns.status')}</dt>
        <dd>{campaign.status}</dd>
      </div>
      <div>
        <dt className="text-muted-foreground">{t('awarenessAdmin.sections.content')}</dt>
        <dd>
          {contentCount} {t('awarenessAdmin.review.blocks')} ·{' '}
          {campaign.contentBlocks.reduce((sum, b) => sum + (b.estimatedMinutes ?? 0), 0)}{' '}
          {t('awarenessAdmin.fields.minutes').toLowerCase()}
        </dd>
      </div>
      <div>
        <dt className="text-muted-foreground">{t('awarenessAdmin.sections.quiz')}</dt>
        <dd>
          {campaign.requireQuiz
            ? `${questionCount} ${t('awarenessAdmin.review.questions')} · ${campaign.passingScorePercent}%`
            : t('awarenessAdmin.quiz.none')}
        </dd>
      </div>
      <div>
        <dt className="text-muted-foreground">{t('awarenessAdmin.fields.startAt')}</dt>
        <dd>
          {campaign.startAtUtc
            ? new Date(campaign.startAtUtc).toLocaleString()
            : '—'}
        </dd>
      </div>
      <div>
        <dt className="text-muted-foreground">{t('awarenessAdmin.fields.dueAt')}</dt>
        <dd>
          {campaign.dueAtUtc ? new Date(campaign.dueAtUtc).toLocaleString() : t('awarenessAdmin.noDue')}
        </dd>
      </div>
    </dl>
  )
}

function ReportPanel({
  report,
  exportUrl,
}: {
  report: Awaited<ReturnType<typeof awarenessV1Api.report>>
  exportUrl: string
}) {
  const { t } = useTranslation()
  return (
    <section className="space-y-4 rounded-xl border bg-card p-4 sm:p-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">{t('awarenessAdmin.sections.report')}</h2>
        <Button asChild size="sm" variant="outline">
          <a href={exportUrl} target="_blank" rel="noreferrer">
            {t('awarenessAdmin.exportCsv')}
          </a>
        </Button>
      </div>
      <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {(
          [
            [t('awarenessAdmin.metrics.assigned'), report.assigned],
            [t('awarenessAdmin.metrics.completed'), report.completed],
            [t('awarenessAdmin.report.inProgress'), report.inProgress],
            [t('awarenessAdmin.metrics.notStarted'), report.notStarted],
            [t('awarenessAdmin.metrics.overdue'), report.overdue],
            [t('awarenessAdmin.report.passed'), report.passed],
          ] as const
        ).map(([label, value]) => (
          <CardMetric key={label} label={label} value={value} />
        ))}
      </div>
      <div className="overflow-x-auto rounded-lg border">
        <table className="w-full min-w-[640px] text-sm">
          <thead className="border-b bg-muted/40">
            <tr>
              <th className="px-3 py-2 text-start">{t('awarenessAdmin.report.employee')}</th>
              <th className="px-3 py-2 text-start">{t('awarenessAdmin.audience.departments')}</th>
              <th className="px-3 py-2 text-start">{t('awarenessAdmin.columns.status')}</th>
              <th className="px-3 py-2 text-start">{t('awarenessAdmin.report.score')}</th>
              <th className="px-3 py-2 text-start">{t('awarenessAdmin.report.attempts')}</th>
            </tr>
          </thead>
          <tbody>
            {report.employees.map((e) => (
              <tr key={e.assignmentId} className="border-b last:border-0">
                <td className="px-3 py-2">
                  <div className="font-medium">{e.displayName}</div>
                  <div className="text-xs text-muted-foreground">{e.upn}</div>
                </td>
                <td className="px-3 py-2 text-muted-foreground">{e.departmentName ?? '—'}</td>
                <td className="px-3 py-2">{e.status}</td>
                <td className="px-3 py-2 tabular-nums">{e.score ?? '—'}</td>
                <td className="px-3 py-2 tabular-nums">{e.attemptCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function CardMetric({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-xl font-semibold tabular-nums">{value}</div>
    </div>
  )
}

function toLocalInput(value: string | null | undefined): string {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function fromLocalInput(value: string): string | null {
  if (!value.trim()) return null
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return null
  return d.toISOString()
}
