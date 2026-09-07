import type { ReactNode } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'

export function AccessCurrentActionCard({
  title,
  description,
  children,
  actions,
  className,
}: {
  title: string
  description?: string | null
  children?: ReactNode
  actions?: ReactNode
  className?: string
}) {
  return (
    <Card
      className={cn(
        'border-primary/25 bg-primary/[0.03] shadow-sm dark:bg-primary/[0.06]',
        className,
      )}
    >
      <CardHeader className="pb-3">
        <CardTitle className="text-base">{title}</CardTitle>
        {description ? <CardDescription>{description}</CardDescription> : null}
      </CardHeader>
      {(children || actions) && (
        <CardContent className="space-y-4">
          {children}
          {actions ? <div className="flex flex-wrap gap-2">{actions}</div> : null}
        </CardContent>
      )}
    </Card>
  )
}
