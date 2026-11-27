# Subscriptions stay personal; teams share API keys

Anthropic's Consumer Terms forbid making an account available to anyone else, and its Claude Code terms forbid a platform to collect, store or intermediate Claude.ai credentials: a subscription may be used only by its own User, signing in to the unmodified Claude Code through Anthropic's own flow. So a personal Runner on a subscription runs only Runs of Requests its User delegated, on a Worker of that User's in which they signed in themselves; its credentials never leave that Worker, and Sofa never reads, stores or passes them on. What a Team shares is a team Runner on an API key, which the terms allow for a customer's own authorized users. A Run spends the delegating User's personal Runner for its vendor when they have one, and otherwise their Team's Runner.

## Considered Options

- **One member's subscription for the whole Team**: the cheapest way for a small team, but it makes the account available to others, against the terms, and puts the subscription at risk of enforcement.
- **Sofa stores subscription tokens**, as a setup token pasted in: simpler to host, but exactly the collecting and storing of credentials the terms forbid.

## Consequences

- A Claude Team or Enterprise plan works as personal Runners: each member signs in with their own seat.
- Subscription usage limits assume ordinary individual use, and Anthropic enforces its terms without notice, so a factory leaning on subscriptions may be throttled; API keys are the dependable path.
- A Request delegated by a User with no Runner for its vendor runs on their Team's, or waits when there is none.
