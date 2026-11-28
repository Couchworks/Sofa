# Every checkout runs the same local installation

Aspire's `WithDataVolume()` names Postgres's volume after the app host's path, so every checkout and every worktree started its own installation, with its own Users, GitHub App Connection, Projects and Runners. Running `main` after merging a slice then started an empty or stale Sofa, and agents copied databases between volumes by hand.

So the app host keeps Postgres's data in one volume of a fixed name, `sofa-postgres-data`, whichever checkout starts it. The passwords and keys Aspire generates already follow it: they live in user secrets under the app host's `UserSecretsId`, which the project file fixes for every checkout, and Records' files sit in the user's local application data folder. `Sofa:PostgresVolume` names another volume for whoever wants a separate installation. Two Postgres servers on one volume would corrupt it, so an app host refuses to start while a running container uses its volume. The integration tests keep their throwaway Postgres off every volume.

## Considered Options

- **A volume per checkout, as Aspire names it**: checkouts never collide, but each is its own installation, the problem itself.
- **A persistent Postgres container of a fixed name**: a second app host would share the running server instead of being refused, but the container then outlives the app host and is kept or replaced by Aspire's rules for persistent containers.

## Consequences

- One app host runs at a time on a box, unless the others name their own volume.
- A checkout whose migrations are ahead of `main` applies them to the shared installation; going back to `main` then runs against a newer schema. Such a slice starts on a volume of its own.
- Installations from before this stay in their old `sofa.apphost-<hash>-postgres-data` volumes until copied over or removed by hand.
