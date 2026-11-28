# A Contribution reaches Records before its Run ends, its attachments copied out of the container

A Run finishes by calling `finish` on the Agent Gateway with an Outcome of its Phase, its Contribution in Markdown, and the paths of files in its working directory to attach, such as screenshots. The Gateway asks Execution to finish the Run, and Execution, which started the container and still holds it (ADR 0037), copies each file out with `docker exec`, refusing any path that leads out of `/work`, then hands the Contribution and its files to Records over HTTP in one request, and only then marks the Run finished. Records keeps the Contribution per Work Item and Run, and each attachment as a file behind its storage interface (ADR 0014), on local disk for now. A Run that finishes has its Contribution kept; a Records that can't keep it leaves the Run running, so the Agent can call `finish` again.

## Considered Options

- **The attachment's bytes as an argument of `finish`**: no copying out of the container, but an Agent can't write a screenshot's bytes as base64 in a tool call, and a large one would spend its output tokens.
- **The Agent uploading its files to the Agent Gateway over HTTP itself**: works for Workers Sofa doesn't start, but needs `curl` and the Run token in every Sandbox, the read-only one included.
- **The Contribution on the broker**: as services usually change one another's state (ADR 0012), but screenshots would sit in Execution's outbox and Records' inbox, in the database; the files then need a store both services share, or an HTTP call anyway.

## Consequences

- Records answers Execution over HTTP for this, as Connections does for tokens (ADR 0027). A Contribution is kept again if Execution sends it again for the same Run, replacing the first.
- A Contribution is kept before the Run is marked finished, so a Run stopped in between keeps a Contribution though it didn't finish.
- Workers Sofa doesn't start, such as a User's own machine, will need another way to hand files in, through the Agent Gateway.
- Only Execution's lexical and `realpath` checks keep a Run from attaching a file outside `/work`; a Run allowed to change files could still race a symbolic link between the check and the copy.
