import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { Checkbox } from '@/components/ui/checkbox'
import { cn } from '@/lib/utils'

export function AccessItemSelectionCard({
  name,
  description,
  selected,
  onSelectedChange,
  secondaryLabel,
  privileged,
  disabled,
  className,
}: {
  name: string
  description?: string | null
  selected: boolean
  onSelectedChange: (selected: boolean) => void
  /** e.g. Default / Optional */
  secondaryLabel?: string | null
  privileged?: boolean
  disabled?: boolean
  className?: string
}) {
  const { t } = useTranslation()

  return (
    <button
      type="button"
      disabled={disabled}
      onClick={() => onSelectedChange(!selected)}
      className={cn(
        'flex w-full items-start gap-3 rounded-lg border p-3 text-start transition-colors',
        selected ? 'border-primary/40 bg-primary/5' : 'bg-card hover:bg-muted/40',
        disabled && 'pointer-events-none opacity-50',
        className,
      )}
    >
      <Checkbox
        checked={selected}
        onCheckedChange={(v) => onSelectedChange(v === true)}
        onClick={(e) => e.stopPropagation()}
        className="mt-0.5"
        aria-label={name}
      />
      <div className="min-w-0 flex-1 space-y-1">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-sm font-medium leading-snug">{name}</span>
          {privileged ? (
            <Badge variant="outline" className="text-[10px] font-normal">
              {t('access.privileged')}
            </Badge>
          ) : null}
        </div>
        {description ? (
          <p className="line-clamp-2 text-xs text-muted-foreground">{description}</p>
        ) : null}
        {secondaryLabel ? (
          <p className="text-[11px] text-muted-foreground">{secondaryLabel}</p>
        ) : null}
      </div>
    </button>
  )
}
