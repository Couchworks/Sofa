# `sofa runner login` signs a subscription in on the box and tells Execution the result

A User signs their subscription Runner in with `sofa runner login <runner>`, a command run in a terminal on the box (milestone 1's Settled 1). It asks Execution for the Runner's Worker volume, starts a throwaway Worker container on that volume running `claude auth login --claudeai` attached to the terminal, so the User signs in through Anthropic's own flow in the unmodified Claude Code, then runs `claude auth status --json` in a fresh container and tells Execution only whether the volume is signed in to a subscription and when that sign-in ends. Nothing of Sofa's reads the volume's files, and nothing else of the status, such as the account's email, is passed on or kept (ADR 0009).

Each subscription Runner has a volume of its own, named after the Runner (`sofa-worker-<runner id>`), made by Docker the first time it is mounted.

The command talks to Execution's internal HTTP address, which only the box reaches, not the public API: whoever can run Docker on the box could read the volume anyway, so a User token would protect nothing more, and a terminal sign-in to Sofa would be a third way to sign in.

## Considered Options

- **Execution starts the containers itself**, on a button in the web client: no command to run, but the sign-in's link and code would pass through Sofa's web client, which Settled 1 rules out until checked against Anthropic's terms.
- **Execution reads `claude auth status` itself**: it could check again later on its own, but it would need Docker before slice 6 gives it Workers; `sofa runner status <runner>` checks again in the meantime.

## Consequences

- The Worker image (`worker/`) has to be built on the box before signing in.
- The recorded sign-in state is what the command last saw: a sign-in revoked elsewhere shows as signed in until the next check or Run.
