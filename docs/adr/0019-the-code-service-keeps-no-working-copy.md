# The Code service keeps no working copy

The Code service changes Repositories only through their host's API: creating branches, updating a branch from its base, retargeting and merging pull requests. Anything that needs a working copy, such as resolving a conflict or a rebase the host's API can't do, is a Run of an agent in a Worker. So Sofa never writes code itself, needs no disk or git checkouts in its services, and every change to code is a Run's, with its Transcript, that a User can review.

## Considered Options

- **The Code service rebases and merges in checkouts of its own**: faster for clean rebases, but disk and git in a service, and commits by Sofa that no Run accounts for.
