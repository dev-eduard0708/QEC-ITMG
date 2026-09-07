import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

export function AccessItemTaskRow({
  name,
  action,
  status,
  privileged,
  isCustom,
  isMandatory,
  completed,
  onComplete,
  quiet,
  className,
}: {
  name: string
  action?: string | null
  status?: string | null
  privileged?: boolean
  isCustom?: boolean
  isMandatory?: boolean
  completed?: boolean
  onComplete?: () => void
  /** Visually de-emphasize completed rows */
  quiet?: boolean
  className?: string
}) {
  const { t } = useTranslation()
  const isDone = completed || status === 'Completed' || status === 'Done'

  return (
    <li
      className={cn(
        'flex flex-wrap items-center gap-2 rounded-md border px-3 py-2 text-sm',
        quiet && isDone && 'border-transparent bg-muted/30 opacity-70',
        className,
      )}
    >
      <span className={cn('min-w-0 flex-1 font-medium', isDone && quiet && 'font-normal')}>
        {name}
      </span>
      {action ? (
        <Badge variant="outline" className="text-[10px] font-normal">
          {action}
        </Badge>
      ) : null}
      {isCustom ? <Badge variant="secondary">{t('access.custom')}</Badge> : null}
      {privileged ? (
        <Badge variant="outline" className="text-[10px] font-normal">
          {t('access.privileged')}
        </Badge>
      ) : null}
      {isMandatory ? <Badge variant="warning">{t('access.mandatory')}</Badge> : null}
      {status && !quiet ? <Badge variant="secondary">{status}</Badge> : null}
      {onComplete && !isDone ? (
        <Button type="button" size="sm" variant="secondary" className="ms-auto" onClick={onComplete}>
          {t('access.actions.completeItem')}
        </Button>
      ) : null}
    </li>
  )
}
