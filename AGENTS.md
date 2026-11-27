# Sofa

A software factory where AI agents take a Request through a Flow of Phases on their own, while Users review every step, at the time or afterwards.

- `CONTEXT.md`: the vocabulary. Use its terms and avoid the listed synonyms.
- `docs/adr/`: decisions that are hard to reverse. Add one, numbered from the next free one, for each new such decision.
- `docs/roadmap.md`: what is settled and the milestones; `docs/specs/` holds each milestone's spec.
- `README.md`: how to run Sofa and its tests.

## Conventions

- **Conventional Commits** for commits and branch names alike, with the same types: `feat`, `fix`, `refactor`, `test`, `docs`, `ci`, `build`, `chore`.
  - A branch is `<type>/<short-kebab-description>`, such as `feat/github-sign-in` or `fix/webhook-signature`, named for the main kind of change it carries.
  - A commit carries exactly one type and only changes of that type: an ADR goes in its own `docs:` commit, new tests in a `test:` commit.
  - Commit messages end with their description; the commit hook refuses Co-Authored-By and other AI attribution lines.
- **Code carries no comments**: the why goes into names.
