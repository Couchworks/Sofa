# The web client's components are shadcn/ui on Base UI

The web client's components are shadcn/ui's Base UI flavour (the `base-nova` style), which is what shadcn/ui sets up by default, rather than its Radix flavour. Components are copied into `web/src/components/ui` and owned there, as with any shadcn/ui project.

## Considered Options

- **shadcn/ui on Radix**: the original flavour, with the most examples written for it, but no longer shadcn/ui's default, so it would be the flavour to keep choosing by hand.
- **React Aria**: the strongest accessibility, but the smallest set of shadcn/ui components.

## Consequences

- Switching primitives later means replacing every component in `web/src/components/ui` and the code that uses their props.
