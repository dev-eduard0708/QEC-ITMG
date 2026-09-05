import * as React from 'react'
import { cn } from '@/lib/utils'

type AvatarContextValue = {
  showFallback: boolean
  setShowFallback: (show: boolean) => void
}

const AvatarContext = React.createContext<AvatarContextValue | null>(null)

export function Avatar({
  className,
  ...props
}: React.ComponentProps<'span'>) {
  const [showFallback, setShowFallback] = React.useState(true)
  const value = React.useMemo(
    () => ({ showFallback, setShowFallback }),
    [showFallback],
  )

  return (
    <AvatarContext.Provider value={value}>
      <span
        data-slot="avatar"
        className={cn(
          'relative flex h-9 w-9 shrink-0 overflow-hidden rounded-full border border-border bg-muted',
          className,
        )}
        {...props}
      />
    </AvatarContext.Provider>
  )
}

export function AvatarImage({
  className,
  src,
  alt = '',
  ...props
}: React.ComponentProps<'img'>) {
  const ctx = React.useContext(AvatarContext)
  const setShowFallback = ctx?.setShowFallback

  React.useEffect(() => {
    setShowFallback?.(true)
  }, [src, setShowFallback])

  if (!src) return null

  return (
    <img
      data-slot="avatar-image"
      src={src}
      alt={alt}
      className={cn('absolute inset-0 aspect-square h-full w-full object-cover', className)}
      referrerPolicy="no-referrer"
      onLoad={() => setShowFallback?.(false)}
      onError={() => setShowFallback?.(true)}
      {...props}
    />
  )
}

export function AvatarFallback({
  className,
  ...props
}: React.ComponentProps<'span'>) {
  const ctx = React.useContext(AvatarContext)
  if (ctx && !ctx.showFallback) return null

  return (
    <span
      data-slot="avatar-fallback"
      className={cn(
        'flex h-full w-full items-center justify-center text-xs font-semibold text-muted-foreground',
        className,
      )}
      {...props}
    />
  )
}
