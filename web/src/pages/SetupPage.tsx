import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { setUp, type SetupForm } from '@/auth/account'
import { userManager } from '@/auth/session'
import { Centered } from '@/components/Centered'
import { Field } from '@/components/Field'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

const fields: { name: keyof SetupForm; label: string; type?: string; autoComplete?: string }[] = [
  { name: 'setupCode', label: 'Setup code', autoComplete: 'off' },
  { name: 'name', label: 'Your name', autoComplete: 'name' },
  { name: 'email', label: 'Email', type: 'email', autoComplete: 'username' },
  { name: 'password', label: 'Password', type: 'password', autoComplete: 'new-password' },
  { name: 'workspaceName', label: 'Workspace', autoComplete: 'organization' },
  { name: 'teamName', label: 'Team', autoComplete: 'off' },
]

export function SetupPage() {
  const form = useForm<SetupForm>({
    defaultValues: { setupCode: '', name: '', email: '', password: '', workspaceName: '', teamName: '' },
  })
  const [refusal, setRefusal] = useState<string>()

  const submit = form.handleSubmit(async (values) => {
    setRefusal(undefined)
    const refused = await setUp(values)
    if (!refused) {
      await userManager.signinRedirect()
      return
    }

    const problems = Object.entries(refused.problems) as [keyof SetupForm, string][]
    for (const [field, problem] of problems) {
      form.setError(field, { message: problem })
    }
    if (problems.length === 0) {
      setRefusal(refused.message)
    }
  })

  return (
    <Centered>
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Set up Sofa</CardTitle>
          <CardDescription>
            Enter the setup code from Identity&apos;s log, then create the first User, their Workspace and its Team.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form className="grid gap-4" onSubmit={submit} noValidate>
            {fields.map(({ name, ...field }) => (
              <Field key={name} id={name} problem={form.formState.errors[name]?.message} {...field} {...form.register(name)} />
            ))}
            {refusal && (
              <p role="alert" className="text-destructive text-sm">
                {refusal}
              </p>
            )}
            <Button type="submit" disabled={form.formState.isSubmitting}>
              Set up
            </Button>
          </form>
        </CardContent>
      </Card>
    </Centered>
  )
}
