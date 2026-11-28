# A Question ends its Run, and Flow keeps it with its answer

A Run asks a Question by calling `ask` on the Agent Gateway, which asks Execution to end the Run with it. Execution marks the Run *finished* with the Question in place of an Outcome, and `RunEnded` carries the Question. Flow, which owns Work Items (ADR 0012), keeps the Question on the Run's Work Item and sends `AskOnRequest` to Intake, which posts it on the issue (ADR 0036); `QuestionAnsweredOnRequest` brings the answer back to Flow, which keeps it with the Question. A later Run reads the Work Item's Questions and their answers through `read_work_item`.

## Considered Options

- **The Agent Gateway sending the Question to Flow beside ending the Run**: one hop less, but two commands that can each be lost or arrive alone, and a Question from a Run that had already ended.
- **Intake keeping the Questions**: it already keeps the comment each was posted as, but the Inbox (slice 10) answers Questions too, and a Question belongs to the Work Item, not to its issue.
- **A Run state of its own for a Question**: clearer at a glance, but the Run did end the way its Agent meant it to, and every reader of Run states would learn one more.

## Consequences

- Execution's Run is the one place saying a Run ended (ADR 0039); Flow learns of the Question only from it.
- Starting a new Run of the Phase once a Question is answered is Flow's, in slice 8.
