# The default Flow

The Flow a new Project starts with. Every Project may change its Phases, casts, Gates and Sandboxes; vocabulary follows `CONTEXT.md`.

```
Request ─▶ Triage ─▶ Spec ─▶ Plan ──(Split)──▶ ┌ Implement ─▶ Review ┐ ×N child Work Items
                                              └─────────────────────┘
                                                          │ every child done
                                                          ▼
                                   Integrate ─▶ Showcase ─▶ Validate ─▶ main branch
```

Triage, Spec and Plan run on the root Work Item, Implement and Review on each child, and Integrate, Showcase and Validate on the root again once every child is done. A plan that splits nothing sends the root through Implement and Review itself.

| Phase | Pattern | Cast (default model) | Gate | Sandbox | Report to the Request |
|---|---|---|---|---|---|
| Triage | Solo | Triager (Haiku) | Auto | Read-only | — |
| Spec | Solo, with Questions | Lead (Opus) | Approve | Read-only | The spec |
| Plan | Panel → Synthesis → Discussion, at most 2 rounds | Planners ×3, the Lead synthesising | Approve | Read-only | The plan and the decisions it makes |
| Implement | Solo, per child | Builder (Opus) | Auto | Pushes to its own branch | — |
| Review | Panel → Review-and-triage | Reviewers ×2–3, the Lead triaging | Auto | Read-only | — |
| Integrate | Solo, only on a conflict | Integrator (Sonnet) | Auto | Pushes to the Integration branch | — |
| Showcase | Solo | Showcaser (Sonnet), with a browser | Auto | Read-only, and the Preview | The Preview, screenshots and the Validation guide |
| Validate | none: a User's | — | per Validation mode | — | Accepted |

## Phase by phase

- **Triage** reads the Request, looks for duplicates, proposes Blocks, sets Priority when it has none, and picks the Track on its own, giving its reasons. **Small** skips Spec, plans with the Lead alone, and turns Plan's Gate to Review afterwards; **Full** runs everything. A User may change the Track at any time.
- **Spec** writes the spec with its acceptance criteria. The Lead asks Questions, each ending its Run, and a new Run picks up the answer. A Request already detailed enough lets the Lead skip its questions, but the spec still waits for approval.
- **Plan**: three Planners each write a plan; the Lead weighs them, keeping, merging and discarding parts with reasons; the Planners comment on that synthesis and the Lead revises, until it reports agreement or the rounds run out. The final plan lists the child Work Items, their Blocks, and the decisions it makes, each marked as new or as following existing Knowledge. Approving the plan Splits the Work Item and Publishes every decision marked new that the User left unchanged.
- **Implement** builds one child on its own branch, from the Integration branch, and screenshots anything visual it changed.
- **Review**: each Reviewer writes Findings; the Lead settles each as fix, follow-up or dismiss. A fix sends the child back to Implement as a round; past the round limit (default 2) it waits for a User.
- **Integrate**: finished children merge into the Integration branch one at a time; a conflict gets a Run of the Integrator to resolve it.
- **Showcase** waits for the Integration branch's Preview, goes through it against the spec's acceptance criteria, and writes the Validation guide.
- **Validate** is the User's: Accept, or file a Correction.

## Diversity among Planners and Reviewers

While Claude is the only vendor, Planners and Reviewers differ by model and by brief, so their Contributions don't converge on one answer:

- **Planners**: simplest thing that works (Sonnet); robust and ready to grow (Opus); the user's experience first (Opus).
- **Reviewers**: correctness (Opus); the spec and the Project's standards (Sonnet); optionally security (Opus).
