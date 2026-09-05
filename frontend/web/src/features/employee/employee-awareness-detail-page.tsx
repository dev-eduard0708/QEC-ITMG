import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError, awarenessApi } from '@/api/client'
import { PageHeader } from '@/components/page-header'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

type Step = 'content' | 'quiz' | 'result'

export function EmployeeAwarenessDetailPage() {
  const { assignmentId = '' } = useParams()
  const { t } = useTranslation()
  const qc = useQueryClient()
  const [step, setStep] = useState<Step>('content')
  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [resultMessage, setResultMessage] = useState<string | null>(null)
  const [passed, setPassed] = useState<boolean | null>(null)
  const [score, setScore] = useState<number | null>(null)

  const detailQuery = useQuery({
    queryKey: ['me', 'security', 'awareness', 'detail', assignmentId],
    queryFn: () => awarenessApi.mineGet(assignmentId),
    enabled: Boolean(assignmentId),
  })

  const submitMutation = useMutation({
    mutationFn: () =>
      awarenessApi.submitQuiz(
        assignmentId,
        Object.entries(answers).map(([questionId, optionId]) => ({ questionId, optionId })),
      ),
    onSuccess: async (result) => {
      setError(null)
      setPassed(result.passed)
      setScore(result.score)
      setResultMessage(result.message)
      setStep('result')
      await qc.invalidateQueries({ queryKey: ['me', 'security', 'awareness'] })
      await detailQuery.refetch()
    },
    onError: (err) => {
      setError(err instanceof ApiError ? err.message : t('docs.error.generic'))
    },
  })

  const module = detailQuery.data?.module
  const assignment = detailQuery.data?.assignment
  const questions = useMemo(
    () => [...(module?.questions ?? [])].sort((a, b) => a.displayOrder - b.displayOrder),
    [module?.questions],
  )
  const allAnswered = questions.length > 0 && questions.every((q) => Boolean(answers[q.id]))
  const alreadyCompleted = assignment?.status === 'Completed'

  if (detailQuery.isLoading) {
    return <p className="text-sm text-muted-foreground">{t('docs.loading')}</p>
  }

  if (!module) {
    return <p className="text-sm text-destructive">{t('employee.security.awareness.notFound')}</p>
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={module.title}
        description={module.summary ?? t('employee.security.awareness.minutes', { count: module.estimatedMinutes })}
        actions={
          <Button asChild variant="outline">
            <Link to="/employee/security/awareness">{t('employee.security.awareness.back')}</Link>
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge variant="outline">v{module.version}</Badge>
        <Badge variant="secondary">
          {t('employee.security.awareness.minutes', { count: module.estimatedMinutes })}
        </Badge>
        {alreadyCompleted ? (
          <Badge variant="secondary">{t('employee.security.awareness.badge.completed')}</Badge>
        ) : null}
      </div>

      {step === 'content' || alreadyCompleted ? (
        <section className="space-y-4 rounded-2xl border bg-card p-4 sm:p-6" aria-labelledby="awareness-content">
          <h2 id="awareness-content" className="text-lg font-semibold">
            {t('employee.security.awareness.readContent')}
          </h2>
          <div className="whitespace-pre-wrap text-sm leading-relaxed text-foreground/90">{module.body}</div>
          {!alreadyCompleted ? (
            <Button type="button" className="min-h-11" onClick={() => setStep('quiz')}>
              {t('employee.security.awareness.continueToQuiz')}
            </Button>
          ) : (
            <p className="text-sm text-muted-foreground">
              {assignment?.score != null
                ? t('employee.security.awareness.score', { score: assignment.score })
                : t('employee.security.awareness.badge.completed')}
              {assignment?.completedAtUtc
                ? ` · ${t('employee.security.awareness.completedOn', {
                    date: new Date(assignment.completedAtUtc).toLocaleString(),
                  })}`
                : ''}
            </p>
          )}
        </section>
      ) : null}

      {step === 'quiz' && !alreadyCompleted ? (
        <section className="space-y-6 rounded-2xl border bg-card p-4 sm:p-6" aria-labelledby="awareness-quiz">
          <h2 id="awareness-quiz" className="text-lg font-semibold">
            {t('employee.security.awareness.quickCheck')}
          </h2>
          <p className="text-sm text-muted-foreground">{t('employee.security.awareness.quickCheckHint')}</p>
          <ol className="space-y-6">
            {questions.map((question, index) => (
              <li key={question.id} className="space-y-3">
                <p className="font-medium">
                  {index + 1}. {question.questionText}
                </p>
                <fieldset className="space-y-2">
                  <legend className="sr-only">{question.questionText}</legend>
                  {[...question.options]
                    .sort((a, b) => a.displayOrder - b.displayOrder)
                    .map((option) => {
                      const inputId = `${question.id}-${option.id}`
                      const selected = answers[question.id] === option.id
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
                            value={option.id}
                            checked={selected}
                            onChange={() =>
                              setAnswers((prev) => ({ ...prev, [question.id]: option.id }))
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
            <Button type="button" variant="outline" className="min-h-11" onClick={() => setStep('content')}>
              {t('employee.security.awareness.backToContent')}
            </Button>
            <Button
              type="button"
              className="min-h-11"
              disabled={!allAnswered || submitMutation.isPending}
              onClick={() => submitMutation.mutate()}
            >
              {t('employee.security.awareness.submit')}
            </Button>
          </div>
        </section>
      ) : null}

      {step === 'result' ? (
        <section className="space-y-4 rounded-2xl border bg-card p-4 sm:p-6" aria-live="polite">
          <h2 className="text-lg font-semibold">
            {passed
              ? t('employee.security.awareness.resultPass')
              : t('employee.security.awareness.resultFail')}
          </h2>
          <p className="text-sm text-muted-foreground">{resultMessage}</p>
          {score != null ? (
            <p className="text-sm">{t('employee.security.awareness.score', { score })}</p>
          ) : null}
          <div className="flex flex-wrap gap-2">
            {!passed ? (
              <>
                <Button type="button" className="min-h-11" onClick={() => setStep('content')}>
                  {t('employee.security.awareness.tryAgain')}
                </Button>
                <Button type="button" variant="outline" className="min-h-11" onClick={() => setStep('quiz')}>
                  {t('employee.security.awareness.retakeQuiz')}
                </Button>
              </>
            ) : (
              <Button asChild className="min-h-11">
                <Link to="/employee/security/awareness">{t('employee.security.awareness.back')}</Link>
              </Button>
            )}
          </div>
        </section>
      ) : null}
    </div>
  )
}
