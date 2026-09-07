import * as React from 'react'
import { cn } from '@/lib/utils'

/**
 * Shared selected/unselected surface styles for choice cards (light + dark).
 * Pair with `.selectable-surface` CSS for the selected border animation.
 */
export function selectableSurfaceClass(selected: boolean, className?: string) {
  return cn(
    'selectable-surface rounded-lg border text-start transition-[border-color,background-color,box-shadow,transform] duration-200',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
    selected
      ? [
          'border-primary bg-primary/10 text-foreground',
          'dark:border-primary dark:bg-primary/20',
        ]
      : [
          'border-border/80 bg-card/60 text-foreground',
          'hover:border-primary/35 hover:bg-muted/50',
          'dark:bg-card/40 dark:hover:border-primary/40 dark:hover:bg-muted/30',
        ],
    className,
  )
}

type SelectableCardProps = React.ComponentProps<'button'> & {
  selected?: boolean
}

/**
 * Reusable choice card button with a strong selected highlight (light/dark)
 * and a soft animated border when selected.
 */
export function SelectableCard({
  selected = false,
  className,
  type = 'button',
  children,
  ...props
}: SelectableCardProps) {
  return (
    <button
      type={type}
      aria-pressed={selected}
      data-selected={selected ? 'true' : 'false'}
      className={selectableSurfaceClass(selected, className)}
      {...props}
    >
      {children}
    </button>
  )
}
