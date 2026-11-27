import type { ComponentProps } from 'react'
import { cn } from 'cn'

export function Textarea({ className, ...props }: ComponentProps<'textarea'>) {
  return (
    <textarea
      className={cn(
        'border-input placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-ring/50 dark:bg-input/30 min-h-24 w-full min-w-0 rounded-lg border bg-transparent px-2.5 py-1.5 text-base outline-none focus-visible:ring-3 disabled:opacity-50 md:text-sm',
        className,
      )}
      {...props}
    />
  )
}
