import { useTranslation } from 'react-i18next'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase()
  return `${parts[0]![0] ?? ''}${parts[1]![0] ?? ''}`.toUpperCase()
}

export function AccessIdentitySummary({
  name,
  email,
  onChange,
  className,
}: {
  name: string
  email?: string | null
  onChange?: () => void
  className?: string
}) {
  const { t } = useTranslation()

  return (
    <div
      className={cn(
        'flex items-center gap-3 rounded-lg border bg-muted/20 px-3 py-2.5',
        className,
      )}
    >
      <Avatar className="h-10 w-10">
        <AvatarFallback>{initials(name || '?')}</AvatarFallback>
      </Avatar>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium">{name || '—'}</p>
        {email ? <p className="truncate text-xs text-muted-foreground">{email}</p> : null}
      </div>
      {onChange ? (
        <Button type="button" size="sm" variant="secondary" onClick={onChange}>
          {t('access.change')}
        </Button>
      ) : null}
    </div>
  )
}
