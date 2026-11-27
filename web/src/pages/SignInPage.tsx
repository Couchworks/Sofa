import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { gitHubSignInUrl, isGitHubSignInAvailable, safeReturnUrl, signIn, type SignInForm } from '@/auth/account'
import { Centered } from '@/components/Centered'
import { Field } from '@/components/Field'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { goTo } from '@/lib/navigation'

export function SignInPage({ returnUrl, problem }: { returnUrl: string | null; problem: string | null }) {
  const form = useForm<SignInForm>({ defaultValues: { email: '', password: '' } })
  const [refusal, setRefusal] = useState<string | undefined>(problem ?? undefined)
  const gitHub = useQuery({ queryKey: ['github-sign-in'], queryFn: isGitHubSignInAvailable })

  const submit = form.handleSubmit(async (values) => {
    setRefusal(undefined)
    const refused = await signIn(values)
    if (refused) {
      setRefusal(refused.message)
    } else {
      goTo(safeReturnUrl(returnUrl))
    }
  })

  return (
    <Centered>
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Sign in to Sofa</CardTitle>
          <CardDescription>With your email and password, or with GitHub.</CardDescription>
        </CardHeader>
        <CardContent>
          <form className="grid gap-4" onSubmit={submit} noValidate>
            <Field id="email" label="Email" type="email" autoComplete="username" {...form.register('email', { required: true })} />
            <Field id="password" label="Password" type="password" autoComplete="current-password" {...form.register('password', { required: true })} />
            {refusal && (
              <p role="alert" className="text-destructive text-sm">
                {refusal}
              </p>
            )}
            <Button type="submit" disabled={form.formState.isSubmitting}>
              Sign in
            </Button>
            {gitHub.data && (
              <Button type="button" variant="outline" onClick={() => goTo(gitHubSignInUrl(returnUrl))}>
                Sign in with GitHub
              </Button>
            )}
          </form>
        </CardContent>
      </Card>
    </Centered>
  )
}
