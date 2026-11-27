# Intake keeps its own copy of Request Provider bindings, learned from Flow's events

Flow keeps Projects and their bindings (ADR 0030); Intake needs each Project's Request Provider binding, its Connection, Repository and label, to know whether a webhook is a Request. Flow publishes `ProjectBound` to the `project-bound` exchange (ADR 0026) whenever a Project is created or rebound, carrying its key, both bindings and a version that grows with every change, and once more for every Project each time Flow starts. Intake keeps the newest version it has seen of each Project's binding and drops an older one arriving late; a `ProjectBound` without a Request Provider removes Intake's copy. Every webhook is matched against that copy alone, so a delivery never makes Intake ask Flow, and labelling keeps working while Flow is down.

## Considered Options

- **Asking Flow over HTTP for each delivery**: nothing to keep in step, but Flow on the path of every webhook, read for every issue comment on every Repository the App is installed on, most of which concern no Project.
- **Intake asking Flow for every binding when it starts**: no event on Flow's start, but Intake would wait on Flow to start, and still need events for the changes in between.

## Consequences

- A binding changed in Flow reaches Intake a moment later; an issue labelled in that moment is matched against the binding it had.
- Flow's start publishes one event per Project, which Intake drops when it already has that version.
- Code binds its own queue to the same exchange when it starts using the Code Provider's binding (slice 9).
