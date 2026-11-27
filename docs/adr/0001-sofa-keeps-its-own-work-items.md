# Sofa keeps its own Work Items; Requests stay in their Provider

Sofa doesn't track work in GitHub Issues, Jira or the like, nor does it run its Flow on their tickets. Each Request a Provider brings in becomes a Work Item in Sofa, which may be split into child Work Items that run through the Flow on their own, and Sofa posts a Report back to the Request each time a Phase ends. One outside ticket can therefore turn into many pieces of agent work, which no Provider's own model could express, and the Flow behaves the same whichever Provider a Request came from.

## Considered Options

- **Run the Flow on the outside tickets themselves**, mapping Phases to their workflows: no second record, but every Provider's workflow and fields differ, and splitting a Request would fill the outside tracker with agent-sized tickets.

## Consequences

- Sofa owns Work Item state, Runs and Contributions; the Request is read when work starts and written to only through Reports.
- A Request's Report starts with a summary and links to the detail in Sofa.
