import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'

/**
 * Visible required marker for labels (light + dark).
 * Pass a localized `label` for the screen-reader text.
 */
export function RequiredMark({
  label = 'required',
  className,
}: {
  label?: string
  className?: string
}) {
  return (
    <span
      className={cn(
        'ms-1 inline-flex translate-y-px items-center font-semibold text-destructive',
        className,
      )}
      title={label}
    >
      <span aria-hidden="true">*</span>
      <span className="sr-only">({label})</span>
    </span>
  )
}

/** Control chrome when a required value is missing. */
export function requiredControlClass(invalid?: boolean, className?: string) {
  return cn(
    invalid &&
      [
        'border-destructive/70 bg-destructive/[0.04]',
        'focus-visible:ring-destructive dark:bg-destructive/[0.09]',
        'placeholder:text-destructive/50',
      ],
    className,
  )
}

/** Short legend: "* Required fields" — place near form tops. */
export function RequiredFieldsHint({ className }: { className?: string }) {
  const { t } = useTranslation()
  return (
    <p className={cn('text-xs text-muted-foreground', className)}>
      <span className="font-semibold text-destructive" aria-hidden="true">
        *
      </span>{' '}
      {t('form.requiredFieldsHint')}
    </p>
  )
}
