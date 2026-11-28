# Flow follows one Run at a time on a Work Item, and moves it by that Run's end

Flow keeps, on each Work Item, its Phase, its Track, the one Run it is following, and what it waits for, if anything: an answer, a User's approval, its pull request, or a User after a Run gone wrong or a pull request closed unmerged. It moves the Work Item only when it hears `RunEnded` for the Run it follows, by how that Run ended: an Outcome of its Phase passes the Phase's Gate (Auto and Review afterwards go on at once, the latter leaving that Phase's end Unverified; Approve waits), a Question waits for its answer, and a failed or lost Run is followed into the retry Execution names (ADR 0038). Anything else, such as a test Run on the Work Item, moves nothing. Every User decision is a command to Flow over the broker: approving a Phase goes on, sending it back keeps the User's comment and starts a new Run of that Phase, an answer in Sofa or on the issue starts a new Run of the Phase that asked, and a User's Retry of the Run gone wrong is followed once Execution says it ended, as `RunEnded` names the Run it retries. Triage's Outcome sets the Track: Small skips Spec and turns Plan's Approve Gate to Review afterwards. After the last Phase the Work Item waits for its pull request, and Code's `PullRequestMerged` finishes it.

What a Work Item waits for goes to its delegating User's Inbox as `WorkItemAwaitsUser`, and `WorkItemNoLongerAwaitsUser` settles it, so Notices keeps the Inbox without knowing the Flow. The Reports of Spec, Plan and Implement are posted from each Run's Contribution, read from Records when the Phase ends: the summary is its first paragraph that is no heading, the detail the whole Contribution.

## Considered Options

- **Flow asking Execution for the Work Item's Runs** whenever something happens: no Run ids kept twice, but every move would wait on an HTTP read, and a Run Flow never meant to start, such as a test Run, would look like one of its own.
- **A Report written by the Run itself**, as a field of `finish`: exactly what the Agent meant to say on the issue, but one more thing for every Brief to ask, and nothing to post for a Contribution whose Run forgot it.
- **Notices reading the Inbox from Flow over HTTP**: one copy of what waits, but the Inbox would join Work Items across services on every read, and Flow being down would empty it.

## Consequences

- A Work Item runs one Run at a time; Panels and child Work Items (milestone 3) will need Flow to follow several.
- A User's Retry shows as waiting until its Run ends, as Flow hears of the retry only then.
- Work Items opened before this have no Phase and never start; they show as awaiting delegation.
- `WorkItemDetails`, read through the App Gateway, gains the state, Track, Runs, Phase ends, what it waits for and its pull request, and `RunDetails` gains the Run's Brief.
