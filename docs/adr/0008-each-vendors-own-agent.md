# Runs use each vendor's own agent

A Run of a Claude model runs Claude Code, and a later vendor's runs that vendor's own agent (such as Codex for OpenAI), each behind the same Runner and Worker seam. Sofa doesn't route models through a cross-vendor agent such as OpenCode, nor run an agent loop of its own: each vendor's agent is what it supports, tests and allows its subscriptions in, and Anthropic's terms allow a subscription only in the unmodified Claude Code binary.

## Considered Options

- **One cross-vendor agent (OpenCode)**, as Cronker uses for models other than Claude: one program and one transcript format, but subscriptions can't be used through it, and every vendor's features arrive late or not at all.
- **Sofa's own agent loop**: everything under our control, and everything ours to build and keep up.

## Consequences

- Each vendor's agent brings its own transcript format, settings and allowlist to convert, and its terms to check before it is added.
