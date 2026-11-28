# A running Run's Repository token is replaced in its container before it expires

An installation token lasts an hour while a Run may last two (ADR 0044). The Worker's command line now keeps the Repository token in a file, `/tmp/sofa/github-token`, which git's credential helper reads on each use, and in GitHub's CLI's own configuration under `/tmp/sofa/gh`, then takes `GH_TOKEN` out of the environment before Claude Code starts. Ten minutes before a token expires (`Workers:RefreshRepositoryTokenBefore`), Execution asks Code for a fresh one for the same Run, which Code gives only while that Run hasn't ended and keeps as the Run's grant, and writes it into the container with `docker exec`, passing it by its variable's name, never on a command line. A refresh that fails is tried again 30 seconds later, and the old token keeps working until it expires.

The Builder opens its pull request itself, with GitHub's CLI (`gh pr create`), which the image gains and the own-branch Sandbox allows with `gh pr` commands only; so `gh` needs the fresh token as much as git does.

## Considered Options

- **A credential helper asking the Agent Gateway** for a token with the Run token: always fresh, but a program of Sofa's in the image (ADR 0037), and `gh` reads no git credential helper.
- **Code opening the pull request** when Implement finishes, through GitHub's API (ADR 0019): no `gh` in the image, but the Agent couldn't title, describe or fix its own pull request, and Code would have to guess which Repository the Run pushed to.
- **Longer-lived tokens**: GitHub's installation tokens last an hour, and a user's token would act as a person.

## Consequences

- Execution watches one more timer per running Run, and its `IWorkerContainer.RunInsideAsync` takes environment variables.
- The token sits in a file the agent's tools may read, as it sat in their environment before; Transcripts are scrubbed of it (ADR 0043).
