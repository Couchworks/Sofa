# Agreed Knowledge lives in Providers, never only in Sofa

Sofa keeps a Working Record of everything its Runs did: Contributions, transcripts, proposals and their discussion. Final, agreed material such as accepted decisions and terminology is Published to a Knowledge Provider (Confluence, a repository's `docs/`), so every User finds it where the team already looks, without access to Sofa. Sofa holds the proposal and the discussion behind it; the Provider holds the outcome.

## Considered Options

- **Sofa stores decisions and terms itself**, as Cronker does: one place, versioned, but knowledge hidden behind Sofa's sign-in and a second home for what teams keep in their wiki or repository.
- **No store in Sofa at all**: impossible, since plans, critiques, screenshots and transcripts have to live somewhere to be reviewed.

## Consequences

- Publishing to a Repository's `docs/` is a pull request of its own that Sofa merges straight away, never part of an Integration branch: a decision agreed in a plan stays agreed even when the code that follows it needs a Correction.
