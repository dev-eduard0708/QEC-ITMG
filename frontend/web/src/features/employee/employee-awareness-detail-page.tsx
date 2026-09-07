import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useMemo, useState, type Dispatch, type SetStateAction } from 'react'
import { useTranslation } from 'react-i18next'
import {
  ApiError,
  awarenessApi,
  awarenessV1Api,
  type AwarenessV1ContentBlock,
  type AwarenessV1Question,
} from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'
import { isAppLanguage } from '@/i18n'

type Step = 'content' | 'quiz' | 'result'
type SingleAnswers = Record<string, string>
type MultiAnswers = Record<string, string[]>

export function EmployeeAwarenessDetailPage() {
  const { assignmentId = '' } = useParams()
  const { t, i18n } = useTranslation()
  const qc = useQueryClient()
  const lang = isAppLanguage(i18n.language) ? i18n.language : 'en'

  const [step, setStep] = useState<Step>('content')
  const [singleAnswers, setSingleAnswers] = useState<SingleAnswers>({})
  const [multiAnswers, setMultiAnswers] = useState<MultiAnswers>({})
  const [attemptId, setAttemptId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [resultMessage, setResultMessage] = useState<string | null>(null)
  const [passed, setPassed] = useState<boolean | null>(null)
  const [score, setScore] = useState<number | null>(null)
  const [passingScore, setPassingScore] = useState<number | null>(null)

  const v1Query = useQuery({
    queryKey: ['me', 'awareness', 'v1', 'detail', assignmentId],
    queryFn: () => awarenessV1Api.myGet(assignmentId),
    enabled: Boolean(assignmentId),
    retry: false,
  })

  const useLegacy = Boolean(v1Query.isError || (v1Query.isSuccess && !v1Query.data))

  const legacyQuery = useQuery({
    queryKey: ['me', 'security', 'awareness', 'detail', assignmentId],
    queryFn: () => awarenessApi.mineGet(assignmentId),
    enabled: Boolean(assignmentId) && useLegacy,
  })

  const startMutation = useMutation({
    mutationFn: () => awarenessV1Api.start(assignmentId),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['me', 'awareness'] })
      await v1Query.refetch()
    },
  })

  const startQuizMutation = useMutation({
    mutationFn: () => awarenessV1Api.startQuizAttempt(assignmentId),
    onSuccess: (attempt) => {
      setAttemptId(attempt.attemptId)
      setStep('quiz')
      setError(null)
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : t('docs.error.generic'))
    },
  })

  const submitV1Mutation = useMutation({
    mutationFn: () => {
      if (!attemptId) throw new Error('No attempt')
      const detail = v1Query.data!
      const answers = detail.questions.map((q) => ({
        questionId: q.id,
        selectedOptionIds:
          q.type === 'MultipleChoice'
            ? multiAnswers[q.id] ?? []
            : singleAnswers[q.id]
              ? [singleAnswers[q.id]]
              : [],
      }))
      return awarenessV1Api.submitQuizAttempt(assignmentId, attemptId, answers)
    },
    onSuccess: async (result) => {
      setPassed(result.passed)
      setScore(result.scorePercent)
      setPassingScore(result.passingScorePercent)
      setResultMessage(result.message)
      setStep('result')
      await qc.invalidateQueries({ queryKey: ['me', 'awareness'] })
      await v1Query.refetch()
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : t('docs.error.generic'))
    },
  })

  const completeMutation = useMutation({
    mutationFn: () => awarenessV1Api.complete(assignmentId),
    onSuccess: async () => {
      setPassed(true)
      setResultMessage(t('employee.awareness.completedNoQuiz'))
      setStep('result')
      await qc.invalidateQueries({ queryKey: ['me', 'awareness'] })
      await v1Query.refetch()
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : t('docs.error.generic'))
    },
  })

  const legacySubmitMutation = useMutation({
    mutationFn: () =>
      awarenessApi.submitQuiz(
        assignmentId,
        Object.entries(singleAnswers).map(([questionId, optionId]) => ({ questionId, optionId })),
      ),
    onSuccess: async (result) => {
      setPassed(result.passed)
      setScore(result.score)
      setResultMessage(result.message)
      setStep('result')
      await qc.invalidateQueries({ queryKey: ['me', 'security', 'awareness'] })
      await legacyQuery.refetch()
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : t('docs.error.generic'))
    },
  })

  const legacyModule = legacyQuery.data?.module
  const legacyAssignment = legacyQuery.data?.assignment
  const legacyQuestions = useMemo(
    () => [...(legacyModule?.questions ?? [])].sort((a, b) => a.displayOrder - b.displayOrder),
    [legacyModule?.questions],
  )

  if (v1Query.isLoading || (useLegacy && legacyQuery.isLoading)) {
    return <p className="text-sm text-muted-foreground">{t('docs.loading')}</p>
  }

  if (v1Query.data) {
    return (
      <V1Player
        detail={v1Query.data}
        lang={lang}
        step={step}
        setStep={setStep}
        singleAnswers={singleAnswers}
        setSingleAnswers={setSingleAnswers}
        multiAnswers={multiAnswers}
        setMultiAnswers={setMultiAnswers}
        error={error}
        resultMessage={resultMessage}
        passed={passed}
        score={score}
        passingScore={passingScore}
        onStart={() => startMutation.mutate()}
        starting={startMutation.isPending}
        onStartQuiz={() => startQuizMutation.mutate()}
        startingQuiz={startQuizMutation.isPending}
        onSubmit={() => submitV1Mutation.mutate()}
        submitting={submitV1Mutation.isPending}
        onComplete={() => completeMutation.mutate()}
        completing={completeMutation.isPending}
        onRetry={() => {
          setSingleAnswers({})
          setMultiAnswers({})
          setAttemptId(null)
          setPassed(null)
          setScore(null)
          setStep('content')
        }}
      />
    )
  }

  if (!legacyModule) {
    return <p className="text-sm text-destructive">{t('employee.security.awareness.notFound')}</p>
  }

  const allAnswered =
    legacyQuestions.length > 0 && legacyQuestions.every((q) => Boolean(singleAnswers[q.id]))
  const alreadyCompleted = legacyAssignment?.status === 'Completed'

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={legacyModule.title}
        description={
          legacyModule.summary ??
          t('employee.security.awareness.minutes', { count: legacyModule.estimatedMinutes })
        }
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/awareness">{t('employee.security.awareness.back')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge variant="outline">v{legacyModule.version}</Badge>
        <Badge variant="secondary">
          {t('employee.security.awareness.minutes', { count: legacyModule.estimatedMinutes })}
        </Badge>
        {alreadyCompleted ? (
          <Badge variant="secondary">{t('employee.security.awareness.badge.completed')}</Badge>
        ) : null}
      </div>

      {step === 'content' || alreadyCompleted ? (
        <section className="space-y-4 rounded-2xl border bg-card p-4 sm:p-6">
          <h2 className="text-lg font-semibold">{t('employee.security.awareness.readContent')}</h2>
          <div className="whitespace-pre-wrap text-sm leading-relaxed">{legacyModule.body}</div>
          {!alreadyCompleted ? (
            <Button type="button" className="min-h-11" onClick={() => setStep('quiz')}>
              {t('employee.security.awareness.continueToQuiz')}
            </Button>
          ) : (
            <p className="text-sm text-muted-foreground">
              {legacyAssignment?.score != null
                ? t('employee.security.awareness.score', { score: legacyAssignment.score })
                : t('employee.security.awareness.badge.completed')}
            </p>
          )}
        </section>
      ) : null}

      {step === 'quiz' && !alreadyCompleted ? (
        <section className="space-y-6 rounded-2xl border bg-card p-4 sm:p-6">
          <h2 className="text-lg font-semibold">{t('employee.security.awareness.quickCheck')}</h2>
          <ol className="space-y-6">
            {legacyQuestions.map((question, index) => (
              <li key={question.id} className="space-y-3">
                <p className="font-medium">
                  {index + 1}. {question.questionText}
                </p>
                <fieldset className="space-y-2">
                  {[...question.options]
                    .sort((a, b) => a.displayOrder - b.displayOrder)
                    .map((option) => {
                      const inputId = `${question.id}-${option.id}`
                      const selected = singleAnswers[question.id] === option.id
                      return (
                        <Label
                          key={option.id}
                          htmlFor={inputId}
                          className={cn(
                            'flex min-h-11 cursor-pointer items-center gap-3 rounded-xl border px-3 py-2 text-sm',
                            selected ? 'border-primary bg-primary/5' : 'border-border',
                          )}
                        >
                          <input
                            id={inputId}
                            type="radio"
                            name={question.id}
                            checked={selected}
                            onChange={() =>
                              setSingleAnswers((prev) => ({ ...prev, [question.id]: option.id }))
                            }
                            className="h-4 w-4"
                          />
                          <span>{option.text}</span>
                        </Label>
                      )
                    })}
                </fieldset>
              </li>
            ))}
          </ol>
          {error ? <p className="text-sm text-destructive">{error}</p> : null}
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="outline" onClick={() => setStep('content')}>
              {t('employee.security.awareness.backToContent')}
            </Button>
            <Button
              type="button"
              disabled={!allAnswered || legacySubmitMutation.isPending}
              onClick={() => legacySubmitMutation.mutate()}
            >
              {t('employee.security.awareness.submit')}
            </Button>
          </div>
        </section>
      ) : null}

      {step === 'result' ? (
        <ResultPanel
          passed={passed}
          score={score}
          passingScore={null}
          message={resultMessage}
          onRetry={() => {
            setSingleAnswers({})
            setStep('content')
          }}
        />
      ) : null}
    </div>
  )
}

function V1Player({
  detail,
  lang,
  step,
  setStep,
  singleAnswers,
  setSingleAnswers,
  multiAnswers,
  setMultiAnswers,
  error,
  resultMessage,
  passed,
  score,
  passingScore,
  onStart,
  starting,
  onStartQuiz,
  startingQuiz,
  onSubmit,
  submitting,
  onComplete,
  completing,
  onRetry,
}: {
  detail: NonNullable<Awaited<ReturnType<typeof awarenessV1Api.myGet>>>
  lang: 'en' | 'ar'
  step: Step
  setStep: (s: Step) => void
  singleAnswers: SingleAnswers
  setSingleAnswers: Dispatch<SetStateAction<SingleAnswers>>
  multiAnswers: MultiAnswers
  setMultiAnswers: Dispatch<SetStateAction<MultiAnswers>>
  error: string | null
  resultMessage: string | null
  passed: boolean | null
  score: number | null
  passingScore: number | null
  onStart: () => void
  starting: boolean
  onStartQuiz: () => void
  startingQuiz: boolean
  onSubmit: () => void
  submitting: boolean
  onComplete: () => void
  completing: boolean
  onRetry: () => void
}) {
  const { t } = useTranslation()
  const assignment = detail.assignment
  const title =
    lang === 'ar' && assignment.titleAr?.trim() ? assignment.titleAr : assignment.titleEn
  const description =
    lang === 'ar' && assignment.descriptionAr?.trim()
      ? assignment.descriptionAr
      : assignment.descriptionEn
  const alreadyCompleted = assignment.status === 'Completed'
  const questions = [...detail.questions].sort((a, b) => a.sortOrder - b.sortOrder)
  const blocks = [...detail.contentBlocks].sort((a, b) => a.sortOrder - b.sortOrder)

  const allAnswered = questions.every((q) => {
    if (q.type === 'MultipleChoice') return (multiAnswers[q.id] ?? []).length > 0
    return Boolean(singleAnswers[q.id])
  })

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={title}
        description={
          description ??
          t('employee.security.awareness.minutes', { count: assignment.estimatedMinutes })
        }
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/awareness">{t('employee.security.awareness.back')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        {assignment.number ? <Badge variant="outline">{assignment.number}</Badge> : null}
        <Badge variant="secondary">
          {t('employee.security.awareness.minutes', { count: assignment.estimatedMinutes })}
        </Badge>
        {assignment.isOverdue ? (
          <Badge variant="warning">{t('employee.security.awareness.badge.overdue')}</Badge>
        ) : null}
        {alreadyCompleted ? (
          <Badge variant="secondary">{t('employee.security.awareness.badge.completed')}</Badge>
        ) : null}
      </div>

      {(step === 'content' || alreadyCompleted) && (
        <section className="space-y-4 rounded-2xl border bg-card p-4 sm:p-6">
          <h2 className="text-lg font-semibold">{t('employee.security.awareness.readContent')}</h2>
          <div className="space-y-6">
            {blocks.map((block) => (
              <ContentBlockView key={block.id} block={block} lang={lang} />
            ))}
            {blocks.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t('employee.awareness.noContent')}</p>
            ) : null}
          </div>

          {!alreadyCompleted ? (
            <div className="flex flex-wrap gap-2 pt-2">
              {assignment.status === 'NotStarted' || !assignment.status ? (
                <Button type="button" className="min-h-11" disabled={starting} onClick={onStart}>
                  {t('employee.security.awareness.start')}
                </Button>
              ) : null}
              {detail.requireQuiz ? (
                <Button
                  type="button"
                  className="min-h-11"
                  disabled={startingQuiz}
                  onClick={onStartQuiz}
                >
                  {t('employee.awareness.takeQuiz')}
                </Button>
              ) : (
                <Button
                  type="button"
                  className="min-h-11"
                  disabled={completing}
                  onClick={onComplete}
                >
                  {t('employee.awareness.completeTraining')}
                </Button>
              )}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              {assignment.score != null
                ? t('employee.security.awareness.score', { score: assignment.score })
                : t('employee.security.awareness.badge.completed')}
            </p>
          )}
          {error && step === 'content' ? <p className="text-sm text-destructive">{error}</p> : null}
        </section>
      )}

      {step === 'quiz' && !alreadyCompleted ? (
        <section className="space-y-6 rounded-2xl border bg-card p-4 sm:p-6">
          <h2 className="text-lg font-semibold">{t('employee.security.awareness.quickCheck')}</h2>
          <p className="text-sm text-muted-foreground">
            {t('employee.security.awareness.quickCheckHint')}
            {detail.passingScorePercent
              ? ` · ${t('employee.awareness.passingScore', { score: detail.passingScorePercent })}`
              : ''}
          </p>
          <ol className="space-y-6">
            {questions.map((question, index) => (
              <QuestionView
                key={question.id}
                index={index}
                question={question}
                lang={lang}
                singleAnswers={singleAnswers}
                setSingleAnswers={setSingleAnswers}
                multiAnswers={multiAnswers}
                setMultiAnswers={setMultiAnswers}
              />
            ))}
          </ol>
          {error ? <p className="text-sm text-destructive">{error}</p> : null}
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="outline" onClick={() => setStep('content')}>
              {t('employee.security.awareness.backToContent')}
            </Button>
            <Button
              type="button"
              disabled={!allAnswered || submitting}
              onClick={onSubmit}
            >
              {t('employee.security.awareness.submit')}
            </Button>
          </div>
        </section>
      ) : null}

      {step === 'result' ? (
        <ResultPanel
          passed={passed}
          score={score}
          passingScore={passingScore}
          message={resultMessage}
          onRetry={
            passed
              ? undefined
              : detail.allowRetry &&
                  (detail.maxAttempts == null || detail.attemptsUsed < detail.maxAttempts)
                ? onRetry
                : undefined
          }
        />
      ) : null}
    </div>
  )
}

function ContentBlockView({
  block,
  lang,
}: {
  block: AwarenessV1ContentBlock
  lang: 'en' | 'ar'
}) {
  const title = lang === 'ar' && block.titleAr?.trim() ? block.titleAr : block.titleEn
  const body = lang === 'ar' && block.bodyAr?.trim() ? block.bodyAr : block.bodyEn

  return (
    <article className="space-y-2 border-s-2 border-primary/30 ps-4">
      <h3 className="font-medium">{title}</h3>
      {block.contentType === 'Text' && body ? (
        <div className="whitespace-pre-wrap text-sm leading-relaxed text-foreground/90">{body}</div>
      ) : null}
      {(block.contentType === 'ExternalLink' || block.contentType === 'VideoLink') && block.url ? (
        <a
          href={block.url}
          target="_blank"
          rel="noreferrer"
          className="text-sm text-primary underline-offset-2 hover:underline"
        >
          {block.url}
        </a>
      ) : null}
      {block.contentType === 'DocumentReference' && block.documentId ? (
        <p className="text-sm text-muted-foreground">Document: {block.documentId}</p>
      ) : null}
    </article>
  )
}

function QuestionView({
  index,
  question,
  lang,
  singleAnswers,
  setSingleAnswers,
  multiAnswers,
  setMultiAnswers,
}: {
  index: number
  question: AwarenessV1Question
  lang: 'en' | 'ar'
  singleAnswers: SingleAnswers
  setSingleAnswers: Dispatch<SetStateAction<SingleAnswers>>
  multiAnswers: MultiAnswers
  setMultiAnswers: Dispatch<SetStateAction<MultiAnswers>>
}) {
  const text =
    lang === 'ar' && question.questionAr?.trim() ? question.questionAr : question.questionEn
  const options = [...question.options].sort((a, b) => a.sortOrder - b.sortOrder)
  const multi = question.type === 'MultipleChoice'

  return (
    <li className="space-y-3">
      <p className="font-medium">
        {index + 1}. {text}
      </p>
      <fieldset className="space-y-2">
        {options.map((option) => {
          const label =
            lang === 'ar' && option.textAr?.trim() ? option.textAr : option.textEn
          const inputId = `${question.id}-${option.id}`
          const selected = multi
            ? (multiAnswers[question.id] ?? []).includes(option.id)
            : singleAnswers[question.id] === option.id
          return (
            <Label
              key={option.id}
              htmlFor={inputId}
              className={cn(
                'flex min-h-11 cursor-pointer items-center gap-3 rounded-xl border px-3 py-2 text-sm',
                selected ? 'border-primary bg-primary/5' : 'border-border',
              )}
            >
              <input
                id={inputId}
                type={multi ? 'checkbox' : 'radio'}
                name={question.id}
                checked={selected}
                onChange={() => {
                  if (multi) {
                    setMultiAnswers((prev) => {
                      const current = prev[question.id] ?? []
                      return {
                        ...prev,
                        [question.id]: selected
                          ? current.filter((id) => id !== option.id)
                          : [...current, option.id],
                      }
                    })
                  } else {
                    setSingleAnswers((prev) => ({ ...prev, [question.id]: option.id }))
                  }
                }}
                className="h-4 w-4"
              />
              <span>{label}</span>
            </Label>
          )
        })}
      </fieldset>
    </li>
  )
}

function ResultPanel({
  passed,
  score,
  passingScore,
  message,
  onRetry,
}: {
  passed: boolean | null
  score: number | null
  passingScore: number | null
  message: string | null
  onRetry?: () => void
}) {
  const { t } = useTranslation()
  return (
    <section className="space-y-4 rounded-2xl border bg-card p-4 sm:p-6" aria-live="polite">
      <h2 className="text-lg font-semibold">
        {passed
          ? t('employee.security.awareness.resultPass')
          : t('employee.security.awareness.resultFail')}
      </h2>
      {message ? <p className="text-sm text-muted-foreground">{message}</p> : null}
      {score != null ? (
        <p className="text-sm">{t('employee.security.awareness.score', { score })}</p>
      ) : null}
      {passingScore != null && !passed ? (
        <p className="text-sm text-muted-foreground">
          {t('employee.awareness.passingScore', { score: passingScore })}
        </p>
      ) : null}
      <div className="flex flex-wrap gap-2">
        {onRetry ? (
          <>
            <Button type="button" className="min-h-11" onClick={onRetry}>
              {t('employee.security.awareness.tryAgain')}
            </Button>
            <Button type="button" variant="outline" className="min-h-11" onClick={onRetry}>
              {t('employee.security.awareness.retakeQuiz')}
            </Button>
          </>
        ) : (
          <Button asChild className="min-h-11">
            <Link to="/employee/awareness">{t('employee.security.awareness.back')}</Link>
          </Button>
        )}
      </div>
    </section>
  )
}
