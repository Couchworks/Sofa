# Requests stack on what blocks them, and accepting one accepts its Stack

Under the Gated Validation mode, a Request's work waits on its own Integration branch until a User accepts it. A Request blocked by others starts its Integration branch from theirs, merged together when there are several, so the factory keeps working on dependent Requests while earlier ones wait for Validation. Accepting a Request accepts every Request of its Stack not yet accepted, and they reach the main branch in order, one merge at a time per branch; accepting one lower in a Stack leaves those above it waiting. When a lower Request merges, those stacked on it are rebased on their own, and a conflict becomes an agent Run rather than the User's problem. A Correction is blocked by the Request it corrects, so it stacks on that work.

## Considered Options

- **Batch**: every Gated Request lands on one shared staging branch, validated and merged together. One validation for a week of work, but independent Requests hold each other up, and a Request that isn't accepted has to be taken back out of work mixed with the rest. Accepting a whole Stack gives batch's single validation exactly where work is linked.

## Consequences

- No Request reaches the main branch before those that block it, whatever its Validation mode: an Autonomous Request stacked on a Gated one waits for it.
- A Stack is a graph, not a chain: a Request blocked by two independent ones starts from a merge of both, and accepting it accepts both.
- Agents may add a Blocks relation, which only makes work wait; only a User removes one.
