import { cn } from '@/lib/utils'

export type TimelineItem = {
  id: string
  timestamp: string | Date
  title: string
  description?: string | null
  actor?: string | null
  type?: string | null
  status?: string | null
}

export type TimelineProps = {
  items: TimelineItem[]
  emptyMessage?: string
  className?: string
  formatTimestamp?: (value: string | Date) => string
}

function defaultFormatTimestamp(value: string | Date): string {
  const date = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(date.getTime())) {
    return String(value)
  }
  return date.toLocaleString()
}

export function Timeline({
  items,
  emptyMessage = 'No timeline entries yet.',
  className,
  formatTimestamp = defaultFormatTimestamp,
}: TimelineProps) {
  if (items.length === 0) {
    return (
      <p className={cn('text-sm text-muted-foreground', className)}>{emptyMessage}</p>
    )
  }

  return (
    <ol className={cn('relative space-y-6 border-s border-border ms-3 ps-6', className)}>
      {items.map((item) => (
        <li key={item.id} className="relative">
          <span
            className="absolute -start-[1.6875rem] mt-1.5 h-3 w-3 rounded-full border-2 border-background bg-primary"
            aria-hidden
          />
          <div className="space-y-1">
            <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <time className="text-xs text-muted-foreground" dateTime={String(item.timestamp)}>
                {formatTimestamp(item.timestamp)}
              </time>
              {item.type || item.status ? (
                <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {[item.type, item.status].filter(Boolean).join(' · ')}
                </span>
              ) : null}
            </div>
            <div className="text-sm font-medium text-foreground">{item.title}</div>
            {item.actor ? (
              <div className="text-xs text-muted-foreground">{item.actor}</div>
            ) : null}
            {item.description ? (
              <p className="text-sm text-muted-foreground whitespace-pre-wrap">{item.description}</p>
            ) : null}
          </div>
        </li>
      ))}
    </ol>
  )
}
