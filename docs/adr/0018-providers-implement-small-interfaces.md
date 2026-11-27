# Providers implement small interfaces, one per capability

A Provider implements only the interfaces its outside system can serve, such as reading new Requests, posting Reports, keeping a Request's state, or filing new Requests, rather than one interface per kind with operations a system answers as "not supported". Sofa learns what a Provider can do from which interfaces it implements, and a Project's settings that need a capability, such as keeping a Request's labels in step with its Phase, are offered only where its Providers have it. Every Provider works through a Connection and never holds credentials of its own.

## Considered Options

- **One interface per kind with capability flags**: fewer types, but every caller checks flags, and every new Provider stubs out what it can't do.
