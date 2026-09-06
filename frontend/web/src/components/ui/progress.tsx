import type * as React from 'react'
import { cn } from '@/lib/utils'

type ProgressProps = React.ComponentProps<'div'> & {
  value?: number | null
  max?: number
}

export function Progress({ value = 0, max = 100, className, ...props }: ProgressProps) {
  const safeMax = max <= 0 ? 100 : max
  const raw = typeof value === 'number' && Number.isFinite(value) ? value : 0
  const pct = Math.max(0, Math.min(100, (raw / safeMax) * 100))

  return (
    <div
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={Math.round(pct)}
      className={cn('relative h-2 w-full overflow-hidden rounded-full bg-secondary', className)}
      {...props}
    >
      <div
        className="h-full rounded-full bg-primary transition-[width] duration-300 ease-out"
        style={{ width: `${pct}%` }}
      />
    </div>
  )
}
