# What the Sandbox keeps from a Run's sign-in and API key

Claude Code needs a subscription's sign-in (on its volume, in `/claude`) or an API key (in its environment) inside the Run's container, so neither can be taken out of it. What Sofa does instead:

- **The agent's own tools are denied them.** Claude Code's rules deny reading, editing or writing `/claude` and reading `/proc`, where every process's environment is; editing git's or Claude Code's configuration in any directory (`.git/`, `.claude/`), which would make later commands or settings run what the agent wrote; and `git diff --no-index`, which reads files outside a Repository. Bash is allowed only as named git commands, never `git` as a whole or `git config`.
- **Transcripts are scrubbed** of known secret patterns before anything is stored (ADR 0043), Anthropic's keys and OAuth tokens and GitHub's tokens among them.
- **The network** lets the container reach only its vendor's API, GitHub and the Agent Gateway (ADR 0045).

This protects against an agent that reads or prints a secret by accident, or obeys an ordinary instruction to: its tools refuse, and what it prints is scrubbed before Sofa keeps it. It does not protect against an agent set on getting them, such as one following instructions planted in a Repository. Git runs commands of its own from options (`--upload-pack`, `-c`, a Repository's hooks) that permission rules matching a command's start can't foresee; and a secret that leaves the container through what the Run may reach, such as a commit or a pull request comment on GitHub, a Contribution, or a request to the vendor's API, is not scrubbed there. The API key is the Run's Runner's, which every Run on that Runner holds anyway; a subscription's sign-in is its User's own (ADR 0009), and lets whoever holds it spend that subscription until it is signed out.

## Considered Options

- **Claude Code's subprocess sandbox** (`CLAUDE_CODE_SUBPROCESS_ENV_SCRUB`), which takes Anthropic's credentials out of what its tools run: it needs bubblewrap, which can't make namespaces in a container without privileges that would weaken the container itself.
- **A key helper** (`apiKeyHelper`) instead of the environment: the key would still be a file the agent's processes can read.

## Consequences

- A Run that must run other commands, such as a project's tests, needs its Sandbox to allow them, each widening what an agent set on the secrets can do.
- Revoking an API key, or signing a subscription out, is the remedy for a leak.
