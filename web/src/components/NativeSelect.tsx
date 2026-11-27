import type { ComponentProps } from 'react'
import { cn } from 'cn'

export function NativeSelect({ className, ...props }: ComponentProps<'select'>) {
  return (
    <select
      className={cn(
        'border-input focus-visible:border-ring focus-visible:ring-ring/50 dark:bg-input/30 h-8 w-full min-w-0 rounded-lg border bg-transparent px-2 py-1 text-base outline-none focus-visible:ring-3 disabled:opacity-50 md:text-sm',
        className,
      )}
      {...props}
    />
  )
}
