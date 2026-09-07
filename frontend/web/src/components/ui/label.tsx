import * as LabelPrimitive from '@radix-ui/react-label'
import type * as React from 'react'
import { RequiredMark } from '@/components/ui/field'
import { cn } from '@/lib/utils'

export function Label({
  className,
  required,
  requiredLabel = 'required',
  children,
  ...props
}: React.ComponentProps<typeof LabelPrimitive.Root> & {
  /** Shows the global required asterisk next to the label text. */
  required?: boolean
  /** Screen-reader / title text for the required mark (pass a translated string). */
  requiredLabel?: string
}) {
  return (
    <LabelPrimitive.Root
      className={cn(
        'text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70',
        className,
      )}
      {...props}
    >
      {children}
      {required ? <RequiredMark label={requiredLabel} /> : null}
    </LabelPrimitive.Root>
  )
}
