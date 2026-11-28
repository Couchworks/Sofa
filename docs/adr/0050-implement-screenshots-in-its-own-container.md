# Implement builds, starts and screenshots the Project's software in its own container

The Worker image gains a headless browser: Playwright's CLI with its Chromium headless shell, at fixed versions. It runs inside the Run's own container, beside Claude Code, and only there. The own-branch Sandbox, Implement's, allows on top of git: the Project's own scripts (`npm run`, `npm start`, `npm test`), `node`, and `playwright screenshot`, which loads a page from the working copy (`file://`) or from what the Run started on `localhost`, and writes a PNG into `/work` for `finish` to attach (ADR 0040). Read-only Sandboxes allow none of it. The network stays as ADR 0045 has it: the container reaches its vendor's API, GitHub and the Agent Gateway, nothing else, so no package registry either; a Project whose build downloads packages can't be built in a Run yet.

## Considered Options

- **A browser in a container of its own**, sharing a network with the Run's: the browser would hold none of the Run's credentials, but a second container per Run for Execution to start, place and clean up, and the Run's server would have to listen beyond `localhost`.
- **Screenshots by Sofa after the Run**, from a URL the Run names: no browser for the agent to drive, but nothing to start the software with once the container is gone, and the agent couldn't look at what it shot.
- **Package registries through the relay** (`registry.npmjs.org` and the like): most Projects could build, but the relay would pass on names that serve anyone's code, which ADR 0045 keeps out; that waits for its own decision.

## Consequences

- `npm run` and `node` run code the agent may write itself, so in Implement an agent set on it can read what the container holds, its API key or its User's sign-in among them, past the tool rules of ADR 0046; the network and the scrubbing of Transcripts are what is left. ADR 0046's consequence that other commands widen the Sandbox is taken here, for Implement only.
- The Worker image grows by the browser, a few hundred megabytes.
- Showcase (milestone 4) will screenshot Previews instead, through a Sandbox of its own.
