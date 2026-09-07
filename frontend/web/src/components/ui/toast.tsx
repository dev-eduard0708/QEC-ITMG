import { useSyncExternalStore, type ReactNode } from 'react'
import { AlertCircle, CheckCircle2, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { dismissToast, getToasts, subscribeToasts, type ToastItem } from '@/components/ui/toast-store'

function ToastIcon({ variant }: { variant: ToastItem['variant'] }) {
  if (variant === 'success') {
    return <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" aria-hidden />
  }
  return (
    <AlertCircle
      className={cn(
        'mt-0.5 h-4 w-4 shrink-0',
        variant === 'error' ? 'text-destructive' : 'text-muted-foreground',
      )}
      aria-hidden
    />
  )
}

export function Toaster(): ReactNode {
  const items = useSyncExternalStore(subscribeToasts, getToasts, getToasts)

  if (items.length === 0) return null

  return (
    <div
      className="pointer-events-none fixed inset-x-0 bottom-4 z-[100] flex flex-col items-center gap-2 px-4 sm:items-end sm:px-6"
      aria-live="polite"
      aria-relevant="additions"
    >
      {items.map((item) => (
        <div
          key={item.id}
          role="status"
          className={cn(
            'pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-lg border px-3.5 py-3 text-sm shadow-lg',
            'bg-card text-card-foreground',
            item.variant === 'success' && 'border-emerald-200 dark:border-emerald-900',
            item.variant === 'error' && 'border-destructive/40',
            item.variant === 'info' && 'border-border',
          )}
        >
          <ToastIcon variant={item.variant} />
          <div className="min-w-0 flex-1 space-y-0.5 leading-snug">
            <p>{item.message}</p>
            {item.description ? (
              <p className="text-xs text-muted-foreground">{item.description}</p>
            ) : null}
          </div>
          <button
            type="button"
            className="rounded-md p-0.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
            aria-label="Dismiss"
            onClick={() => dismissToast(item.id)}
          >
            <X className="h-3.5 w-3.5" aria-hidden />
          </button>
        </div>
      ))}
    </div>
  )
}
