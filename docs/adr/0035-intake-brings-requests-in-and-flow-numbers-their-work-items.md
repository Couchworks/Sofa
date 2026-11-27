# Intake brings Requests in, and Flow numbers their Work Items

When an issue in a bound Repository gets the Project's label, Intake keeps it as a Request, one per Project and issue, so labelling it again, or GitHub delivering the event twice, changes nothing. It asks Identity over HTTP which User of the Workspace holds the GitHub account that added the label, by its account id, never its login (ADR 0029). It then sends `OpenWorkItem` to Flow, which owns Work Items (ADR 0012): Flow numbers them from a counter on each Project, so the first is `DEMO-1` and a number is never used twice, and keeps the Request's id so a repeated command opens nothing new.

A label added by an account linked to no User of the Workspace still opens the Work Item, with no delegating User, so nothing runs it. Intake publishes `RequestAwaitingDelegation`, which Notices records as an Inbox item for the Workspace's Admins, who are the Project's Admins while every User of a Workspace has Write in its Projects (ADR 0031). Slice 10's Inbox lists it.

## Considered Options

- **Intake keeping the Work Item's number**: no command to Flow before a key exists, but Work Items and their keys would live in two services.
- **No Work Item until someone delegates**: nothing to show that isn't delegated, but the Request would live only in Intake, and the Admin delegating it would make the Work Item from the Inbox instead of the Project's list.

## Consequences

- Work Items carry the Request's Repository, issue number, title and address, which Flow copies once; the issue's later edits don't reach Sofa in this milestone.
- The labelling User is found at the moment of labelling: linking an account afterwards doesn't delegate a Request already in.
