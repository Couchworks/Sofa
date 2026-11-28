# Milestone 1b: Ready for real projects

**Status:** agreed. Vocabulary follows `CONTEXT.md`; decisions are in `docs/adr/`.

## Goal

Milestone 1's demo ran on a near-empty Repository. This short milestone settles the Known gaps that stop Sofa working on a small real Project: one that downloads packages to build, whose agents run ordinary shell commands, and whose pull requests can collide. It is one slice.

## The demo

On a small real Project with a `package.json` (or a .NET project) and its own tests:

1. A User labels an issue asking for a visible change. Implement installs the Project's packages, builds it, runs its tests, starts it and screenshots it, without a Question about commands it may run.
2. A second issue changing the same files is labelled at the same time. The first pull request is merged; the second now conflicts. Sofa notices, says so on the Work Item and in its delegating User's Inbox, and the User's **Resolve** starts a Run of the Builder that merges the main branch into its branch and pushes. Merging it finishes the Work Item, and the merge is reported as a new comment on the issue.

## What it settles

Each item is a Known gap of `docs/roadmap.md`; take each off the list as it is settled.

1. **Building with packages.** The egress relay (ADR 0045) lets Implement reach package registries: by default npm's, NuGet's and PyPI's, and each Project may add hosts to its own list. Nothing else of the network opens, and the change gets an ADR.
2. **Commands Claude Code asks about.** The Sandboxes' rules let through the everyday shell a Run needs (loops, pipes into other commands, `VAR=… command`, removing files inside its clone), so real Runs stop spending turns rephrasing commands. Judge the rules by the Transcripts of real Runs, not by guessing, and keep what the Sandbox keeps from a Run's sign-in and API key (ADR 0046) intact.
3. **A pull request that can't merge.** When a merge into the main branch leaves another Work Item's pull request conflicting, Code notices; the Work Item shows the conflict and waits in its delegating User's Inbox with **Resolve**, which starts a Run of the Builder told to merge the main branch into its own branch, resolve the conflict and push. Integrate's own handling of conflicts stays milestone 3's.
4. **The pull request's base.** Only a merge into the Repository's default branch finishes a Work Item.
5. **The last Report is an edit.** The merge is reported as a new comment on the issue, so GitHub notifies whoever follows it.
6. **A Track changed to Full.** A Work Item whose Track was changed to Full after Spec's turn shows Spec as skipped, not as waiting for a Run that never comes.
7. **An intermittent integration test.** Find which integration test fails now and then, and make it reliable.

## Out of scope

Everything else on the Known gaps list, and milestone 2 onwards.

**Shown when:** the demo runs on a small real Project of the User's on real GitHub, once on a subscription Runner, with the integration suite passing several times in a row.
