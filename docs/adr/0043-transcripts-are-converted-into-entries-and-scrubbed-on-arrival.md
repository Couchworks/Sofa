# Transcripts are converted into entries and scrubbed on arrival

A Run's Transcript reaches Records as Claude Code's `stream-json` lines, in batches as Execution reads them (ADR 0037). Records converts each line on arrival into entries of one format (role, text, tool name and input, tool result, time), as Cronker does (its ADR 0006), and keeps no raw line: a tool result fills the entry of the call it answers, the start and end of a session become a system entry saying the model, the tools allowed, the credentials and how it ended, and thinking is left out. Known secret patterns become `[scrubbed]` before anything is stored: Sofa's own tokens (`sofa_…` codes and signed JWTs such as Run tokens), GitHub's tokens, Anthropic's API keys and OAuth tokens, AWS access keys and named secret keys, Slack's tokens, private-key blocks and bearer values. A tool input over 20 KB (as JSON) or a tool result over 20 KB keeps its first 20 KB and says it was cut.

The scrubbing lives in `Sofa.Contracts` and runs twice: in Execution on each line before it enters the outbox, so neither Execution's outbox nor Records' inbox ever holds a known secret, and in Records again before converting, so a Transcript arriving another way is scrubbed too. A JSON line is scrubbed in its strings, so it stays JSON even when a private key's block never ends.

## Considered Options

- **Raw lines in Records**, as slice 6 kept them, converted when read: lossless, but every reader handles Claude Code's format, a vendor's other format later means two, and a secret missed by today's patterns stays readable forever.
- **Scrubbing only in Records**: one place, but every line would rest unscrubbed in two outbox and inbox tables until they are cleaned, as ADR 0027 avoids for tokens.

## Consequences

- A secret of another shape is not caught; a User finding one needs a way to delete the Transcript, still to come.
- A change in Claude Code's `stream-json` needs the converter changed; entries already stored keep their shape.
- A reader reading with `after` sees an earlier tool call again once its result arrives (`toolResultLine`).
- Transcripts stored by slice 6 as raw lines are dropped by the migration.
