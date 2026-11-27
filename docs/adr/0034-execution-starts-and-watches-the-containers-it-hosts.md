# Execution starts and watches the Worker containers it hosts

Execution starts each hosted Run's container itself (ADR 0017) and reads what happens there from the container's own output: the subscription's sign-in check, then Claude Code's `stream-json` Transcript, line by line, and its exit. Everything else a Run tells Sofa, such as ending itself, goes through the Agent Gateway with its Run token. The image stays the unmodified Claude Code (ADR 0008): the few shell lines run before it are Execution's command line, not a program of Sofa's baked into the image, and the container holds its Run token and nothing else of Sofa's. A container that Sofa starts is one Sofa can watch, so claiming, heartbeats and streaming over a protocol (ADR 0007) wait for the Workers Sofa doesn't start, such as a User's own machine; those will hold a Run the same way, through the Agent Gateway.

On a subscription Run, the container first prints `claude auth status --json` and waits for Execution's go-ahead on its input: Execution records what it saw on the Runner (ADR 0033) and starts Claude Code only when the volume is signed in to a subscription, so a signed-out volume refuses the Run before it spends anything.

The seam is `IWorkerContainers`: start a container with its image, volume, environment and command; read its lines; send it a line; stop it; wait for its exit. Docker implements it through its CLI on the box; Kubernetes Jobs implement it later through the pod's log and attach.

## Considered Options

- **A Worker program in the image that claims its Run over the Agent Gateway**, as ADR 0007 first had for every Worker: one protocol for hosted Workers and Users' machines alike, but a program of Sofa's inside every container that runs agent code, a claim and heartbeat protocol to build before any Run runs, and nothing gained on one box, where Execution already started the container and sees its output.
- **Checking the sign-in in a container of its own** before the Run's, as `sofa runner status` does: no go-ahead line, but a second container per Run, and a check that may pass for a volume the Run's container then finds signed out.

## Consequences

- When Execution restarts, it no longer sees the containers it started: their Runs end as *lost*, and their containers are removed.
- A container reaches the box's network like any other on Docker; holding only its Run token, it can call nothing of Sofa's but the Agent Gateway, but keeping it from the box's other ports is the Sandbox's network rules, still to come.
