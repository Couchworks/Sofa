# Reports, Questions and their answers are comments on the issue

Intake writes to a Request's issue only when told to, by `PostReport`, `AskOnRequest` or an event about its Work Item, with an installation token Connections hands it (ADR 0027).

- **A Report** is one comment per Phase: a summary, the detail collapsed beneath it in `<details>`, and a link to the Work Item in Sofa. Intake keeps the comment's id, so posting again for that Phase edits the comment instead of adding one; if someone deleted it, a new one is posted.
- **A Question** is a comment of its own. GitHub's issue comments have no threads, so a reply is any later comment on the issue while the Question is open: the first one from an account linked to a User with access to the Project answers the newest open Question, and Intake publishes `QuestionAnsweredOnRequest` for Flow. The answer is the comment without the quoted lines and @-mentions it starts with, such as GitHub's quote reply or a mention of the App; a comment that is only those answers nothing. Anyone else's comment, and every comment once no Question is open, is ignored.
- **The issue's state** is kept only when the Project turns it on: Intake labels the issue `sofa: <phase>` as its Work Item enters each Phase, removing the previous one, and closes it when the work reaches the main branch.

## Considered Options

- **Only a quoted reply counts as an answer**: closer to a thread, but quoting is easy to drop, and GitHub's quote reply quotes the text, not which comment it came from.
- **A new Report comment each time**: no ids to keep, but the issue fills with a Phase's earlier summaries.

## Consequences

- The App needs `issues: write` and the `issue_comment` event, which its manifest already asks for.
- A User answering on the issue answers as themself; Flow gets their User id, not their GitHub login.
