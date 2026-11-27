---
status: accepted
---

# Phases are built from a fixed set of Patterns

Agents work together inside a Phase in one of a few Patterns (Solo, Panel, Synthesis, Discussion, Review-and-triage) rather than in a graph each Project draws itself. A fixed set covers the planning panel, the discussion rounds and the review panel already wanted, and each Pattern can be shown, limited and reviewed in a way a free graph can't. Every Discussion has a round limit and ends when its synthesizer reports agreement.

## Considered Options

- **A general graph of agents per Phase**: anything is possible, but every Project would design and debug its own orchestration, and the UI couldn't explain a Phase at a glance.
- **One agent per Phase**, as in Cronker: simplest, but no way for several models to propose, criticise and review together.

## Consequences

- Splitting a Work Item is no Pattern: the plan lists the child Work Items, and Sofa makes them when the plan is approved.
