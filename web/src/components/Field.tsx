import type { ComponentProps } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

type FieldProps = ComponentProps<typeof Input> & { label: string; problem?: string }

export function Field({ label, problem, id, ...input }: FieldProps) {
  const problemId = problem ? `${id}-problem` : undefined
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} aria-invalid={problem ? true : undefined} aria-describedby={problemId} {...input} />
      {problem && (
        <p id={problemId} className="text-destructive text-xs">
          {problem}
        </p>
      )}
    </div>
  )
}
