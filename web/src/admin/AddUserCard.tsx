import { useForm } from 'react-hook-form'
import { api } from '@/api/client'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

type AddedByLogin = { gitHubLogin: string }

export function AddUserCard({ workspaceId }: { workspaceId: string }) {
  const { register, handleSubmit, reset } = useForm<AddedByLogin>({ defaultValues: { gitHubLogin: '' } })
  const adding = api.useMutation('post', '/api/workspaces/{workspaceId}/users', { onSuccess: () => reset() })

  const add = handleSubmit(({ gitHubLogin }) =>
    adding.mutate({ params: { path: { workspaceId } }, body: { gitHubLogin: gitHubLogin.trim() } }),
  )

  return (
    <Card>
      <CardHeader>
        <CardTitle>Add a User</CardTitle>
        <CardDescription>By their GitHub login. They join this Workspace when they first sign in with GitHub.</CardDescription>
      </CardHeader>
      <form onSubmit={add}>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="github-login">GitHub login</Label>
            <Input id="github-login" placeholder="octocat" {...register('gitHubLogin', { required: true })} />
          </div>
          {adding.isSuccess && (
            <Alert>
              <AlertTitle>{adding.data.gitHubLogin} is added</AlertTitle>
              <AlertDescription>They join when they sign in with GitHub.</AlertDescription>
            </Alert>
          )}
          {adding.isError && (
            <Alert variant="destructive">
              <AlertTitle>{adding.error?.title ?? 'They couldn’t be added'}</AlertTitle>
            </Alert>
          )}
        </CardContent>
        <CardFooter className="mt-4">
          <Button type="submit" disabled={adding.isPending}>
            Add
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}
