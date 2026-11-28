# Runs read their Request as it is now on its issue

`read_work_item` gives a Run its Request as the issue reads at that moment: the Agent Gateway asks Intake over HTTP, and Intake reads the issue's title and body from GitHub with an installation token. Edits made to the issue after it was labelled reach the next Run, without Sofa keeping a copy of the issue's text or following its edits. The rest of what the tool returns is read the same way, each from the service that owns it: the Work Item and its Questions from Flow, earlier Contributions from Records.

## Considered Options

- **Intake keeping the issue's text, updated from GitHub's `issues.edited` events**: no call to GitHub per read, but a second copy of the Request in Sofa, which ADR 0001 keeps outside, and edits missed while a webhook is lost.
- **The text copied once into the Work Item, as ADR 0035 had it**: nothing to read at Run time, but a User correcting the issue would have to tell Sofa some other way.

## Consequences

- This changes ADR 0035's consequence that the issue's later edits don't reach Sofa: they reach Runs, while the Work Item's title stays the one it came in with.
- Each `read_work_item` spends a GitHub API call; an issue Sofa can no longer read still gives the Run its Work Item, with the title it came in with and why the issue couldn't be read.
