import { Check, Circle, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { AccessCase } from '@/api/client'
import {
  getAccessWorkflowSteps,
  type WorkflowStepState,
} from '@/features/it/access-ux/access-status'
import { cn } from '@/lib/utils'

function StepIcon({ state }: { state: WorkflowStepState }) {
  if (state === 'completed') {
    return (
      <span className="flex h-7 w-7 items-center justify-center rounded-full bg-emerald-600 text-white dark:bg-emerald-500">
        <Check className="h-3.5 w-3.5" aria-hidden />
      </span>
    )
  }
  if (state === 'problem') {
    return (
      <span className="flex h-7 w-7 items-center justify-center rounded-full bg-destructive text-destructive-foreground">
        <X className="h-3.5 w-3.5" aria-hidden />
      </span>
    )
  }
  if (state === 'current') {
    return (
      <span className="flex h-7 w-7 items-center justify-center rounded-full border-2 border-primary bg-background text-primary">
        <Circle className="h-2.5 w-2.5 fill-current" aria-hidden />
      </span>
    )
  }
  return (
    <span className="flex h-7 w-7 items-center justify-center rounded-full border border-muted-foreground/30 bg-muted text-muted-foreground">
      <Circle className="h-2 w-2" aria-hidden />
    </span>
  )
}

export function AccessWorkflowStepper({
  accessCase,
  className,
}: {
  accessCase: Pick<AccessCase, 'status' | 'isReadyToClose' | 'verificationOutcome'>
  className?: string
}) {
  const { t } = useTranslation()
  const steps = getAccessWorkflowSteps(accessCase)

  return (
    <nav
      aria-label={t('access.workflow.title')}
      className={cn('w-full', className)}
    >
      {/* Desktop: horizontal */}
      <ol className="hidden items-stretch gap-0 md:flex">
        {steps.map((step, index) => (
          <li key={step.id} className="flex min-w-0 flex-1 items-center">
            <div className="flex min-w-0 flex-col items-center gap-1.5 px-1 text-center">
              <StepIcon state={step.state} />
              <span
                className={cn(
                  'text-xs font-medium leading-tight',
                  step.state === 'current' && 'text-foreground',
                  step.state === 'completed' && 'text-muted-foreground',
                  step.state === 'upcoming' && 'text-muted-foreground/70',
                  step.state === 'problem' && 'text-destructive',
                )}
              >
                {t(step.labelKey)}
              </span>
            </div>
            {index < steps.length - 1 ? (
              <div
                className={cn(
                  'mx-1 h-px min-w-[12px] flex-1',
                  step.state === 'completed' ? 'bg-emerald-600/60 dark:bg-emerald-500/50' : 'bg-border',
                )}
                aria-hidden
              />
            ) : null}
          </li>
        ))}
      </ol>

      {/* Mobile: vertical */}
      <ol className="flex flex-col gap-0 md:hidden">
        {steps.map((step, index) => (
          <li key={step.id} className="flex gap-3">
            <div className="flex flex-col items-center">
              <StepIcon state={step.state} />
              {index < steps.length - 1 ? (
                <div
                  className={cn(
                    'my-1 w-px flex-1 min-h-4',
                    step.state === 'completed' ? 'bg-emerald-600/60 dark:bg-emerald-500/50' : 'bg-border',
                  )}
                  aria-hidden
                />
              ) : null}
            </div>
            <div className="pb-4 pt-1">
              <p
                className={cn(
                  'text-sm font-medium',
                  step.state === 'current' && 'text-foreground',
                  step.state === 'completed' && 'text-muted-foreground',
                  step.state === 'upcoming' && 'text-muted-foreground/70',
                  step.state === 'problem' && 'text-destructive',
                )}
              >
                {t(step.labelKey)}
              </p>
            </div>
          </li>
        ))}
      </ol>
    </nav>
  )
}
