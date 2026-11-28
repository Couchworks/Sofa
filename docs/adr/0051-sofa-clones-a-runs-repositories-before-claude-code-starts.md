# Sofa clones a Run's Repositories before Claude Code starts, and starts it in the clone

A Sandbox allows git as named commands that Claude Code matches by their start, such as `Bash(git commit:*)` (ADR 0046). The first real Implement Run showed what that leaves out: Claude Code refuses `cd <clone> && git …` on its own, as a directory change before a version-control command may run what that Repository configures, and `git -C <clone> …` matches no rule. A Run could clone, but never branch, commit or push in its clone.

So Execution's command line clones each of the Run's Repositories into `/work/<name>` with its Repository token, checks out the Run's own branch in each when its Sandbox pushes (the one pushed before, so a retry or a Phase sent back carries on, or else a new one from the main branch), and starts Claude Code in the first clone with `/work` as a directory it may also write in. Git then runs there as plain `git <command>`, which the Sandbox's rules match. The Brief says where the clones are, which branch is checked out, and that `git -C` and `cd … &&` are refused.

## Considered Options

- **Allowing `git -C`**: one rule, but it matches every git command, `git config` included, which ADR 0046 keeps out.
- **Telling the agent to `cd` as a command of its own**: no change to the command line, but Claude Code may refuse it too, and every Brief would spend the Run's turns learning it.
- **A `--settings` file with hooks** approving git in `/work`: exact, but a program of Sofa's deciding inside the container (ADR 0037).

## Consequences

- Every Run spends a clone before Claude Code starts, the read-only ones included, which also gives Triage, Spec and Plan the code without a turn.
- A Run of several Repositories starts in the first; git in the others is still refused, until a Run is given one Repository or Claude Code lets a Sandbox name the directories git may run in.
- What a Repository's own git configuration or hooks would run is now run by Sofa's command line on clone and checkout; `git clone` runs no hooks of the Repository, and checkout runs only hooks git installs itself.
