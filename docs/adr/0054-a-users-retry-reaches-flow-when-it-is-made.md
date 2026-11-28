# A User's Retry reaches Flow when it is made

When a User retries a Run gone wrong, Execution publishes `RunRetriedByAUser`, naming the new Run, the one it retries and its Work Item, as soon as it has saved the retry. Flow follows the retry at once if its Work Item waits on the Run retried: the wait is settled and the Work Item shows as running the retry. This takes back ADR 0048's consequence that a User's Retry shows as waiting until its Run ends. Flow still follows a retry it first hears of from the retry's `RunEnded`, so a lost or late `RunRetriedByAUser` changes nothing.

## Considered Options

- **Flow asking Execution to retry**, as a command through Flow: Flow would know first, but every client of Execution's Retry, the Runs API among them, would have to go through Flow.
- **`RunChanged` alone**, which the web client hears: it says a Run changed, but not that it retries one, and Flow would have to ask Execution what it is.

## Consequences

- Execution publishes one more event, which Flow alone hears.
